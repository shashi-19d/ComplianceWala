# ComplianceWala 🇮🇳

> AI-Powered GST Reconciliation & ITC Risk Intelligence Engine for Indian SMBs

## The Problem
India has 1.51 crore GST-registered businesses. Every month, SMBs 
manually reconcile GSTR-1 vs GSTR-2B — losing an average of ₹12,000/month 
to missed ITC claims and duplicate payments. Existing tools show mismatches. 
None explain WHY or predict which ITC is at risk before the deadline.

## What ComplianceWala Does
- Ingests GSTR-1 and GSTR-2B JSON exports
- AI classifies each mismatch by root cause (6 mismatch types)
- Predicts ITC recovery probability per supplier
- Generates plain Hindi/English explanation per mismatch
- Produces CA-ready reconciliation reports

## Architecture
Clean Architecture | .NET 8 Minimal API | PostgreSQL | EF Core | 
Angular 17 | Ollama (phi3:mini) | Quartz.NET | xUnit

## Tech Stack
- **Backend:** C# .NET 8, Minimal API
- **Database:** PostgreSQL + EF Core
- **AI Layer:** Ollama (phi3:mini) — 100% local, zero data leakage
- **Frontend:** Angular 17
- **Testing:** xUnit, FluentAssertions
- **Scheduler:** Quartz.NET

## Domain
GST (Goods and Services Tax) compliance automation for 
Indian MSMEs — specifically the GSTR-1/GSTR-2B reconciliation 
and ITC (Input Tax Credit) recovery pipeline.

## Running Locally
_Setup instructions coming as project progresses_