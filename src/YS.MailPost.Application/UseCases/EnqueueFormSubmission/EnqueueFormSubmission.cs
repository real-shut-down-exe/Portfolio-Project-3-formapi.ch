using System.Net;
using System.Security.Cryptography;
using System.Text;
using YS.MailPost.Application.Abstractions;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Contracts.Forms;
using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Application.UseCases.EnqueueFormSubmission;

public sealed class EnqueueFormSubmission
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ICaptchaVerifier _captchaVerifier;
    private readonly FormRoutingOptions _routingOptions;

    public EnqueueFormSubmission(
        IOutboxRepository outboxRepository,
        ITemplateRenderer templateRenderer,
        ICaptchaVerifier captchaVerifier,
        FormRoutingOptions routingOptions)
    {
        _outboxRepository = outboxRepository;
        _templateRenderer = templateRenderer;
        _captchaVerifier = captchaVerifier;
        _routingOptions = routingOptions;
    }

    public async Task<EnqueueFormSubmissionResult> HandleAsync(
        EnqueueFormSubmissionRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (!_routingOptions.Forms.TryGetValue(request.FormType.ToString(), out var routingRule))
        {
            return EnqueueFormSubmissionResult.CreateNotFound($"Form type '{request.FormType}' is not configured.");
        }

        if (routingRule.RequireCaptcha)
        {
            var captchaResult = await _captchaVerifier.VerifyAsync(request.CaptchaToken, request.RemoteIp, cancellationToken);
            if (!captchaResult.Success)
            {
                return EnqueueFormSubmissionResult.CreateRejected(captchaResult.ErrorMessage ?? "Captcha verification failed.");
            }
        }

        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _outboxRepository.GetByIdempotencyKeyAsync(
                request.FormType.ToString(),
                idempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                return EnqueueFormSubmissionResult.CreateDeduplicated(existing.Id);
            }
        }

        var replyTo = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : EmailAddress.Create(SanitizeHeaderValue(request.Email));

        var subject = $"{routingRule.SubjectPrefix} {SanitizeHeaderValue(request.Subject ?? request.FormType.ToString())}".Trim();
        var htmlBody = await _templateRenderer.RenderAsync(routingRule.Template, BuildTokens(request), cancellationToken);

        var outboxEmail = new OutboxEmail(
            Guid.NewGuid(),
            request.FormType.ToString(),
            EmailAddress.Create(routingRule.To),
            EmailAddress.Create(request.FromAddress),
            replyTo,
            subject,
            htmlBody,
            idempotencyKey,
            request.Fingerprint,
            correlationId,
            request.NowUtc);

        await _outboxRepository.AddAsync(outboxEmail, cancellationToken);
        return EnqueueFormSubmissionResult.CreateAccepted(outboxEmail.Id);
    }

    private static IReadOnlyDictionary<string, string> BuildTokens(EnqueueFormSubmissionRequest request)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["form_type"] = request.FormType.ToString(),
            ["name"] = HtmlEncode(request.Name),
            ["email"] = HtmlEncode(request.Email),
            ["company"] = HtmlEncode(request.Company),
            ["phone"] = HtmlEncode(request.Phone),
            ["subject"] = HtmlEncode(request.Subject),
            ["message"] = HtmlEncode(request.Message),
            ["privacy_policy_accepted"] = request.AcceptPrivacyPolicy.HasValue
                ? (request.AcceptPrivacyPolicy.Value ? "Yes" : "No")
                : string.Empty,
            ["submitted_at"] = request.NowUtc.ToString("u"),
            ["ip_address"] = HtmlEncode(request.RemoteIp ?? string.Empty),
            ["user_agent"] = HtmlEncode(request.UserAgent ?? string.Empty)
        };

        return tokens;
    }

    private static string HtmlEncode(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string SanitizeHeaderValue(string value)
    {
        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    public static string ComputeFingerprint(string? remoteIp, string? userAgent, string? normalizedFields)
    {
        var input = $"{remoteIp}|{userAgent}|{normalizedFields}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}

public sealed record EnqueueFormSubmissionRequest(
    FormType FormType,
    string Name,
    string Email,
    string Message,
    string? Subject,
    string? Company,
    string? Phone,
    bool? AcceptPrivacyPolicy,
    string? CaptchaToken,
    string? Honeypot,
    string? RemoteIp,
    string? UserAgent,
    string? IdempotencyKey,
    string FromAddress,
    string? Fingerprint,
    DateTimeOffset NowUtc);

public sealed record EnqueueFormSubmissionResult
{
    private EnqueueFormSubmissionResult(bool accepted, Guid? id, string? error, bool deduplicated)
    {
        Accepted = accepted;
        Id = id;
        Error = error;
        Deduplicated = deduplicated;
    }

    public bool Accepted { get; }
    public Guid? Id { get; }
    public string? Error { get; }
    public bool Deduplicated { get; }

    public static EnqueueFormSubmissionResult CreateAccepted(Guid id) => new(true, id, null, false);
    public static EnqueueFormSubmissionResult CreateDeduplicated(Guid id) => new(true, id, null, true);
    public static EnqueueFormSubmissionResult CreateRejected(string error) => new(false, null, error, false);
    public static EnqueueFormSubmissionResult CreateNotFound(string error) => new(false, null, error, false);
}
