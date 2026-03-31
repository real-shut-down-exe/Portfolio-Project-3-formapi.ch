using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Application.UseCases.EnqueueFormSubmission;
using YS.MailPost.Contracts.Forms;

namespace YS.MailPost.Api.Controllers;

[ApiController]
[Route("api/forms")]
public sealed class FormsController : ControllerBase
{
    private readonly EnqueueFormSubmission _enqueueFormSubmission;
    private readonly IValidator<ContactFormRequest> _contactValidator;
    private readonly IValidator<ContactDetailsFormRequest> _contactDetailsValidator;
    private readonly FormRoutingOptions _routingOptions;
    private readonly ILogger<FormsController> _logger;
    private readonly string _fromAddress;

    public FormsController(
        EnqueueFormSubmission enqueueFormSubmission,
        IValidator<ContactFormRequest> contactValidator,
        IValidator<ContactDetailsFormRequest> contactDetailsValidator,
        FormRoutingOptions routingOptions,
        ILogger<FormsController> logger,
        Microsoft.Extensions.Options.IOptions<YS.MailPost.Infrastructure.Email.SmtpOptions> smtpOptions)
    {
        _enqueueFormSubmission = enqueueFormSubmission;
        _contactValidator = contactValidator;
        _contactDetailsValidator = contactDetailsValidator;
        _routingOptions = routingOptions;
        _logger = logger;
        _fromAddress = smtpOptions.Value.From;
    }

    [HttpPost("{formType}")]
    [EnableRateLimiting("form-submissions")]
    public async Task<IActionResult> SubmitAsync([FromRoute] FormType formType, [FromBody] ContactFormRequest request, CancellationToken cancellationToken)
    {
        if (!_routingOptions.Forms.ContainsKey(formType.ToString()))
        {
            return NotFound(new { error = "Form type not configured." });
        }

        var validationResult = await _contactValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            _logger.LogWarning("Honeypot triggered for form {FormType}.", formType);
            return Accepted(new { id = Guid.NewGuid(), suppressed = true });
        }

        var routingRule = _routingOptions.Forms[formType.ToString()];
        if (request.ElapsedMilliseconds.HasValue && request.ElapsedMilliseconds.Value < routingRule.MinimumSubmissionMilliseconds)
        {
            return BadRequest(new { error = "Submission too fast." });
        }

        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var idempotency)
            ? idempotency.ToString()
            : null;

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var fingerprint = EnqueueFormSubmission.ComputeFingerprint(remoteIp, userAgent, $"{request.Name}|{request.Email}|{request.Message}");
        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var correlationValue)
            ? correlationValue?.ToString()
            : HttpContext.TraceIdentifier;

        var result = await _enqueueFormSubmission.HandleAsync(
            new EnqueueFormSubmissionRequest(
                formType,
                request.Name,
                request.Email,
                request.Message,
                request.Subject,
                Company: null,
                Phone: null,
                AcceptPrivacyPolicy: null,
                request.CaptchaToken,
                request.Honeypot,
                remoteIp,
                userAgent,
                idempotencyKey,
                FromAddress: _fromAddress,
                Fingerprint: fingerprint,
                NowUtc: DateTimeOffset.UtcNow),
            correlationId,
            cancellationToken);

        if (!result.Accepted)
        {
            return BadRequest(new { error = result.Error });
        }

        return Accepted(new { id = result.Id, deduplicated = result.Deduplicated });
    }

    [HttpPost("contact-details")]
    [EnableRateLimiting("contact-details-submissions")]
    public async Task<IActionResult> SubmitContactDetailsAsync([FromBody] ContactDetailsFormRequest request, CancellationToken cancellationToken)
    {
        var formType = FormType.ContactDetailsForm;
        if (!_routingOptions.Forms.ContainsKey(formType.ToString()))
        {
            return NotFound(new { error = "Form type not configured." });
        }

        var validationResult = await _contactDetailsValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            _logger.LogWarning("Honeypot triggered for form {FormType}.", formType);
            return Accepted(new { id = Guid.NewGuid(), suppressed = true });
        }

        var routingRule = _routingOptions.Forms[formType.ToString()];
        if (request.ElapsedMilliseconds.HasValue && request.ElapsedMilliseconds.Value < routingRule.MinimumSubmissionMilliseconds)
        {
            return BadRequest(new { error = "Submission too fast." });
        }

        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var idempotency)
            ? idempotency.ToString()
            : null;

        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var fingerprint = EnqueueFormSubmission.ComputeFingerprint(
            remoteIp,
            userAgent,
            $"{request.Name}|{request.Email}|{request.Subject}|{request.Message}");
        var correlationId = HttpContext.Items.TryGetValue("CorrelationId", out var correlationValue)
            ? correlationValue?.ToString()
            : HttpContext.TraceIdentifier;

        var result = await _enqueueFormSubmission.HandleAsync(
            new EnqueueFormSubmissionRequest(
                formType,
                request.Name,
                request.Email,
                request.Message,
                request.Subject,
                request.Company,
                request.Phone,
                request.AcceptPrivacyPolicy,
                request.CaptchaToken,
                request.Honeypot,
                remoteIp,
                userAgent,
                idempotencyKey,
                FromAddress: _fromAddress,
                Fingerprint: fingerprint,
                NowUtc: DateTimeOffset.UtcNow),
            correlationId,
            cancellationToken);

        if (!result.Accepted)
        {
            return BadRequest(new { error = result.Error });
        }

        return Accepted(new { id = result.Id, deduplicated = result.Deduplicated });
    }
}
