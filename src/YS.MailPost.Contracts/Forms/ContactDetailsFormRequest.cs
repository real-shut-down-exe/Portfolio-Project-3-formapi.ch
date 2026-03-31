namespace YS.MailPost.Contracts.Forms;

public sealed class ContactDetailsFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool AcceptPrivacyPolicy { get; set; }
    public string? CaptchaToken { get; set; }
    public string? Honeypot { get; set; }
    public long? ElapsedMilliseconds { get; set; }
}
