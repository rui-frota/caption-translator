namespace CaptionTranslator.Pipeline;

public sealed class TranscriptBuffer
{
    private string _text = string.Empty;

    public string Text => _text;

    public void Clear() => _text = string.Empty;

    public string GetNewSegment(string value)
    {
        var incoming = Normalize(value);
        if (incoming.Length == 0 || _text.Length == 0)
        {
            return incoming;
        }

        if (string.Equals(_text, incoming, StringComparison.OrdinalIgnoreCase)
            || _text.EndsWith(incoming, StringComparison.OrdinalIgnoreCase)
            || _text.Contains(incoming, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (incoming.StartsWith(_text, StringComparison.OrdinalIgnoreCase))
        {
            return incoming[_text.Length..].TrimStart();
        }

        var existingWords = _text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var incomingWords = incoming.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = FindOverlap(existingWords, incomingWords);
        return string.Join(' ', incomingWords.Skip(overlap));
    }

    public string Append(string value)
    {
        var incoming = Normalize(value);
        if (incoming.Length == 0)
        {
            return _text;
        }

        if (_text.Length == 0)
        {
            _text = incoming;
            return _text;
        }

        if (string.Equals(_text, incoming, StringComparison.OrdinalIgnoreCase)
            || _text.EndsWith(incoming, StringComparison.OrdinalIgnoreCase)
            || _text.Contains(incoming, StringComparison.OrdinalIgnoreCase))
        {
            return _text;
        }

        if (incoming.StartsWith(_text, StringComparison.OrdinalIgnoreCase))
        {
            _text = incoming;
            return _text;
        }

        var existingWords = _text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var incomingWords = incoming.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var overlap = FindOverlap(existingWords, incomingWords);
        var suffix = string.Join(' ', incomingWords.Skip(overlap));
        if (suffix.Length > 0)
        {
            _text = $"{_text} {suffix}";
        }

        return _text;
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int FindOverlap(string[] existingWords, string[] incomingWords)
    {
        var maximum = Math.Min(existingWords.Length, incomingWords.Length);
        for (var length = maximum; length >= 1; length--)
        {
            var matches = true;
            for (var index = 0; index < length; index++)
            {
                var existingWord = existingWords[existingWords.Length - length + index];
                if (!string.Equals(existingWord, incomingWords[index], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return length;
            }
        }

        return 0;
    }
}