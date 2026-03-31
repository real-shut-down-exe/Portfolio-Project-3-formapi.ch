using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
