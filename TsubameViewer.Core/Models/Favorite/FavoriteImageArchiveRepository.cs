using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using TsubameViewer.Core.Contracts.Models;
using Windows.Foundation;
using static TsubameViewer.Core.Models.Favorite.FavoriteImageArchiveRepository;
#nullable enable
namespace TsubameViewer.Core.Models.Favorite;

public sealed class FavoriteImageArchiveRepository 
    : IFavoriteRepository
    , IFavoriteRepository<FavoriteImageArchiveItemFacade>
{
    private readonly ILiteCollection<FavoriteImageArchiveItemEntry> _col;

    public FavoriteImageArchiveRepository(ILiteDatabase liteDatabase)
    {
        _col = liteDatabase.GetCollection<FavoriteImageArchiveItemEntry>();
        _col.EnsureIndex(x => x.Path);
    }


    public bool IsFavoriteAny(string path)
    {
        return _col.Exists(x => x.Path.Equals(path, StringComparison.Ordinal));
    }

    public FavoriteImageArchiveItemFacade AddFavorite(
        string path,
        string label,
        Rect range)
    {
        var entry = new FavoriteImageArchiveItemEntry
        {
            Path = path,
            AddedAt = DateTimeOffset.Now,
            Label = label,
            Range = range
        };

        _col.Insert(entry);
        return new FavoriteImageArchiveItemFacade(entry, _col);
    }

    public IEnumerable<FavoriteImageArchiveItemFacade> GetFavorites(string path)
    {
        return _col.Find(x => x.Path.Equals(path, StringComparison.Ordinal)&& x.Range != null).Select(x => new FavoriteImageArchiveItemFacade(x, _col));
    }

    IEnumerable<IFavoriteItemFacade> IFavoriteRepository.GetFavorites(string path)
    {
        return GetFavorites(path);
    }


    public sealed class FavoriteImageArchiveItemFacade
        : DeferSaveAwareObservableObject
        , IFavoriteItemFacade
    {
        private readonly FavoriteImageArchiveItemEntry _entry;
        private readonly ILiteCollection<FavoriteImageArchiveItemEntry> _col;

        public FavoriteImageArchiveItemFacade(FavoriteImageArchiveItemEntry entry, ILiteCollection<FavoriteImageArchiveItemEntry> col)
        {
            _entry = entry;
            _col = col;
        }

        protected override void OnSave()
        {
            if (IsFavorite)
            {
                _col.Update(_entry);
            }
        }

        public bool IsFavorite
        {
            get => _col.Exists(x => x.Id == _entry.Id);
            set
            {
                bool isFav = IsFavorite;
                SetProperty(isFav, value, this, (m, v) =>
                {
                    var entry = m._entry;
                    if (v)
                    {
                        m._col.Delete(entry.Id);
                    }
                    else
                    {
                        entry.AddedAt = DateTimeOffset.Now;
                        m._col.Upsert(entry);
                    }
                });
            }
        }

        public string Label
        {
            get => _entry.Label;
            set => SetProperty(_entry.Label, value, _entry, (m, v) => m.Label = v);
        }
        public DateTimeOffset AddedAt
        {
            get => _entry.AddedAt;
            set => SetProperty(_entry.AddedAt, value, _entry, (m, v) => m.AddedAt = v);
        }
        public Rect? Range
        {
            get => _entry.Range;
            set => SetProperty(_entry.Range, value, _entry, (m, v) => m.Range = v);
        }
    }

    public sealed class FavoriteImageArchiveItemEntry
    {
        [BsonId(true)]
        public ObjectId Id { get; init; }
        public string Path { get; init; }
        public int Page { get; init; }
        public DateTimeOffset AddedAt { get; set; }
        public string Label { get; set; } = "";
        public Rect? Range { get; set; }

    }

}

