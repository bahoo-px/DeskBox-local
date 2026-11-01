using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DeskBox.Models;

namespace DeskBox.Services;

internal sealed record MarkdownInlineImageExtractionResult(
    string Body,
    IReadOnlyList<TodoAttachment> Attachments);

internal static partial class MarkdownInlineImageExtractor
{
    private const int MaxImageBytes = 20 * 1024 * 1024;
    private const int MaxTotalBytes = 50 * 1024 * 1024;

    public static async Task<MarkdownInlineImageExtractionResult> ExtractAsync(
        string? markdown,
        string managedDirectory,
        CancellationToken cancellationToken = default)
    {
        string source = markdown ?? string.Empty;
        MatchCollection matches = InlineDataImageRegex().Matches(source);
        if (matches.Count == 0)
        {
            return new MarkdownInlineImageExtractionResult(source, []);
        }

        var output = new StringBuilder(source.Length);
        var attachments = new List<TodoAttachment>();
        var attachmentsByHash = new Dictionary<string, TodoAttachment>(StringComparer.Ordinal);
        int sourceOffset = 0;
        int totalBytes = 0;
        int imageIndex = 0;

        foreach (Match match in matches)
        {
            Group urlGroup = match.Groups["url"];
            string payload = WhitespaceRegex().Replace(match.Groups["payload"].Value, string.Empty);
            if (!TryDecode(payload, totalBytes, out byte[] bytes))
            {
                continue;
            }

            string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!attachmentsByHash.TryGetValue(hash, out TodoAttachment? attachment))
            {
                string extension = GetExtension(match.Groups["subtype"].Value);
                string fileName = $"inline-{++imageIndex:00}-{hash[..12]}{extension}";
                try
                {
                    await using var stream = new MemoryStream(bytes, writable: false);
                    attachment = await AttachmentStorageService.SaveStreamAsync(
                        stream,
                        fileName,
                        managedDirectory,
                        cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    App.Log($"[QuickCapture] Failed to extract inline Markdown image: {ex.Message}");
                    continue;
                }

                if (attachment is null)
                {
                    continue;
                }

                attachmentsByHash[hash] = attachment;
                attachments.Add(attachment);
                totalBytes += bytes.Length;
            }

            output.Append(source, sourceOffset, urlGroup.Index - sourceOffset);
            output.Append("attachment:");
            output.Append(attachment.Id);
            sourceOffset = urlGroup.Index + urlGroup.Length;
        }

        if (attachments.Count == 0)
        {
            return new MarkdownInlineImageExtractionResult(source, []);
        }

        output.Append(source, sourceOffset, source.Length - sourceOffset);
        return new MarkdownInlineImageExtractionResult(output.ToString(), attachments);
    }

    private static bool TryDecode(string payload, int totalBytes, out byte[] bytes)
    {
        bytes = [];
        if (payload.Length == 0 || payload.Length > ((MaxImageBytes + 2L) / 3L) * 4L)
        {
            return false;
        }

        try
        {
            byte[] decoded = Convert.FromBase64String(payload);
            if (decoded.Length == 0 ||
                decoded.Length > MaxImageBytes ||
                totalBytes + decoded.Length > MaxTotalBytes)
            {
                return false;
            }

            bytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string GetExtension(string subtype) => subtype.ToLowerInvariant() switch
    {
        "jpeg" or "jpg" => ".jpg",
        "gif" => ".gif",
        "webp" => ".webp",
        "bmp" => ".bmp",
        _ => ".png"
    };

    [GeneratedRegex(
        "!\\[[^\\]\\r\\n]*\\]\\(\\s*(?<url>data:image/(?<subtype>png|jpe?g|gif|webp|bmp);base64,(?<payload>[A-Za-z0-9+/=\\t\\r\\n ]+))(?=\\s*(?:\\)|[\\\"']))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InlineDataImageRegex();

    [GeneratedRegex(@"\s", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
