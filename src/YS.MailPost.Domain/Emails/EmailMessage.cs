namespace YS.MailPost.Domain.Emails;

public sealed record EmailMessage(
    EmailAddress To,
    EmailAddress From,
    EmailAddress? ReplyTo,
    string Subject,
    string HtmlBody);
