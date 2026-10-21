using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DnD.CharSheet.Desktop;

internal static class PortraitCodec
{
    public const int MaximumBytes = 10 * 1024 * 1024;

    public static BitmapSource Decode(byte[] data)
    {
        if (data.Length is 0 or > MaximumBytes) throw new InvalidDataException("Choose an image up to 10 MiB.");
        using var stream = new MemoryStream(data, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        if (decoder is not PngBitmapDecoder and not JpegBitmapDecoder)
            throw new InvalidDataException("Choose a PNG or JPEG image.");
        var frame = decoder.Frames[0];
        if (frame.PixelWidth is < 1 or > 4096 || frame.PixelHeight is < 1 or > 4096 || (long)frame.PixelWidth * frame.PixelHeight > 16_000_000)
            throw new InvalidDataException("Choose an image up to 4096 pixels per side and 16 million pixels.");
        // Inspect dimensions before realizing the pixels; detach from the source stream.
        BitmapSource image = new CachedBitmap(frame, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (frame.Metadata is BitmapMetadata metadata && decoder is JpegBitmapDecoder)
        {
            ushort orientation = 1;
            try { if (metadata.GetQuery("/app1/ifd/{ushort=274}") is ushort value) orientation = value; }
            catch (NotSupportedException) { }
            var transform = new TransformGroup();
            if (orientation is 2 or 4 or 5 or 7) transform.Children.Add(new ScaleTransform(-1, 1));
            var angle = orientation switch { 3 or 4 => 180, 6 or 7 => 90, 5 or 8 => 270, _ => 0 };
            if (angle != 0) transform.Children.Add(new RotateTransform(angle));
            if (transform.Children.Count > 0) image = new TransformedBitmap(image, transform);
        }
        image.Freeze();
        return image;
    }

    public static byte[] Normalize(BitmapSource source, bool crop)
    {
        const int size = 512;
        var scale = crop ? Math.Max((double)size / source.PixelWidth, (double)size / source.PixelHeight)
                         : Math.Min((double)size / source.PixelWidth, (double)size / source.PixelHeight);
        var width = source.PixelWidth * scale;
        var height = source.PixelHeight * scale;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(new SolidColorBrush(Color.FromRgb(16, 20, 25)), null, new Rect(0, 0, size, size));
            context.PushClip(new RectangleGeometry(new Rect(0, 0, size, size)));
            context.DrawImage(source, new Rect((size - width) / 2, (size - height) / 2, width, height));
            context.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    public static byte[] ReadImport(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length is < 1 or > MaximumBytes) throw new InvalidDataException("Choose an image up to 10 MiB.");
        var data = new byte[(int)file.Length];
        file.ReadExactly(data);
        return data;
    }
}
