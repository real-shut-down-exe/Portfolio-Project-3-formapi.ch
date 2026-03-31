using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Application.Abstractions;

public interface IOutboxRepository
{
    Task AddAsync(OutboxEmail email, CancellationToken cancellationToken);
    Task<OutboxEmail?> GetByIdempotencyKeyAsync(string formType, string idempotencyKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxEmail>> GetDueAsync(int batchSize, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task UpdateAsync(OutboxEmail email, CancellationToken cancellationToken);
}
