namespace YS.MailPost.Domain.Emails;

public sealed class OutboxEmail
{
    public OutboxEmail(
        Guid id,
        string formType,
        EmailAddress to,
        EmailAddress from,
        EmailAddress? replyTo,
        string subject,
        string htmlBody,
        string? idempotencyKey,
        string? fingerprint,
        string? correlationId,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        FormType = formType;
        To = to;
        From = from;
        ReplyTo = replyTo;
        Subject = subject;
        HtmlBody = htmlBody;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
        CorrelationId = correlationId;
        CreatedAtUtc = createdAtUtc;
        Status = EmailStatus.Pending;
        AttemptCount = 0;
        NextAttemptAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string FormType { get; }
    public EmailAddress To { get; }
    public EmailAddress From { get; }
    public EmailAddress? ReplyTo { get; }
    public string Subject { get; }
    public string HtmlBody { get; }
    public string? IdempotencyKey { get; }
    public string? Fingerprint { get; }
    public string? CorrelationId { get; }

    public EmailStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset? SentAtUtc { get; private set; }
    public string? LastError { get; private set; }

    public void MarkSending(DateTimeOffset nowUtc)
    {
        Status = EmailStatus.Sending;
        LastError = null;
        NextAttemptAtUtc = nowUtc;
    }

    public void MarkSent(DateTimeOffset sentAtUtc)
    {
        Status = EmailStatus.Sent;
        SentAtUtc = sentAtUtc;
        LastError = null;
    }

    public void MarkFailed(DateTimeOffset nextAttemptAtUtc, string lastError)
    {
        Status = EmailStatus.Pending;
        LastError = lastError;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    public void MarkDeadLetter(string lastError)
    {
        Status = EmailStatus.Failed;
        LastError = lastError;
    }

    public void IncrementAttempt() => AttemptCount++;

    public void ApplyState(
        EmailStatus status,
        int attemptCount,
        DateTimeOffset nextAttemptAtUtc,
        DateTimeOffset? sentAtUtc,
        string? lastError)
    {
        Status = status;
        AttemptCount = attemptCount;
        NextAttemptAtUtc = nextAttemptAtUtc;
        SentAtUtc = sentAtUtc;
        LastError = lastError;
    }
}
