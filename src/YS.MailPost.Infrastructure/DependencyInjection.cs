using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YS.MailPost.Application.Abstractions;
using YS.MailPost.Infrastructure.Email;
using YS.MailPost.Infrastructure.Persistence;
using YS.MailPost.Infrastructure.Security;
using YS.MailPost.Infrastructure.Templates;

namespace YS.MailPost.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<TurnstileOptions>()
            .Bind(configuration.GetSection(TurnstileOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateOnStart();
        services.AddOptions<CaptchaOptions>()
            .Bind(configuration.GetSection(CaptchaOptions.SectionName))
            .ValidateOnStart();

        services.AddDbContext<MailPostDbContext>((sp, options) =>
        {
            var databaseOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
            if (databaseOptions.Provider.Equals("postgres", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(databaseOptions.ConnectionString);
            }
            else
            {
                options.UseSqlite(ResolveSqliteConnectionString(databaseOptions));
            }
        });

        services.AddHostedService<DatabaseInitializerHostedService>();

        services.AddScoped<IOutboxRepository, OutboxRepository>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ITemplateRenderer, FileTemplateRenderer>();

        var captchaOptions = new CaptchaOptions();
        configuration.GetSection(CaptchaOptions.SectionName).Bind(captchaOptions);
        if (captchaOptions.Provider.Equals("turnstile", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ICaptchaVerifier, TurnstileCaptchaVerifier>();
        }
        else
        {
            services.AddSingleton<ICaptchaVerifier, NoCaptchaVerifier>();
        }

        return services;
    }

    private static string ResolveSqliteConnectionString(DatabaseOptions databaseOptions)
    {
        var builder = new SqliteConnectionStringBuilder(databaseOptions.ConnectionString);
        if (!string.IsNullOrWhiteSpace(databaseOptions.SqlitePath))
        {
            builder.DataSource = Path.GetFullPath(databaseOptions.SqlitePath);
        }
        else if (!string.IsNullOrWhiteSpace(builder.DataSource) && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(builder.DataSource);
        }

        return builder.ToString();
    }
}
