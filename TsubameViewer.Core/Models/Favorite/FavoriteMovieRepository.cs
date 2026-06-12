using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using TsubameViewer.Core.Contracts.Models;
using Windows.Foundation;
#nullable enable
namespace TsubameViewer.Core.Models.Favorite;

public sealed class FavoriteMovieRepository
    : IFavoriteRepository
    , IFavoriteRepository<FavoriteMovieRepository.FavoriteMovieItemFacade>
{
    private readonly ILiteCollection<FavoriteMovieItemEntry> _col;

    public FavoriteMovieRepository(ILiteDatabase liteDatabase)
    {
        _col = liteDatabase.GetCollection<FavoriteMovieItemEntry>();
        _col.EnsureIndex(x => x.Path);
        _col.EnsureIndex(x => x.StartAt);
    }

    public bool IsFavoriteAny(string path)
    {
        return _col.Exists(x => x.Path.Equals(path, StringComparison.Ordinal));
    }

    public FavoriteMovieItemFacade AddFavorite(
        string path,
        string label,
        TimeSpan startAt,
        TimeSpan endAt,
        Rect range)
    {
        var entry = new FavoriteMovieItemEntry
        {
            Path = path,
            AddedAt = DateTimeOffset.Now,
            Label = label,
            StartAt = startAt,
            EndAt = endAt,
            Range = range
        };

        _col.Insert(entry);
        return new FavoriteMovieItemFacade(entry, _col);
    }

    public IEnumerable<FavoriteMovieItemFacade> GetFavorites(string path)
    {
        return _col.Find(x => x.Path == path && x.StartAt != null).Select(x => new FavoriteMovieItemFacade(x, _col));
    }

    IEnumerable<IFavoriteItemFacade> IFavoriteRepository.GetFavorites(string path)
    {
        return GetFavorites(path);
    }


    public sealed class FavoriteMovieItemFacade
        : DeferSaveAwareObservableObject
        , IFavoriteItemFacade
    {
        private readonly FavoriteMovieItemEntry _entry;
        private readonly ILiteCollection<FavoriteMovieItemEntry> _col;

        public FavoriteMovieItemFacade(FavoriteMovieItemEntry entry, ILiteCollection<FavoriteMovieItemEntry> col)
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

        public void SetRange(TimeSpan startAt, TimeSpan endAt, Rect range)
        {
            _entry.StartAt = startAt;
            _entry.EndAt = endAt;
            _entry.Range = range;
            TrySave();
        }

        public void ClearRange()
        {
            _entry.StartAt = null;
            _entry.EndAt = null;
            _entry.Range = null;
            TrySave();
        }
    }


    public sealed class FavoriteMovieItemEntry
    {
        [BsonId(true)]
        public ObjectId Id { get; init; }
        public string Path { get; init; } = "";
        public DateTimeOffset AddedAt { get; set; }
        public string Label { get; set; } = "";

        public TimeSpan? StartAt { get; set; }
        public TimeSpan? EndAt { get; set; }
        public Rect? Range { get; set; }
    }
}
