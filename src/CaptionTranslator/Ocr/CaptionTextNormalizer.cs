using System.Text.RegularExpressions;

namespace CaptionTranslator.Ocr;

public static partial class CaptionTextNormalizer
{
    public static string Normalize(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}