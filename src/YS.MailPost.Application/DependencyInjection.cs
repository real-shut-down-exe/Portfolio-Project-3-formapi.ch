using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using YS.MailPost.Application.Configuration;
using YS.MailPost.Application.UseCases.EnqueueFormSubmission;
using YS.MailPost.Application.Validation;

namespace YS.MailPost.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<EnqueueFormSubmission>();
        services.AddValidatorsFromAssemblyContaining<ContactFormRequestValidator>();
        return services;
    }
}
