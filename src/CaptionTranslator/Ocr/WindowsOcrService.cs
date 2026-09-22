using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace CaptionTranslator.Ocr;

public sealed class WindowsOcrService
{
    private readonly OcrEngine _engine = OcrEngine.TryCreateFromLanguage(new Language("en-US"))
        ?? throw new InvalidOperationException("O OCR ingles nao esta instalado no Windows.");

    public async Task<string> RecognizeAsync(Bitmap frame)
    {
        var rectangle = new Rectangle(0, 0, frame.Width, frame.Height);
        var bitmapData = frame.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[bitmapData.Stride * frame.Height];
            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
            using var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
                pixels.AsBuffer(),
                BitmapPixelFormat.Bgra8,
                frame.Width,
                frame.Height,
                BitmapAlphaMode.Premultiplied);
            var result = await _engine.RecognizeAsync(softwareBitmap);
            return CaptionTextNormalizer.Normalize(result.Text);
        }
        finally
        {
            frame.UnlockBits(bitmapData);
        }
    }
}