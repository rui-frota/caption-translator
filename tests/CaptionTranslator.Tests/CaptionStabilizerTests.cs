using CaptionTranslator.Pipeline;
using Xunit;

namespace CaptionTranslator.Tests;

public sealed class CaptionStabilizerTests
{
    [Fact]
    public void Accept_EmitsTextAfterItAppearsInTwoConsecutiveFrames()
    {
        var stabilizer = new CaptionStabilizer();

        Assert.Null(stabilizer.Accept("Welcome to the meeting"));

        var result = stabilizer.Accept("Welcome to the meeting");

        Assert.Equal("Welcome to the meeting", result);
    }

    [Fact]
    public void Accept_DoesNotEmitTextThatWasAlreadyEmitted()
    {
        var stabilizer = new CaptionStabilizer();
        stabilizer.Accept("Hello");
        stabilizer.Accept("Hello");

        Assert.Null(stabilizer.Accept("Hello"));
    }

    [Fact]
    public void Accept_ResetsTheStabilityCountWhenTextChanges()
    {
        var stabilizer = new CaptionStabilizer();
        stabilizer.Accept("Partial caption");

        Assert.Null(stabilizer.Accept("Complete caption"));
        Assert.Equal("Complete caption", stabilizer.Accept("Complete caption"));
    }

    [Fact]
    public void Reset_AllowsTheSameTextToBeEmittedAgain()
    {
        var stabilizer = new CaptionStabilizer();
        stabilizer.Accept("Hello");
        stabilizer.Accept("Hello");
        stabilizer.Reset();

        stabilizer.Accept("Hello");

        Assert.Equal("Hello", stabilizer.Accept("Hello"));
    }
}