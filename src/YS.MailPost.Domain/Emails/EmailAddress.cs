using System.Net.Mail;

namespace YS.MailPost.Domain.Emails;

public sealed record EmailAddress
{
    public EmailAddress(string value)
    {
        Value = Normalize(value);
    }

    public string Value { get; }

    public static EmailAddress Create(string value) => new(value);

    private static string Normalize(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        _ = new MailAddress(trimmed);
        return trimmed;
    }
}
