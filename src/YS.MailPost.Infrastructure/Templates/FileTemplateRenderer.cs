using System.Text;
using YS.MailPost.Application.Abstractions;

namespace YS.MailPost.Infrastructure.Templates;

public sealed class FileTemplateRenderer : ITemplateRenderer
{
    public async Task<string> RenderAsync(string templatePath, IReadOnlyDictionary<string, string> tokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return string.Empty;
        }

        var resolvedPath = Path.IsPathRooted(templatePath)
            ? templatePath
            : Path.Combine(AppContext.BaseDirectory, templatePath);

        var content = await File.ReadAllTextAsync(resolvedPath, Encoding.UTF8, cancellationToken);
        foreach (var (key, value) in tokens)
        {
            content = content.Replace($"{{{{{key}}}}}", value, StringComparison.OrdinalIgnoreCase);
        }

        return content;
    }
}
