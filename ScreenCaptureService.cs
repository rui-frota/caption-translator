using System.Drawing;
using System.Timers;
using Timer = System.Timers.Timer;

namespace CaptionTranslator.Capture;

public sealed class ScreenCaptureService : IDisposable
{
    private readonly Timer _timer = new(250);
    private Rectangle _region;

    public ScreenCaptureService()
    {
        _timer.Elapsed += Timer_Elapsed;
    }

    public event EventHandler<Bitmap>? FrameCaptured;

    public void Start(Rectangle region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region));
        }

        _region = region;
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Dispose()
    {
        _timer.Dispose();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs eventArgs)
    {
        using var screenGraphics = Graphics.FromHwnd(IntPtr.Zero);
        var frame = new Bitmap(_region.Width, _region.Height);
        using var bitmapGraphics = Graphics.FromImage(frame);
        bitmapGraphics.CopyFromScreen(_region.Location, Point.Empty, _region.Size, CopyPixelOperation.SourceCopy);
        FrameCaptured?.Invoke(this, frame);
    }
}