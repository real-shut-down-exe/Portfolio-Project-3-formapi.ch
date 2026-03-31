using YS.MailPost.Application.Abstractions;

namespace YS.MailPost.Infrastructure.Security;

public sealed class NoCaptchaVerifier : ICaptchaVerifier
{
    public Task<CaptchaVerificationResult> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CaptchaVerificationResult(true, null));
    }
}
