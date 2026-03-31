namespace YS.MailPost.Application.Abstractions;

public interface ICaptchaVerifier
{
    Task<CaptchaVerificationResult> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken);
}

public sealed record CaptchaVerificationResult(bool Success, string? ErrorMessage);
