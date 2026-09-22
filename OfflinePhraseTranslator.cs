using System.Text.RegularExpressions;

namespace CaptionTranslator.Translation;

public sealed class OfflinePhraseTranslator
{
    public const string UnavailableMessage = "Traducao offline indisponivel para esta legenda.";
    public const string PartialMessage = "Traducao parcial offline: ";

    private static readonly (string English, string Portuguese)[] Phrases =
    [
        ("this is an old prison machine that they would use to make plywood and", "esta e uma maquina de prisao antiga que eles usariam para fazer compensado e"),
        ("peel the plastic off the back and you have your completed", "retire o plastico da parte de tras e voce tem seu produto completo"),
        ("how are you", "como voce esta"),
        ("what are you doing", "o que voce esta fazendo"),
        ("what's going on", "o que esta acontecendo"),
        ("what is going on", "o que esta acontecendo"),
        ("i don't know", "eu nao sei"),
        ("i do not know", "eu nao sei"),
        ("i'm sorry", "sinto muito"),
        ("thank you", "obrigado"),
        ("see you later", "ate mais tarde"),
        ("good morning", "bom dia"),
        ("good night", "boa noite"),
        ("please wait", "por favor, aguarde"),
        ("come on", "vamos"),
        ("let's go", "vamos")
    ];

    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hello"] = "ola",
        ["hi"] = "oi",
        ["the"] = "",
        ["a"] = "uma",
        ["an"] = "uma",
        ["and"] = "e",
        ["or"] = "ou",
        ["but"] = "mas",
        ["to"] = "para",
        ["of"] = "de",
        ["in"] = "em",
        ["on"] = "em",
        ["for"] = "para",
        ["with"] = "com",
        ["from"] = "de",
        ["about"] = "sobre",
        ["not"] = "nao",
        ["yes"] = "sim",
        ["no"] = "nao",
        ["please"] = "por favor",
        ["thanks"] = "obrigado",
        ["sorry"] = "desculpe",
        ["welcome"] = "bem-vindo",
        ["old"] = "antigo",
        ["prison"] = "prisao",
        ["machine"] = "maquina",
        ["peel"] = "retire",
        ["plastic"] = "plastico",
        ["off"] = "de",
        ["back"] = "tras",
        ["completed"] = "completo",
        ["would"] = "iria",
        ["use"] = "usar",
        ["make"] = "fazer",
        ["plywood"] = "compensado",
        ["today"] = "hoje",
        ["tomorrow"] = "amanha",
        ["yesterday"] = "ontem",
        ["now"] = "agora",
        ["later"] = "mais tarde",
        ["again"] = "novamente",
        ["here"] = "aqui",
        ["there"] = "la",
        ["what"] = "o que",
        ["where"] = "onde",
        ["when"] = "quando",
        ["why"] = "por que",
        ["who"] = "quem",
        ["how"] = "como",
        ["this"] = "isto",
        ["that"] = "isso",
        ["you"] = "voce",
        ["your"] = "seu",
        ["i"] = "eu",
        ["we"] = "nos",
        ["they"] = "eles",
        ["he"] = "ele",
        ["she"] = "ela",
        ["it"] = "isso",
        ["is"] = "e",
        ["are"] = "estao",
        ["am"] = "estou",
        ["was"] = "era",
        ["were"] = "eram",
        ["have"] = "tenho",
        ["has"] = "tem",
        ["can"] = "pode",
        ["cannot"] = "nao pode",
        ["do"] = "fazer",
        ["does"] = "faz",
        ["go"] = "ir",
        ["come"] = "vir",
        ["see"] = "ver",
        ["look"] = "olhe",
        ["know"] = "saber",
        ["think"] = "achar",
        ["say"] = "dizer",
        ["said"] = "disse",
        ["tell"] = "contar",
        ["get"] = "obter",
        ["got"] = "teve",
        ["put"] = "colocar",
        ["take"] = "pegar",
        ["show"] = "mostrar",
        ["happen"] = "acontecer",
        ["happening"] = "acontecendo",
        ["going"] = "indo",
        ["feel"] = "sentir",
        ["hear"] = "ouvir",
        ["watch"] = "assistir",
        ["want"] = "querer",
        ["need"] = "precisar",
        ["like"] = "gostar",
        ["love"] = "amar",
        ["help"] = "ajudar",
        ["stop"] = "pare",
        ["start"] = "iniciar",
        ["wait"] = "aguarde",
        ["okay"] = "certo",
        ["good"] = "bom",
        ["bad"] = "ruim",
        ["great"] = "otimo",
        ["really"] = "realmente",
        ["very"] = "muito",
        ["time"] = "tempo",
        ["way"] = "jeito",
        ["thing"] = "coisa",
        ["people"] = "pessoas",
        ["friend"] = "amigo",
        ["world"] = "mundo",
        ["home"] = "casa",
        ["work"] = "trabalho"
    };

    public string Translate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var translated = Regex.Replace(text.Trim(), @"\s+", " ");
        var remainingEnglish = translated;
        var phraseReplacements = new List<string>();
        for (var phraseIndex = 0; phraseIndex < Phrases.Length; phraseIndex++)
        {
            var (english, portuguese) = Phrases[phraseIndex];
            var pattern = $"\\b{Regex.Escape(english)}\\b";
            var remainingAfterMatch = Regex.Replace(
                remainingEnglish,
                pattern,
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!string.Equals(remainingEnglish, remainingAfterMatch, StringComparison.Ordinal))
            {
                remainingEnglish = remainingAfterMatch;
            }

            translated = Regex.Replace(
                translated,
                pattern,
                _ => $"__OFFLINE_PHRASE_{phraseIndex}__",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            phraseReplacements.Add(portuguese);
        }

        var hasUnknownWord = false;
        var knownWordCount = 0;
        Regex.Replace(
            remainingEnglish,
            @"\b[A-Za-z]+(?:'[A-Za-z]+)?\b",
            match =>
            {
                if (Words.TryGetValue(match.Value, out var word))
                {
                    if (!string.IsNullOrWhiteSpace(word))
                    {
                        knownWordCount++;
                    }

                    return word;
                }

                hasUnknownWord = true;
                return string.Empty;
            },
            RegexOptions.CultureInvariant);

        if (hasUnknownWord && knownWordCount == 0)
        {
            return UnavailableMessage;
        }

        var result = Regex.Replace(
            translated,
            @"\b[A-Za-z]+(?:'[A-Za-z]+)?\b",
            match => Words.TryGetValue(match.Value, out var word) ? word : string.Empty,
            RegexOptions.CultureInvariant);

        for (var phraseIndex = 0; phraseIndex < phraseReplacements.Count; phraseIndex++)
        {
            result = result.Replace($"__OFFLINE_PHRASE_{phraseIndex}__", phraseReplacements[phraseIndex], StringComparison.Ordinal);
        }

        result = Regex.Replace(result, @"\s+", " ").Trim();
        return hasUnknownWord ? PartialMessage + result : result;
    }
}