using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using YS.MailPost.Application.Abstractions;
using YS.MailPost.Domain.Emails;

namespace YS.MailPost.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;

    public SmtpEmailSender(IOptions<SmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(MailboxAddress.Parse(_options.From));
        mimeMessage.To.Add(MailboxAddress.Parse(message.To.Value));

        if (message.ReplyTo is not null)
        {
            mimeMessage.ReplyTo.Add(MailboxAddress.Parse(message.ReplyTo.Value));
        }

        mimeMessage.Subject = message.Subject;
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOption = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.Host, _options.Port, socketOption, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            await client.AuthenticateAsync(_options.User, _options.Pass, cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
