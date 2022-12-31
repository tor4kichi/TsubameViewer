using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Services;
using TsubameViewer.Core.Infrastructure;
using Windows.Storage;
using Windows.UI.StartScreen;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TsubameViewer.Services;

public sealed class SecondaryTileManager : ISecondaryTileManager
{
    private readonly ISecondaryTileThumbnailImageService _secondaryTileThumbnailImageService;
    private readonly SecondaryTileIdRepository _secondaryTileIdRepository;

    public sealed class SecondaryTileIdRepository : LiteDBServiceBase<SecondaryTileId>
    {
        public SecondaryTileIdRepository(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
            _collection.EnsureIndex(x => x.TiteId);
        }

        public string FindPathFromTileId(string tileId)
        {
            return _collection.FindOne(x => x.TiteId == tileId)?.Path;
        }

        public string GetTileId(string path)
        {
            var tileId = _collection.FindById(path)?.TiteId;
            if (tileId != null) { return tileId; }

            tileId = new Random().Next().ToString();
            _collection.Insert(new SecondaryTileId() { Path = path, TiteId = tileId });
            return tileId;
        }


        public IEnumerable<string> GetAllTileIdUnderPath(string path)
        {
            return _collection.Find(x => x.Path.StartsWith(path)).Select(x => x.TiteId);
        }

        public bool RemoveTiteId(string path)
        {
            return _collection.Delete(path);
        }
    }

    public class SecondaryTileId
    {
        [BsonId]
        public string Path { get; set; }

        [BsonId]
        public string ThumbnailSubFolderName { get; set; }

        [BsonField]
        public string TiteId { get; set; }
    }



    public SecondaryTileManager(
        ISecondaryTileThumbnailImageService secondaryTileThumbnailImageService,
        SecondaryTileIdRepository secondaryTileIdRepository
        )
    {
        _secondaryTileThumbnailImageService = secondaryTileThumbnailImageService;
        _secondaryTileIdRepository = secondaryTileIdRepository;
    }

    Dictionary<string, SecondaryTile> Tiles { get; set; }

    public async Task InitializeAsync()
    {
        var tiles = await SecondaryTile.FindAllAsync();
        Tiles = tiles.ToDictionary(x => x.TileId);
        // tilesに含まれない生成済みのセカンダリタイル用サムネイルを削除する
        await _secondaryTileThumbnailImageService.SecondaryThumbnailDeleteNotExist(tiles.Select(x => x.TileId));
    }


    public bool ExistTile(string path)
    {
        return SecondaryTile.Exists(_secondaryTileIdRepository.GetTileId(path));
    }

    public static bool DeserializeSecondaryTileArguments(string arguments, out SecondaryTileArguments? args)
    {
        try
        {
            args = JsonSerializer.Deserialize<SecondaryTileArguments>(arguments);
            return true;
        }
        catch
        {
            args = null;
            return false;
        }
    }

    public async Task<bool> AddSecondaryTile(ISecondaryTileArguments arguments, string displayName, IStorageItem storageItem)
    {
        static string AppLocalFolderUriConvertToMsAppDataSchema(string uri)
        {
            var index = uri.IndexOf("LocalState\\");
            return "ms-appdata:///local/" + uri.Substring(index + "LocalState\\".Length).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        var tileId = _secondaryTileIdRepository.GetTileId(storageItem.Path);
        var tileThubmnails = await Task.Run(async () => await _secondaryTileThumbnailImageService.GenerateSecondaryThumbnailImageAsync(storageItem, tileId, CancellationToken.None));
        var json = JsonSerializer.Serialize(arguments);
        var tile = new SecondaryTile(
            tileId,
            displayName,
            json,
            new Uri(AppLocalFolderUriConvertToMsAppDataSchema(tileThubmnails.Square150x150Logo.Path)),
            TileSize.Square150x150
            );
        tile.VisualElements.Square310x310Logo = new Uri(AppLocalFolderUriConvertToMsAppDataSchema(tileThubmnails.Square310x310Logo.Path)); //   の形にする必要がある
        tile.VisualElements.Wide310x150Logo = new Uri(AppLocalFolderUriConvertToMsAppDataSchema(tileThubmnails.Wide310x150Logo.Path));

        var result = await tile.RequestCreateAsync();
        Debug.WriteLine($"セカンダリタイルを追加： result {result} - " + storageItem.Path);
        if (result)
        {
            // アプリ起動時には既に追加済みだったが、その後タイルを削除して、またタイルを追加と操作した場合に対応するため
            // 重複のID登録が起きうることを想定
            Tiles.Remove(tileId);
            Tiles.Add(tileId, tile);
        }
        return result;
    }

    public async Task<bool> RemoveSecondaryTile(string path)
    {
        var tileIds = _secondaryTileIdRepository.GetAllTileIdUnderPath(path);
        foreach (var tileId in tileIds)
        {
            try
            {
                if (Tiles.TryGetValue(tileId, out var tile))
                {
                    if (await tile.RequestDeleteAsync())
                    {
                        Tiles.Remove(tileId);
                        Debug.WriteLine("セカンダリタイルを削除：" + path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        return false;
    }
}

public sealed class SecondaryTileArguments : ISecondaryTileArguments
{
    public string Path { get; set; }

    public string PageName { get; set; }
}
