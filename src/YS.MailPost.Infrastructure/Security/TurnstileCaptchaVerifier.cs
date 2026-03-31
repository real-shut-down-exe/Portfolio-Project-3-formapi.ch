using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using YS.MailPost.Application.Abstractions;

namespace YS.MailPost.Infrastructure.Security;

public sealed class TurnstileCaptchaVerifier : ICaptchaVerifier
{
    private readonly HttpClient _httpClient;
    private readonly TurnstileOptions _options;

    public TurnstileCaptchaVerifier(HttpClient httpClient, IOptions<TurnstileOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<CaptchaVerificationResult> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return new CaptchaVerificationResult(false, "Captcha secret is not configured.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return new CaptchaVerificationResult(false, "Captcha token is required.");
        }

        var response = await _httpClient.PostAsync(
            _options.VerifyEndpoint,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey,
                ["response"] = token,
                ["remoteip"] = remoteIp ?? string.Empty
            }),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new CaptchaVerificationResult(false, "Captcha verification failed.");
        }

        var payload = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
        if (payload is null || !payload.Success)
        {
            return new CaptchaVerificationResult(false, "Captcha verification failed.");
        }

        return new CaptchaVerificationResult(true, null);
    }

    private sealed class TurnstileResponse
    {
        public bool Success { get; set; }
    }
}
