using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Infrastructure.Persistence;

public sealed class MailPostDbContext : DbContext
{
    public MailPostDbContext(DbContextOptions<MailPostDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxEmailEntity> OutboxEmails => Set<OutboxEmailEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

        var outbox = modelBuilder.Entity<OutboxEmailEntity>();
        outbox.HasKey(x => x.Id);
        outbox.Property(x => x.FormType).HasMaxLength(100).IsRequired();
        outbox.Property(x => x.To).HasMaxLength(320).IsRequired();
        outbox.Property(x => x.From).HasMaxLength(320).IsRequired();
        outbox.Property(x => x.ReplyTo).HasMaxLength(320);
        outbox.Property(x => x.Subject).HasMaxLength(400).IsRequired();
        outbox.Property(x => x.HtmlBody).IsRequired();
        outbox.Property(x => x.IdempotencyKey).HasMaxLength(200);
        outbox.Property(x => x.Fingerprint).HasMaxLength(128);
        outbox.Property(x => x.CorrelationId).HasMaxLength(100);
        outbox.Property(x => x.Status).HasConversion<int>();
        outbox.Property(x => x.NextAttemptAtUtc).HasConversion(dateTimeOffsetConverter);
        outbox.Property(x => x.CreatedAtUtc).HasConversion(dateTimeOffsetConverter);
        outbox.Property(x => x.SentAtUtc).HasConversion(nullableDateTimeOffsetConverter);
        outbox.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        outbox.HasIndex(x => new { x.FormType, x.IdempotencyKey }).IsUnique();
    }
}
