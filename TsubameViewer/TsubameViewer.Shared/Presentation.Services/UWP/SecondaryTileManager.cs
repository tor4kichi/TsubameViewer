﻿using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.FolderItemListing;
using TsubameViewer.Models.Infrastructure;
using Windows.Storage;
using Windows.UI.StartScreen;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TsubameViewer.Presentation.Services.UWP
{
    public sealed class SecondaryTileManager
    {
        private readonly ThumbnailManager _thumbnailManager;
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

            public bool RemoveTiteId(string path)
            {
                return _collection.Delete(path);
            }
        }

        public class SecondaryTileId
        {
            [BsonId]
            public string Path { get; set; }

            [BsonField]
            public string TiteId { get; set; }
        }



        public SecondaryTileManager(
            ThumbnailManager thumbnailManager,
            SecondaryTileIdRepository secondaryTileIdRepository
            )
        {
            _thumbnailManager = thumbnailManager;
            _secondaryTileIdRepository = secondaryTileIdRepository;
        }

        Dictionary<string, SecondaryTile> Tiles { get; set; } 

        public async Task InitializeAsync()
        {
            var tiles = await SecondaryTile.FindAllAsync();
            Tiles = tiles.ToDictionary(x => x.TileId);
            // tilesに含まれない生成済みのセカンダリタイル用サムネイルを削除する
            await _thumbnailManager.SecondaryThumbnailDeleteNotExist(tiles.Select(x => _secondaryTileIdRepository.FindPathFromTileId(x.TileId)));
        }


        public bool ExistTile(string path)
        {
            return SecondaryTile.Exists(_secondaryTileIdRepository.GetTileId(path));
        }


        public static SecondaryTileArguments DeserializeSecondaryTileArguments(string arguments)
        {
            return JsonSerializer.Deserialize<SecondaryTileArguments>(arguments);
        }

        public async Task<bool> AddSecondaryTile(string token, string path, string displayName, IStorageItem storageItem)
        {
            static string AppLocalFolderUriConvertToMsAppDataSchema(string uri)
            {
                var index = uri.IndexOf("LocalState\\");
                return "ms-appdata:///local/" + uri.Substring(index + "LocalState\\".Length).Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            var tileId = _secondaryTileIdRepository.GetTileId(storageItem.Path);
            var item = new SecondaryTileArguments() { Token = token, Path = path };

            var tileThubmnails = await _thumbnailManager.GenerateSecondaryThumbnailImageAsync(storageItem);
            var json = JsonSerializer.Serialize(item);
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
                Tiles.Add(path, tile);
            }
            return result;
        }

        public async Task<bool> RemoveSecondaryTile(IStorageItem storageItem)
        {
            var tileId = _secondaryTileIdRepository.GetTileId(storageItem.Path);
            if (Tiles.TryGetValue(tileId, out var tile))
            {
                if (await tile.RequestDeleteAsync())
                {
                    Tiles.Remove(tileId);
                    Debug.WriteLine("セカンダリタイルを削除：" + storageItem.Path);
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class SecondaryTileArguments
    {
        public string Token { get; set; }

        public string Path { get; set; }
    }
}