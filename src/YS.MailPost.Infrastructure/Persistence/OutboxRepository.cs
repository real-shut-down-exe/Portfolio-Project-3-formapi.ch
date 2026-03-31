using Microsoft.EntityFrameworkCore;
using YS.MailPost.Application.Abstractions;
using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Infrastructure.Persistence;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly MailPostDbContext _dbContext;

    public OutboxRepository(MailPostDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        var entity = MapToEntity(email);
        _dbContext.OutboxEmails.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<OutboxEmail?> GetByIdempotencyKeyAsync(
        string formType,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OutboxEmails
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.FormType == formType && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<IReadOnlyList<OutboxEmail>> GetDueAsync(int batchSize, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.OutboxEmails
            .Where(x => x.Status == EmailStatus.Pending && x.NextAttemptAtUtc <= nowUtc)
            .OrderBy(x => x.NextAttemptAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task UpdateAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.OutboxEmails.FindAsync([email.Id], cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Status = email.Status;
        entity.AttemptCount = email.AttemptCount;
        entity.NextAttemptAtUtc = email.NextAttemptAtUtc;
        entity.SentAtUtc = email.SentAtUtc;
        entity.LastError = email.LastError;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OutboxEmailEntity MapToEntity(OutboxEmail email)
    {
        return new OutboxEmailEntity
        {
            Id = email.Id,
            FormType = email.FormType,
            To = email.To.Value,
            From = email.From.Value,
            ReplyTo = email.ReplyTo?.Value,
            Subject = email.Subject,
            HtmlBody = email.HtmlBody,
            IdempotencyKey = email.IdempotencyKey,
            Fingerprint = email.Fingerprint,
            CorrelationId = email.CorrelationId,
            Status = email.Status,
            AttemptCount = email.AttemptCount,
            NextAttemptAtUtc = email.NextAttemptAtUtc,
            CreatedAtUtc = email.CreatedAtUtc,
            SentAtUtc = email.SentAtUtc,
            LastError = email.LastError
        };
    }

    private static OutboxEmail MapToDomain(OutboxEmailEntity entity)
    {
        var email = new OutboxEmail(
            entity.Id,
            entity.FormType,
            EmailAddress.Create(entity.To),
            EmailAddress.Create(entity.From),
            string.IsNullOrWhiteSpace(entity.ReplyTo) ? null : EmailAddress.Create(entity.ReplyTo),
            entity.Subject,
            entity.HtmlBody,
            entity.IdempotencyKey,
            entity.Fingerprint,
            entity.CorrelationId,
            entity.CreatedAtUtc);

        email.ApplyState(
            entity.Status,
            entity.AttemptCount,
            entity.NextAttemptAtUtc,
            entity.SentAtUtc,
            entity.LastError);

        return email;
    }
}
