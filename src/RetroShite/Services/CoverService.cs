namespace RetroShite.Services;

public static class CoverService
{
    public const int MaximumDimension = 1200;
    public const int JpegQuality = 80;

    public static async Task SaveCapturedAsync(Stream source, string destination)
    {
        var temporary = destination + ".capture";
        try
        {
            await using (var output = File.Create(temporary))
                await source.CopyToAsync(output);
            await Task.Run(() => OptimiseFile(temporary, destination));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static (long Before, long After, bool Changed) OptimiseExisting(string path)
    {
        var before = new FileInfo(path).Length;
        var temporary = path + ".optimising";
        try
        {
            // Already-small covers do not benefit meaningfully from repeated JPEG encoding.
            if (before <= 350_000 && IsWithinSize(path)) return (before, before, false);
            OptimiseFile(path, temporary);
            var after = new FileInfo(temporary).Length;
            if (after >= before)
                return (before, before, false);
            File.Move(temporary, path, true);
            return (before, after, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static bool IsWithinSize(string path)
    {
#if ANDROID
        var options = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeFile(path, options);
        return options.OutWidth > 0 && options.OutHeight > 0 &&
               Math.Max(options.OutWidth, options.OutHeight) <= MaximumDimension;
#else
        return false;
#endif
    }

    private static void OptimiseFile(string source, string destination)
    {
#if ANDROID
        var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeFile(source, bounds);
        if (bounds.OutWidth <= 0 || bounds.OutHeight <= 0)
            throw new InvalidDataException("Cover image could not be decoded.");
        var sample = 1;
        while (Math.Max(bounds.OutWidth, bounds.OutHeight) / (sample * 2) >= MaximumDimension)
            sample *= 2;
        var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
        using var decoded = Android.Graphics.BitmapFactory.DecodeFile(source, options)
            ?? throw new InvalidDataException("Cover image could not be decoded.");

        // Camera photos may store their rotation in EXIF rather than their pixel data.
        using var matrix = new Android.Graphics.Matrix();
        var orientation = new Android.Media.ExifInterface(source).GetAttributeInt(
            Android.Media.ExifInterface.TagOrientation, 1);
        switch (orientation)
        {
            case 2: matrix.SetScale(-1, 1); break;
            case 3: matrix.SetRotate(180); break;
            case 4: matrix.SetScale(1, -1); break;
            case 5: matrix.SetRotate(90); matrix.PostScale(-1, 1); break;
            case 6: matrix.SetRotate(90); break;
            case 7: matrix.SetRotate(270); matrix.PostScale(-1, 1); break;
            case 8: matrix.SetRotate(270); break;
        }
        using var oriented = Android.Graphics.Bitmap.CreateBitmap(decoded, 0, 0,
            decoded.Width, decoded.Height, matrix, true);
        var scale = Math.Min(1.0, (double)MaximumDimension / Math.Max(oriented.Width, oriented.Height));
        using var scaled = Android.Graphics.Bitmap.CreateScaledBitmap(oriented,
            Math.Max(1, (int)Math.Round(oriented.Width * scale)),
            Math.Max(1, (int)Math.Round(oriented.Height * scale)), true);
        using (var output = File.Create(destination))
        {
            if (!scaled.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, JpegQuality, output))
                throw new IOException("Could not encode cover image.");
        }
        if (!IsWithinSize(destination)) throw new InvalidDataException("Optimised image failed validation.");
#else
        throw new PlatformNotSupportedException("Image optimisation requires Android.");
#endif
    }
}
