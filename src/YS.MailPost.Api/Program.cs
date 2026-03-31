using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Context;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using YS.MailPost.Api.RateLimiting;
using YS.MailPost.Application;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Contracts.Forms;
using YS.MailPost.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var routingOptions = new FormRoutingOptions();
builder.Configuration.GetSection("FormRouting").Bind(routingOptions);

builder.Services.AddOptions<FormRoutingOptions>()
    .Bind(builder.Configuration.GetSection("FormRouting"))
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FormRoutingOptions>>().Value);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        return ValueTask.CompletedTask;
    };

    static FormRoutingRule ResolveRule(HttpContext context, FormRoutingOptions options)
    {
        var formType = context.Request.RouteValues["formType"]?.ToString() ?? "Unknown";
        return options.Forms.TryGetValue(formType, out var configuredRule)
            ? configuredRule
            : new FormRoutingRule();
    }

    static string BuildPartitionKey(HttpContext context, string bucket, string? formTypeOverride = null)
    {
        var formType = formTypeOverride ?? context.Request.RouteValues["formType"]?.ToString() ?? "Unknown";
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var userAgentHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(userAgent)));
        return $"{ip}:{formType}:{userAgentHash}:{bucket}";
    }

    options.AddPolicy<string>("form-submissions", context =>
    {
        var rule = ResolveRule(context, routingOptions);
        var partitionKey = BuildPartitionKey(context, "form-submissions");

        return RateLimitPartition.Get(partitionKey, _ => new CompositeRateLimiter(new RateLimiter[]
        {
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }),
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.RateLimitPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })
        }));
    });

    options.AddPolicy<string>("contact-details-submissions", context =>
    {
        var formType = FormType.ContactDetailsForm.ToString();
        var rule = routingOptions.Forms.TryGetValue(formType, out var configuredRule)
            ? configuredRule
            : new FormRoutingRule();
        var partitionKey = BuildPartitionKey(context, "form-submissions", formType);

        return RateLimitPartition.Get(partitionKey, _ => new CompositeRateLimiter(new RateLimiter[]
        {
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.RateLimitPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }),
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = rule.RateLimitPerHour,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                AutoReplenishment = true
            })
        }));
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
        ? headerValue.ToString()
        : Guid.NewGuid().ToString("N");

    context.TraceIdentifier = correlationId;
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        await next();
    }
});

app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
