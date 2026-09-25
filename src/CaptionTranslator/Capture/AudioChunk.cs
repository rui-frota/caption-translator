namespace CaptionTranslator.Capture;

public sealed record AudioChunk(byte[] Data, int SampleRate, int Channels);
