namespace YS.MailPost.Application.Configuration;

public sealed class FormRoutingOptions
{
    public IDictionary<string, FormRoutingRule> Forms { get; set; } = new Dictionary<string, FormRoutingRule>(StringComparer.OrdinalIgnoreCase);
}

public sealed class FormRoutingRule
{
    public string To { get; set; } = string.Empty;
    public string SubjectPrefix { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public bool RequireCaptcha { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 5;
    public int RateLimitPerHour { get; set; } = 30;
    public int MinimumSubmissionMilliseconds { get; set; } = 1200;
    public int MaxAttempts { get; set; } = 5;
}
