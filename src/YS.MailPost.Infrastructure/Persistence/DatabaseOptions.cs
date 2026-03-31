namespace YS.MailPost.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; set; } = "sqlite";
    public string ConnectionString { get; set; } = "Data Source=mailpost.db";
    public string? SqlitePath { get; set; }
}
