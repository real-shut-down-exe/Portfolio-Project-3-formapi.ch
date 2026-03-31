using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Infrastructure.Persistence;

public sealed class OutboxEmailEntity
{
    public Guid Id { get; set; }
    public string FormType { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? IdempotencyKey { get; set; }
    public string? Fingerprint { get; set; }
    public string? CorrelationId { get; set; }
    public EmailStatus Status { get; set; } = EmailStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset NextAttemptAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
