using CaptionTranslator.Translation;
using Xunit;

namespace CaptionTranslator.Tests;

public sealed class OfflinePhraseTranslatorTests
{
    [Fact]
    public void Translate_UsesKnownPhraseAndPreservesPunctuation()
    {
        var translator = new OfflinePhraseTranslator();

        Assert.Equal("como voce esta?", translator.Translate("How are you?"));
    }

    [Fact]
    public void Translate_UsesKnownWordsAndLeavesUnknownWordsVisible()
    {
        var translator = new OfflinePhraseTranslator();

        Assert.Equal("ola mundo", translator.Translate("Hello world"));
    }

    [Fact]
    public void Translate_UsesSentencePatternForNaturalWordOrder()
    {
        var translator = new OfflinePhraseTranslator();

        var result = translator.Translate("This is an old prison machine that they would use to make plywood and");

        Assert.Equal("esta e uma maquina de prisao antiga que eles usariam para fazer compensado e", result);
    }

    [Fact]
    public void Translate_DoesNotMixLanguagesInAKnownSentence()
    {
        var translator = new OfflinePhraseTranslator();

        var result = translator.Translate("Peel the plastic off the back and you have your completed");

        Assert.Equal("retire o plastico da parte de tras e voce tem seu produto completo", result);
    }

    [Fact]
    public void Translate_NormalizesOcrLineBreaksBeforeMatchingSentence()
    {
        var translator = new OfflinePhraseTranslator();

        var result = translator.Translate("Peel the plastic off the back\nand you have your completed");

        Assert.Equal("retire o plastico da parte de tras e voce tem seu produto completo", result);
    }

    [Fact]
    public void Translate_DoesNotReturnMixedLanguagesForUnknownSentence()
    {
        var translator = new OfflinePhraseTranslator();

        Assert.Equal(
            OfflinePhraseTranslator.UnavailableMessage,
            translator.Translate("The museum closes at five o'clock"));
    }

    [Fact]
    public void Translate_ReturnsPortuguesePartialInsteadOfEnglishMix()
    {
        var translator = new OfflinePhraseTranslator();

        Assert.Equal(
            "Traducao parcial offline: eu achar isto e uma",
            translator.Translate("I think this is an unfamiliar sentence"));
    }
}