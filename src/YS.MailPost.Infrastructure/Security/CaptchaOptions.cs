namespace YS.MailPost.Infrastructure.Security;

public sealed class CaptchaOptions
{
    public const string SectionName = "Captcha";

    public string Provider { get; set; } = "none";
}
