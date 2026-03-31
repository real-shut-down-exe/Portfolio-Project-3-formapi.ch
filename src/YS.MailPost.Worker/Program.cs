using Serilog;
using Microsoft.Extensions.Options;
using YS.MailPost.Application;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Infrastructure;
using YS.MailPost.Worker;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddSerilog();

var routingOptions = new FormRoutingOptions();
builder.Configuration.GetSection("FormRouting").Bind(routingOptions);

builder.Services.AddOptions<FormRoutingOptions>()
    .Bind(builder.Configuration.GetSection("FormRouting"))
    .ValidateOnStart();
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FormRoutingOptions>>().Value);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<WorkerOptions>()
    .Bind(builder.Configuration.GetSection(WorkerOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddHostedService<OutboxWorker>();

var host = builder.Build();
host.Run();
