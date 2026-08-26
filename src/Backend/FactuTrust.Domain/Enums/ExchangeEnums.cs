namespace FactuTrust.Domain.Enums;

public enum ExchangeThreadStatus
{
    Open = 0,
    Closed = 1
}

public enum ExchangeMessageVisibility
{
    ClientVisible = 0,
    InternalNote = 1
}

public enum ExchangeRequestCategory
{
    Reclamation = 0,
    Information = 1,
    DocumentManquant = 2,
    Autre = 3
}

public enum ExchangeRequestPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public enum ExchangeRequestStatus
{
    Open = 0,
    InProgress = 1,
    WaitingClient = 2,
    WaitingFirm = 3,
    Resolved = 4,
    Closed = 5
}

public enum ExchangeTaskStatus
{
    Todo = 0,
    Doing = 1,
    Done = 2,
    Cancelled = 3
}

public enum ExchangeAuditEventType
{
    ThreadOpened = 0,
    MessageSent = 1,
    InternalNoteAdded = 2,
    RequestCreated = 3,
    RequestStatusChanged = 4,
    TaskCreated = 5,
    TaskCompleted = 6,
    DocumentShared = 7,
    ThreadClosed = 8,
    ThreadReopened = 9,
    AppointmentSuggested = 10,
    TaskStatusChanged = 11,
    RequestCommented = 12,
    RequestAssigned = 13
}
