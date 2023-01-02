using Microsoft.IO;
using SharpCompress.Archives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.Transform;

public class SplitImageTransform
{
    private readonly RecyclableMemoryStreamManager _memoryStreamManager;

    public SplitImageTransform(RecyclableMemoryStreamManager memoryStreamManager)
    {
        _memoryStreamManager = memoryStreamManager;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="archive">処理対象のアーカイブ（読み取りのみ）</param>
    /// <param name="thresholdAspectRatio">分割対象とする縦横比のしきい値</param>
    /// <param name="pageAspectRatio">分割時に、中央からどれだけ横幅を切り出すのかを決める横縦比。画像の「幅 / 高さ」で求める。 nullの場合、単に二分割する。</param>
    /// <param name="isLeftBinding">左開きを示すフラグ。日本の漫画は基本的に左開き。</param>
    /// <param name="encoderId">BitmapEncoder.XXXEncoderIdの値を使用する。nullの場合は、JpegEncoderIdを使用する。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task<IWritableArchive> SplitImageOutputToArchiveFileAsync(IArchive archive, double thresholdAspectRatio, double? pageAspectRatio, bool isLeftBinding, Guid? encoderId, CancellationToken ct)
    {
        var outputArchive = ArchiveFactory.Create(SharpCompress.Common.ArchiveType.Zip);

        try
        {
            // アーカイブを開いて画像を列挙
            foreach (var entry in archive.Entries.Where(x => SupportedFileTypesHelper.IsSupportedImageFileExtension(x.Key)))
            {
                var memoryStream = _memoryStreamManager.GetStream();
                
                try
                {
                    using (var entryStream = entry.OpenEntryStream())
                    {
                        entryStream.CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);
                    }

                    if (pageAspectRatio < thresholdAspectRatio)
                    {
                        var (leftImage, rightImage, ext) = await SplitTwoImageAsync(pageAspectRatio, encoderId, memoryStream, ct);
                        string dir = Path.GetDirectoryName(entry.Key);
                        string fileName = Path.GetFileNameWithoutExtension(entry.Key);
                        if (isLeftBinding)
                        {
                            outputArchive.AddEntry(Path.Combine(dir, $"{fileName}_0.{ext}"), rightImage, closeStream: true);
                            outputArchive.AddEntry(Path.Combine(dir, $"{fileName}_1.{ext}"), leftImage, closeStream: true);
                        }
                        else
                        {
                            outputArchive.AddEntry(Path.Combine(dir, $"{fileName}_0.{ext}"), leftImage, closeStream: true);
                            outputArchive.AddEntry(Path.Combine(dir, $"{fileName}_1.{ext}"), rightImage, closeStream: true);
                        }                            
                    }
                    else
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        outputArchive.AddEntry(entry.Key, memoryStream, closeStream: true);
                    }
                }
                catch
                {
                    memoryStream.Dispose();
                    throw;
                }
            }

            return outputArchive;
        }
        catch
        {
            outputArchive.Dispose();
            throw;
        }            
    }

    public async Task<(Stream leftImage, Stream rightImage, string ext)> SplitTwoImageAsync(double? pageAspectRatio, Guid? encoderId, Stream memoryStream, CancellationToken ct)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(memoryStream.AsRandomAccessStream());

        // 画像の縦横比がTargetVHRatioよりも小さい（＝より横長な）場合に分割を実行する
        double imageVHRatio = decoder.PixelHeight / (double)decoder.PixelWidth;
        
        uint halfWidth = decoder.PixelWidth / 2;
        BitmapBounds leftBounds;
        BitmapBounds rightBounds;
        if (pageAspectRatio is not null and double hvRatio)
        {
            uint pageWidth = (uint)Math.Round(decoder.PixelHeight * hvRatio);
            leftBounds = new BitmapBounds() { X = halfWidth - pageWidth, Y = 0, Width = pageWidth, Height = decoder.PixelHeight };
            rightBounds = new BitmapBounds() { X = halfWidth, Y = 0, Width = pageWidth, Height = decoder.PixelHeight };
        }
        else
        {
            leftBounds = new BitmapBounds() { X = halfWidth * 0, Y = 0, Width = halfWidth, Height = decoder.PixelHeight };
            rightBounds = new BitmapBounds() { X = halfWidth * 1, Y = 0, Width = halfWidth, Height = decoder.PixelHeight };
        }


        async Task<(Stream outputStream, string ext)> ClipAndEncode(BitmapBounds bounds)
        {
            var encoderStream = _memoryStreamManager.GetStream();
            try
            {
                var ext = await GetClipedImageStreamAsync(encoderStream.AsRandomAccessStream(), decoder, bounds, encoderId, ct);
                encoderStream.Seek(0, SeekOrigin.Begin);
                return (encoderStream, ext);
            }
            catch
            {
                encoderStream.Dispose();
                throw;
            }
        }

        Stream leftImage = null;
        Stream rightImage = null;
        try
        {
            (leftImage, string ext) = await ClipAndEncode(leftBounds);
            (rightImage, _) = await ClipAndEncode(rightBounds);

            return (leftImage, rightImage, ext);
        }
        catch
        {
            leftImage?.Dispose();
            throw;
        }
        finally
        {
            memoryStream.Dispose();
        }
    }

    public async Task<(Stream image, string ext)> ClipCoverImageAsync(double hvRatio, Guid? encoderId, Stream memoryStream, CancellationToken ct)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(memoryStream.AsRandomAccessStream());

        // 画像の縦横比がTargetVHRatioよりも小さい（＝より横長な）場合に分割を実行する
        double imageVHRatio = decoder.PixelHeight / (double)decoder.PixelWidth;

        uint halfWidth = decoder.PixelWidth / 2;
        uint pageWidth = (uint)Math.Round(decoder.PixelHeight * hvRatio);
        BitmapBounds bounds = new BitmapBounds() { X = halfWidth - pageWidth / 2, Y = 0, Width = pageWidth, Height = decoder.PixelHeight };

        async Task<(Stream outputStream, string ext)> ClipAndEncode(BitmapBounds bounds)
        {
            var encoderStream = _memoryStreamManager.GetStream();
            try
            {
                var ext = await GetClipedImageStreamAsync(encoderStream.AsRandomAccessStream(), decoder, bounds, encoderId, ct);
                encoderStream.Seek(0, SeekOrigin.Begin);
                return (encoderStream, ext);
            }
            catch
            {
                encoderStream.Dispose();
                throw;
            }
        }

        try
        {
            return await ClipAndEncode(bounds);
        }
        finally
        {
            memoryStream.Dispose();
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="thresholdAspectRatio">分割対象とする縦横比のしきい値</param>
    /// <param name="pageVHRatio">分割時に、中央からどれだけ横幅を切り出すのかを決める縦横比。nullの場合、単に二分割する。</param>
    /// <param name="isLeftBinding">左開きを示すフラグ。日本の漫画は基本的に左開き。</param>
    /// <param name="encoderId">BitmapEncoder.XXXEncoderIdの値を使用する。nullの場合は、JpegEncoderIdを使用する。</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public async Task SplitImageOutputToFolderAsync(StorageFolder inputFolder, StorageFolder outputFolder, double thresholdAspectRatio, double? pageAspectRatio, bool isLeftBinding, Guid? encoderId, CancellationToken ct)
    {
        // アーカイブを開いて画像を列挙
        var query = inputFolder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.OrderByName, fileTypeFilter: SupportedFileTypesHelper.SupportedImageFileExtensions) { FolderDepth = Windows.Storage.Search.FolderDepth.Deep });
        var files = await query.GetFilesAsync().AsTask(ct);
        foreach (var file in files)
        {
            using var fs = await FileRandomAccessStream.OpenAsync(file.Path, FileAccessMode.Read).AsTask(ct);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fs).AsTask(ct);

            // 画像の縦横比がTargetVHRatioよりも小さい（＝より横長な）場合に分割を実行する
            double imageVHRatio = decoder.PixelHeight / (double)decoder.PixelWidth;
            if (imageVHRatio < thresholdAspectRatio)
            {
                uint halfWidth = decoder.PixelWidth / 2;
                var filePath = file.Path.Substring(inputFolder.Path.Length);
                var dir = Path.GetDirectoryName(filePath);
                var ext = file.FileType;
                var fileName = Path.GetFileNameWithoutExtension(filePath);

                BitmapBounds leftBounds;
                BitmapBounds rightBounds;                    
                if (pageAspectRatio is not null and double hvRatio)
                {
                    uint pageWidth = (uint)Math.Round(decoder.PixelHeight * hvRatio);
                    leftBounds = new BitmapBounds() { X = halfWidth - pageWidth, Y = 0, Width = pageWidth, Height = decoder.PixelHeight };
                    rightBounds = new BitmapBounds() { X = halfWidth , Y = 0, Width = pageWidth, Height = decoder.PixelHeight };
                }
                else
                {
                    leftBounds = new BitmapBounds() { X = halfWidth * 0, Y = 0, Width = halfWidth, Height = decoder.PixelHeight };
                    rightBounds = new BitmapBounds() { X = halfWidth * 1, Y = 0, Width = halfWidth, Height = decoder.PixelHeight };
                }

                if (isLeftBinding)
                {
                    await ClipAndEncode(2u, leftBounds);
                    await ClipAndEncode(1u, rightBounds);
                }
                else
                {
                    await ClipAndEncode(1u, leftBounds);
                    await ClipAndEncode(2u, rightBounds);
                }

                // 分割元となったファイルをゴミ箱へ移動
                if (inputFolder.Path == outputFolder.Path)
                {
                    await file.DeleteAsync().AsTask(ct);
                }

                async Task ClipAndEncode(uint index, BitmapBounds bounds)
                {
                    var outputFile = await outputFolder.DigStorageFileFromPathAsync(Path.Combine(dir, Path.ChangeExtension($"{fileName}_{index}", ext)), CreationCollisionOption.ReplaceExisting, ct);
                    try
                    {
                        using (var writeStream = await FileRandomAccessStream.OpenAsync(outputFile.Path, FileAccessMode.ReadWrite))
                        {
                            var changeExt = await GetClipedImageStreamAsync(writeStream, decoder, bounds, encoderId, ct);
                            if (outputFile.FileType.EndsWith(changeExt) is false)
                            {
                                await outputFile.RenameAsync(Path.ChangeExtension(outputFile.Name, changeExt), NameCollisionOption.ReplaceExisting);
                            }
                        }
                    }
                    catch
                    {
                        await outputFile.DeleteAsync();
                        throw;
                    }
                }
            }
        }
    }

    static readonly BitmapPropertySet _jpegPropertySet = new BitmapPropertySet()
    {
        { "ImageQuality", new BitmapTypedValue(0.8d, Windows.Foundation.PropertyType.Single) },
    };

    public static async Task<string> GetClipedImageStreamAsync(IRandomAccessStream outputStream, BitmapDecoder decoder, BitmapBounds bounds, Guid? encoderId, CancellationToken ct)
    {
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId ?? BitmapEncoder.JpegEncoderId, outputStream, _jpegPropertySet);

        encoder.BitmapTransform.Bounds = bounds;
        var pixelData = await decoder.GetPixelDataAsync().AsTask(ct);
        var detachedPixelData = pixelData.DetachPixelData();
        pixelData = null;

        encoder.SetPixelData(decoder.BitmapPixelFormat, decoder.BitmapAlphaMode, decoder.OrientedPixelWidth, decoder.OrientedPixelHeight, decoder.DpiX, decoder.DpiY, detachedPixelData);
        
        await encoder.FlushAsync().AsTask(ct);
        await outputStream.FlushAsync().AsTask(ct);

        return encoder.EncoderInformation.FileExtensions.First();
    }
}
