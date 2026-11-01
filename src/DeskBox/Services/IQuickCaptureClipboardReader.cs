using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DeskBox.Services;

public interface IQuickCaptureClipboardReader
{
    event EventHandler<object>? ContentChanged;

    Task<QuickCaptureClipboardContent?> ReadContentAsync();
}

public sealed class QuickCaptureClipboardContent
{
    private QuickCaptureClipboardContent(string? text, byte[]? imagePngBytes)
    {
        Text = text;
        ImagePngBytes = imagePngBytes;
    }

    public string? Text { get; }

    public byte[]? ImagePngBytes { get; }

    public bool HasImage => ImagePngBytes is { Length: > 0 };

    public static QuickCaptureClipboardContent FromText(string text) => new(text, null);

    public static QuickCaptureClipboardContent FromImage(byte[] imagePngBytes) => new(null, imagePngBytes);
}

public sealed class WindowsQuickCaptureClipboardReader : IQuickCaptureClipboardReader
{
    public event EventHandler<object>? ContentChanged
    {
        add => Clipboard.ContentChanged += value;
        remove => Clipboard.ContentChanged -= value;
    }

    public async Task<QuickCaptureClipboardContent?> ReadContentAsync()
    {
        var data = Clipboard.GetContent();
        if (data.Contains(StandardDataFormats.Bitmap))
        {
            byte[]? pngBytes = await TryReadBitmapAsPngAsync(data);
            if (pngBytes is { Length: > 0 })
            {
                return QuickCaptureClipboardContent.FromImage(pngBytes);
            }
        }

        // Some sources (WeChat, QQ, browsers) publish a clipboard bitmap the
        // WinRT GetBitmapAsync stream cannot decode (WINCODEC_ERR_BADIMAGE)
        // while the raw "PNG" or CF_DIB payloads on the same package are
        // valid. Falling through to those formats keeps those captures out
        // of "ignored: empty-or-unsupported" (feedback 335).
        byte[]? rawPngBytes = await TryReadRawFormatAsPngAsync(data, "PNG");
        if (rawPngBytes is { Length: > 0 })
        {
            return QuickCaptureClipboardContent.FromImage(rawPngBytes);
        }

        byte[]? dibPngBytes = await TryReadDeviceIndependentBitmapAsPngAsync(data);
        if (dibPngBytes is { Length: > 0 })
        {
            return QuickCaptureClipboardContent.FromImage(dibPngBytes);
        }

        if (data.Contains(StandardDataFormats.StorageItems))
        {
            var storageItems = await data.GetStorageItemsAsync();
            foreach (var storageItem in storageItems)
            {
                if (storageItem is Windows.Storage.StorageFile file &&
                    IsImageFile(file.Path))
                {
                    byte[]? bytes = await TryReadStorageFileAsPngAsync(file);
                    if (bytes is { Length: > 0 })
                    {
                        return QuickCaptureClipboardContent.FromImage(bytes);
                    }
                }
            }
        }

        if (data.Contains(StandardDataFormats.Text))
        {
            string text = await data.GetTextAsync();
            return string.IsNullOrWhiteSpace(text)
                ? null
                : QuickCaptureClipboardContent.FromText(text);
        }

        if (data.Contains(StandardDataFormats.WebLink))
        {
            var link = await data.GetWebLinkAsync();
            return string.IsNullOrWhiteSpace(link?.AbsoluteUri)
                ? null
                : QuickCaptureClipboardContent.FromText(link.AbsoluteUri);
        }

        return null;
    }

    private static async Task<byte[]?> TryReadBitmapAsPngAsync(DataPackageView data)
    {
        try
        {
            var bitmapReference = await data.GetBitmapAsync();
            using var stream = await bitmapReference.OpenReadAsync();
            return await TryDecodeStreamAsPngAsync(stream);
        }
        catch (Exception ex)
        {
            App.Log($"[QuickCaptureClipboardReader] Failed to read bitmap: {ex}");
            return null;
        }
    }

    private static async Task<byte[]?> TryReadRawFormatAsPngAsync(
        DataPackageView data,
        string formatId)
    {
        try
        {
            if (!data.Contains(formatId))
            {
                return null;
            }

            object payload = await data.GetDataAsync(formatId);
            if (payload is not IRandomAccessStream payloadStream)
            {
                return null;
            }

            using (payloadStream)
            {
                return await TryDecodeStreamAsPngAsync(payloadStream);
            }
        }
        catch (Exception ex)
        {
            App.Log(
                $"[QuickCaptureClipboardReader] Failed to read '{formatId}' payload: {ex.Message}");
            return null;
        }
    }

    private static async Task<byte[]?> TryReadDeviceIndependentBitmapAsPngAsync(
        DataPackageView data)
    {
        try
        {
            const string dibFormatId = "DeviceIndependentBitmap";
            if (!data.Contains(dibFormatId))
            {
                return null;
            }

            object payload = await data.GetDataAsync(dibFormatId);
            if (payload is not IRandomAccessStream payloadStream)
            {
                return null;
            }

            byte[] dibBytes;
            using (payloadStream)
            {
                dibBytes = await ReadAllBytesAsync(payloadStream);
            }

            byte[]? bmpBytes = ClipboardDibComposer.ComposeFileBytes(dibBytes);
            if (bmpBytes is null)
            {
                return null;
            }

            using var bmpStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(bmpStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bmpBytes);
                await writer.StoreAsync();
            }

            return await TryDecodeStreamAsPngAsync(bmpStream);
        }
        catch (Exception ex)
        {
            App.Log(
                "[QuickCaptureClipboardReader] Failed to read DeviceIndependentBitmap payload: " +
                ex.Message);
            return null;
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStream stream)
    {
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static async Task<byte[]?> TryDecodeStreamAsPngAsync(IRandomAccessStream stream)
    {
        var decoder = await BitmapDecoder.CreateAsync(stream);
        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetSoftwareBitmap(await decoder.GetSoftwareBitmapAsync());
        await encoder.FlushAsync();

        var bytes = new byte[outputStream.Size];
        outputStream.Seek(0);
        using var dataReader = new DataReader(outputStream.GetInputStreamAt(0));
        await dataReader.LoadAsync((uint)outputStream.Size);
        dataReader.ReadBytes(bytes);
        return bytes;
    }

    private static async Task<byte[]?> TryReadStorageFileAsPngAsync(StorageFile file)
    {
        try
        {
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var outputStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
            encoder.SetSoftwareBitmap(await decoder.GetSoftwareBitmapAsync());
            await encoder.FlushAsync();

            var bytes = new byte[outputStream.Size];
            using var dataReader = new DataReader(outputStream.GetInputStreamAt(0));
            await dataReader.LoadAsync((uint)outputStream.Size);
            dataReader.ReadBytes(bytes);
            return bytes;
        }
        catch (Exception ex)
        {
            App.Log($"[QuickCaptureClipboardReader] Failed to read image file: {ex}");
            return null;
        }
    }

    private static bool IsImageFile(string? path)
    {
        string extension = string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Wraps a clipboard CF_DIB payload (BITMAPINFOHEADER + optional palette +
/// pixels) in a BITMAPFILEHEADER so WIC can decode it as a normal BMP. The
/// DIB bytes are copied verbatim after the 14-byte header; only the pixel
/// offset needs computing.
/// </summary>
internal static class ClipboardDibComposer
{
    internal static byte[]? ComposeFileBytes(byte[] dib)
    {
        if (dib.Length < 40)
        {
            return null;
        }

        int headerSize = BitConverter.ToInt32(dib, 0);
        if (headerSize is not (40 or 52 or 56 or 64 or 108 or 124))
        {
            // BITMAPCOREHEADER and unknown layouts are not worth guessing;
            // the PNG fallback should already have handled modern sources.
            return null;
        }

        if (dib.Length < headerSize)
        {
            return null;
        }

        int width = BitConverter.ToInt32(dib, 4);
        int height = BitConverter.ToInt32(dib, 8);
        short bitCount = BitConverter.ToInt16(dib, 14);
        int compression = BitConverter.ToInt32(dib, 16);
        uint clrUsed = BitConverter.ToUInt32(dib, 32);
        if (width == 0 || height == 0 ||
            bitCount is not (1 or 2 or 4 or 8 or 16 or 24 or 32))
        {
            return null;
        }

        int paletteBytes = 0;
        if (bitCount <= 8)
        {
            uint entries = clrUsed != 0 ? clrUsed : (uint)(1 << bitCount);
            paletteBytes = checked((int)(entries * 4u));
        }

        // For a 40-byte header, BI_BITFIELDS stores its three DWORD masks
        // after the header and before the palette; the extended headers
        // already carry the masks inside headerSize.
        int maskBytes = compression == 3 && headerSize == 40 ? 12 : 0;

        int pixelOffset = checked(14 + headerSize + maskBytes + paletteBytes);
        if (pixelOffset > 14 + dib.Length)
        {
            return null;
        }

        var bmp = new byte[14 + dib.Length];
        bmp[0] = (byte)'B';
        bmp[1] = (byte)'M';
        WriteUInt32(bmp, 2, (uint)bmp.Length);
        WriteUInt32(bmp, 6, 0);
        WriteUInt32(bmp, 10, (uint)pixelOffset);
        System.Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);
        return bmp;
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
