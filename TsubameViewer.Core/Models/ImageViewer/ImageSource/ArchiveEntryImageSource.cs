﻿using Reactive.Bindings;
using SharpCompress.Archives;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Models.Navigation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace TsubameViewer.Core.Models.ImageViewer.ImageSource;

public interface IArchiveEntryImageSource
{
    string EntryKey { get; }
}

public sealed class ArchiveEntryImageSource : IArchiveEntryImageSource, IImageSource
{
    private readonly IArchiveEntry _entry;
    private readonly ArchiveDirectoryToken _archiveDirectoryToken;
    private readonly ArchiveImageCollection _archiveImageCollection;

    public ArchiveEntryImageSource(
        IArchiveEntry entry, 
        ArchiveDirectoryToken archiveDirectoryToken, 
        ArchiveImageCollection archiveImageCollection
        )
    {
        _entry = entry;
        _archiveDirectoryToken = archiveDirectoryToken;
        _archiveImageCollection = archiveImageCollection;
        StorageItem = _archiveImageCollection.File;
        DateCreated = entry.CreatedTime ?? entry.LastModifiedTime ?? entry.ArchivedTime ?? DateTime.Now;
        Path = PageNavigationConstants.MakeStorageItemIdWithPage(archiveImageCollection.File.Path, entry.Key);        
    }

    public StorageFile StorageItem { get; }

    IStorageItem IImageSource.StorageItem => StorageItem;

    public string ArchiveDirectoryName => _archiveDirectoryToken?.Label;

    // Note: NameをImageViewerで表示時のページ名として扱っている
    // フォルダ名をスキップしてしまうとアーカイブ内に別フォルダに同名ファイルがある場合に
    // 常に最初のフォルダの同名ファイルが選ばれてしまう問題が起きる
    private string _name;
    //public string Name => _name ??= System.IO.Path.GetFileName(_entry.Key);
    public string Name => _name ??= _entry.Key;

    public string Path { get; }

    public DateTime DateCreated { get; }

    public string EntryKey => _entry.Key;

    public async Task<IRandomAccessStream> GetImageStreamAsync(CancellationToken ct)
    {
        using var mylock = await _archiveEntryAccessLock.LockAsync(ct);

        var memoryStream = new InMemoryRandomAccessStream();
        using (var entryStream = _entry.OpenEntryStream())
        {
            // Note: コメントアウトした書き方だと稀にコピーできないケースが発生する
            // entryStream.CopyTo(memoryStream.AsStream());
            await RandomAccessStream.CopyAsync(entryStream.AsInputStream(), memoryStream);
            memoryStream.Seek(0);

            ct.ThrowIfCancellationRequested();
        }

        return memoryStream;
    }


    internal static readonly AsyncLock _archiveEntryAccessLock = new ();


    public IArchiveEntry GetParentDirectoryEntry()
    {
        if (_archiveDirectoryToken.Key == null
            || _archiveDirectoryToken.IsRoot
            || !(_archiveDirectoryToken.Key.Contains(System.IO.Path.DirectorySeparatorChar) || _archiveDirectoryToken.Entry.Key.Contains(System.IO.Path.AltDirectorySeparatorChar))                
            )
        {
            return null;
        }

        return _archiveDirectoryToken.Entry;
    }

    public bool Equals(IImageSource other)
    {
        if (other == null) { return false; }
        return this.Path == other.Path;
    }

    public override string ToString()
    {
        return Path;
    }
}
