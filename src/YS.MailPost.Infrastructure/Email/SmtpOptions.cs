namespace YS.MailPost.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Pass { get; set; }
    public bool UseSsl { get; set; }
    public bool UseStartTls { get; set; } = true;
    public string From { get; set; } = string.Empty;
}
