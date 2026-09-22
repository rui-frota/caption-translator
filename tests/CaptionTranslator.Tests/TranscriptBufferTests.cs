using CaptionTranslator.Pipeline;
using Xunit;

namespace CaptionTranslator.Tests;

public sealed class TranscriptBufferTests
{
    [Fact]
    public void Append_ConcatenatesNewCaptions()
    {
        var transcript = new TranscriptBuffer();

        transcript.Append("Hello everyone");
        var result = transcript.Append("Welcome to the meeting");

        Assert.Equal("Hello everyone Welcome to the meeting", result);
    }

    [Fact]
    public void Append_ExtendsRollingCaptionWithoutDuplicatingOverlap()
    {
        var transcript = new TranscriptBuffer();

        transcript.Append("This is a");
        var result = transcript.Append("This is a test");

        Assert.Equal("This is a test", result);
    }

    [Fact]
    public void Append_DoesNotDuplicateIdenticalCaption()
    {
        var transcript = new TranscriptBuffer();

        transcript.Append("Hello everyone");
        var result = transcript.Append("Hello everyone");

        Assert.Equal("Hello everyone", result);
    }

    [Fact]
    public void GetNewSegment_ReturnsOnlyWordsAddedToRollingCaption()
    {
        var transcript = new TranscriptBuffer();
        transcript.Append("This is a");

        Assert.Equal("test", transcript.GetNewSegment("This is a test"));
    }

    [Fact]
    public void GetNewSegment_ReturnsEmptyForRepeatedCaption()
    {
        var transcript = new TranscriptBuffer();
        transcript.Append("This is complete");

        Assert.Empty(transcript.GetNewSegment("This is complete"));
    }
}