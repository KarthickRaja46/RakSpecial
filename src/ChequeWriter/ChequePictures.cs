using ChequeWriter.Core;

namespace ChequeWriter;

/// <summary>
/// The picture of a blank cheque for a cheque book: the user's own scan when one is set,
/// otherwise the picture built into the program. Images are cached; callers must not dispose them.
/// </summary>
public static class ChequePictures
{
    static readonly Dictionary<string, Image?> Cache = new();

    public static Image? For(BankLayout bank)
    {
        var own = bank.BackgroundImagePath;
        string key;
        if (own.Length > 0 && File.Exists(own)) key = "file:" + own + "|" + File.GetLastWriteTimeUtc(own).Ticks;
        else if (bank.BuiltInPicture.Length > 0) key = "res:" + bank.BuiltInPicture;
        else return null;

        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var img = key.StartsWith("file:") ? FromFile(own) : FromResource(bank.BuiltInPicture);
            Cache[key] = img;
            return img;
        }
    }

    /// <summary>True when the program has a built-in picture with this name.</summary>
    public static bool HasBuiltIn(string name) =>
        typeof(ChequePictures).Assembly.GetManifestResourceInfo($"RAKSpecial.Images.{name}.jpg") != null;

    static Image? FromResource(string name)
    {
        using var s = typeof(ChequePictures).Assembly.GetManifestResourceStream($"RAKSpecial.Images.{name}.jpg");
        if (s == null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        ms.Position = 0;
        return new Bitmap(Image.FromStream(ms));   // copy, so the stream can be closed
    }

    static Image? FromFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }
        catch { return null; }
    }
}
