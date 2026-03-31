namespace YS.MailPost.Application.Abstractions;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templatePath, IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken);
}
