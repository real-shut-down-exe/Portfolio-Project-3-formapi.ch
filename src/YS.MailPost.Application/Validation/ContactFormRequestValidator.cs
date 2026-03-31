using FluentValidation;
using YS.MailPost.Contracts.Forms;

namespace YS.MailPost.Application.Validation;

public sealed class ContactFormRequestValidator : AbstractValidator<ContactFormRequest>
{
    public ContactFormRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.CaptchaToken).MaximumLength(4096);
        RuleFor(x => x.Honeypot).MaximumLength(200);
        RuleFor(x => x.ElapsedMilliseconds).GreaterThanOrEqualTo(0);
    }
}
