namespace YS.MailPost.Contracts.Forms;

public sealed class ContactFormRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? CaptchaToken { get; set; }
    public string? Honeypot { get; set; }
    public long? ElapsedMilliseconds { get; set; }
}
