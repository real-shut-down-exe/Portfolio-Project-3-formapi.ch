using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YS.MailPost.Application.Abstractions;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Worker;

public sealed class OutboxWorker : BackgroundService
{
    private readonly ILogger<OutboxWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkerOptions _options;
    private readonly FormRoutingOptions _routingOptions;

    public OutboxWorker(
        ILogger<OutboxWorker> logger,
        IServiceScopeFactory scopeFactory,
        IOptions<WorkerOptions> options,
        FormRoutingOptions routingOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _routingOptions = routingOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var semaphore = new SemaphoreSlim(_options.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                using var scope = _scopeFactory.CreateScope();
                var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var batch = await outboxRepository.GetDueAsync(_options.BatchSize, now, stoppingToken);

                if (batch.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                var tasks = batch.Select(async email =>
                {
                    await semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        await ProcessEmailAsync(email, stoppingToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox worker loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task ProcessEmailAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var maxAttempts = ResolveMaxAttempts(email.FormType);
        var now = DateTimeOffset.UtcNow;

        email.IncrementAttempt();
        email.MarkSending(now);
        await outboxRepository.UpdateAsync(email, cancellationToken);

        try
        {
            var message = new EmailMessage(email.To, email.From, email.ReplyTo, email.Subject, email.HtmlBody);
            await emailSender.SendAsync(message, cancellationToken);

            email.MarkSent(DateTimeOffset.UtcNow);
            await outboxRepository.UpdateAsync(email, cancellationToken);

            _logger.LogInformation("Sent outbox email {EmailId} for {FormType}.", email.Id, email.FormType);
        }
        catch (Exception ex)
        {
            if (email.AttemptCount >= maxAttempts)
            {
                email.MarkDeadLetter(ex.Message);
            }
            else
            {
                var backoff = CalculateBackoff(email.AttemptCount);
                email.MarkFailed(DateTimeOffset.UtcNow.Add(backoff), ex.Message);
            }

            await outboxRepository.UpdateAsync(email, cancellationToken);
            _logger.LogError(ex, "Failed sending outbox email {EmailId}. Attempt {Attempt}/{MaxAttempts}.", email.Id, email.AttemptCount, maxAttempts);
        }
    }

    private int ResolveMaxAttempts(string formType)
    {
        return _routingOptions.Forms.TryGetValue(formType, out var rule)
            ? rule.MaxAttempts
            : _options.MaxAttempts;
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        var exponential = Math.Min(_options.MaxBackoffSeconds, _options.BaseBackoffSeconds * Math.Pow(2, attempt));
        var jitter = Random.Shared.NextDouble() * _options.JitterSeconds;
        return TimeSpan.FromSeconds(exponential + jitter);
    }
}

public sealed class WorkerOptions
{
    public const string SectionName = "Worker";

    public int BatchSize { get; set; } = 25;
    public int MaxConcurrency { get; set; } = 4;
    public int PollIntervalSeconds { get; set; } = 5;
    public int BaseBackoffSeconds { get; set; } = 5;
    public int MaxBackoffSeconds { get; set; } = 300;
    public int JitterSeconds { get; set; } = 3;
    public int MaxAttempts { get; set; } = 5;
}
