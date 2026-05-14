using ComplianceWala.Domain.Enums;

namespace ComplianceWala.Application.DTOs.Responses;

public record SessionResponse(
    Guid SessionId,
    string BusinessGstin,
    string FilingPeriod,
    SessionStatus Status,
    decimal TotalItcInBooks,
    decimal TotalItcInGstr2b,
    decimal TotalItcAtRisk,
    int TotalMismatches,
    DateTime CreatedAt,
    DateTime? CompletedAt
);