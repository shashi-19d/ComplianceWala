namespace ComplianceWala.Domain.Enums;

public enum SessionStatus
{
    AwaitingUploads = 1,
    ReadyForReconciliation = 2,
    Reconciling = 3,
    Completed = 4,
    Failed = 5
}