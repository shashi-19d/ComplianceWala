using System.Diagnostics;
using ComplianceWala.Application.DTOs;
using ComplianceWala.Application.DTOs.Requests;
using ComplianceWala.Application.DTOs.Responses;
using ComplianceWala.Application.Interfaces;
using ComplianceWala.Domain.Entities;
using ComplianceWala.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace ComplianceWala.Application.Services;

public sealed class ReconciliationOrchestrator : IReconciliationOrchestrator
{
    private readonly IGstr1Parser _gstr1Parser;
    private readonly IGstr2bParser _gstr2bParser;
    private readonly ISupplierRiskService _supplierRiskService;
    private readonly IReconciliationEngine _reconciliationEngine;
    private readonly IMismatchClassifier _mismatchClassifier;
    private readonly IReconciliationSessionRepository _sessionRepository;
    private readonly ILogger<ReconciliationOrchestrator> _logger;

    public ReconciliationOrchestrator(
        IGstr1Parser gstr1Parser,
        IGstr2bParser gstr2bParser,
        ISupplierRiskService supplierRiskService,
        IReconciliationEngine reconciliationEngine,
        IMismatchClassifier mismatchClassifier,
        IReconciliationSessionRepository sessionRepository,
        ILogger<ReconciliationOrchestrator> logger)
    {
        _gstr1Parser = gstr1Parser;
        _gstr2bParser = gstr2bParser;
        _supplierRiskService = supplierRiskService;
        _reconciliationEngine = reconciliationEngine;
        _mismatchClassifier = mismatchClassifier;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<SessionResponse> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken ct = default)
    {
        var session = ReconciliationSession.Create(
            request.BusinessGstin,
            request.Year,
            request.Month);

        await _sessionRepository.AddAsync(session, ct);

        _logger.LogInformation(
            "Session {SessionId} created for GSTIN {Gstin}, period {Year}-{Month:D2}",
            session.Id, request.BusinessGstin, request.Year, request.Month);

        return MapToSessionResponse(session);
    }

    public async Task<SessionResponse> UploadPurchaseRegisterAsync(
        Guid sessionId,
        UploadGstrRequest request,
        CancellationToken ct = default)
    {
        var session = await GetSessionOrThrowAsync(sessionId, ct);

        // Parse GSTR-1 JSON → Invoice domain objects
        var parseResult = await _gstr1Parser.ParseAsync(request.JsonContent);

        session.LoadPurchaseRegisterInvoices(parseResult.Invoices);

        await _sessionRepository.UpdateAsync(session, ct);

        _logger.LogInformation(
            "Session {SessionId}: {Count} purchase register invoices loaded. " +
            "Total ITC in books: ₹{Itc:N0}",
            sessionId, parseResult.TotalInvoiceCount, parseResult.TotalItc);

        return MapToSessionResponse(session);
    }

    public async Task<ReconciliationCompleteResponse> UploadGstr2bAndReconcileAsync(
        Guid sessionId,
        UploadGstrRequest request,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var session = await GetSessionOrThrowAsync(sessionId, ct);

        // ── Step 1: Parse GSTR-2B ─────────────────────────────────
        var gstr2bResult = await _gstr2bParser.ParseAsync(request.JsonContent);
        session.LoadGstr2bInvoices(gstr2bResult.Invoices);

        // ── Step 2: Build supplier ITC amounts from purchase register
        var supplierItcAmounts = session.PurchaseRegisterInvoices
            .GroupBy(i => i.SupplierGstin)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalItc));

        var purchaseRegisterGstins = session.PurchaseRegisterInvoices
            .Select(i => i.SupplierGstin)
            .Distinct();

        // ── Step 3: Process supplier risk ─────────────────────────
        var supplierRisks = await _supplierRiskService.ProcessGstr2bFilingDataAsync(
            purchaseRegisterGstins,
            gstr2bResult.SupplierFilingDates,
            supplierItcAmounts,
            session.FilingPeriod,
            ct);

        // ── Step 4: Build supplier profile lookup for engine ───────
        var supplierProfiles = supplierRisks
            .Select(r => SupplierProfile.Create(r.SupplierGstin, r.SupplierName))
            .ToList();

        // ── Step 5: Run reconciliation engine ─────────────────────
        await _reconciliationEngine.ReconcileAsync(
            session,
            supplierProfiles,
            ct);

        // ── Step 6: Classify all mismatches ───────────────────────
        var reconciliationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var classifications = _mismatchClassifier.ClassifyAll(
            session.Mismatches,
            reconciliationDate);

        // ── Step 7: Persist completed session ─────────────────────
        await _sessionRepository.UpdateAsync(session, ct);

        stopwatch.Stop();

        _logger.LogInformation(
            "Reconciliation complete for session {SessionId}. " +
            "{Mismatches} mismatches found. ₹{ItcAtRisk:N0} ITC at risk. {Ms}ms",
            sessionId,
            session.TotalMismatches,
            session.TotalItcAtRisk,
            stopwatch.ElapsedMilliseconds);

        return new ReconciliationCompleteResponse(
            Session: MapToSessionResponse(session),
            Mismatches: MapToMismatchResponses(classifications, session),
            SupplierRisks: supplierRisks,
            ProcessingTimeMs: stopwatch.ElapsedMilliseconds
        );
    }

    public async Task<ReconciliationCompleteResponse?> GetSessionResultAsync(
        Guid sessionId,
        CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null) return null;

        var reconciliationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var classifications = _mismatchClassifier.ClassifyAll(
            session.Mismatches,
            reconciliationDate);

        var supplierGstins = session.PurchaseRegisterInvoices
            .Select(i => i.SupplierGstin)
            .Distinct();

        var supplierItcAmounts = session.PurchaseRegisterInvoices
            .GroupBy(i => i.SupplierGstin)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.TotalItc));

        var supplierRisks = await _supplierRiskService
            .GetSupplierRiskSummariesAsync(supplierGstins, supplierItcAmounts, ct);

        return new ReconciliationCompleteResponse(
            Session: MapToSessionResponse(session),
            Mismatches: MapToMismatchResponses(classifications, session),
            SupplierRisks: supplierRisks,
            ProcessingTimeMs: 0
        );
    }

    // ── Private Helpers ───────────────────────────────────────────

    private async Task<ReconciliationSession> GetSessionOrThrowAsync(
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null)
            throw new DomainException("SESSION_NOT_FOUND",
                $"Reconciliation session {sessionId} not found.");
        return session;
    }

    private static SessionResponse MapToSessionResponse(ReconciliationSession session) =>
        new(
            SessionId:       session.Id,
            BusinessGstin:   session.BusinessGstin,
            FilingPeriod:    session.FilingPeriod,
            Status:          session.Status,
            TotalItcInBooks: session.TotalItcInBooks,
            TotalItcInGstr2b:session.TotalItcInGstr2b,
            TotalItcAtRisk:  session.TotalItcAtRisk,
            TotalMismatches: session.TotalMismatches,
            CreatedAt:       session.CreatedAt,
            CompletedAt:     session.CompletedAt
        );

    private static IReadOnlyList<MismatchResponse> MapToMismatchResponses(
        IReadOnlyList<DTOs.MismatchClassification> classifications,
        ReconciliationSession session)
    {
        return classifications.Select(c =>
        {
            var mismatch = session.Mismatches
                .FirstOrDefault(m => m.Id == c.MismatchId);

            return new MismatchResponse(
                MismatchId:        c.MismatchId,
                MismatchType:      c.MismatchType,
                Priority:          c.Priority,
                RecommendedAction: c.RecommendedAction,
                ItcAmountAtRisk:   c.ItcAmountAtRisk,
                SupplierName:      c.SupplierName,
                SupplierGstin:     c.SupplierGstin,
                InvoiceNumber:     c.InvoiceNumber,
                DaysToDeadline:    c.DaysToDeadline,
                DeadlineDate:      c.DeadlineDate,
                RequiresCaReview:  c.RequiresCaReview,
                ActionSummary:     c.ActionSummary,
                AiExplanation:     mismatch?.AiExplanation,
                AiExplanationHindi:mismatch?.AiExplanationHindi
            );
        }).ToList().AsReadOnly();
    }
}