using LiteDB;
using SharpCompress.Compressors.ZStandard.Unsafe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Contracts.Models;
using static TsubameViewer.Core.Models.Favorite.FavoriteEBookRepository;
#nullable enable
namespace TsubameViewer.Core.Models.Favorite;

public sealed class FavoriteEBookRepository
    : IFavoriteRepository
    , IFavoriteRepository<FavoriteEBookItemFacade>
{
    private readonly ILiteCollection<FavoriteEBookItemEntry> _col;

    public FavoriteEBookRepository(ILiteDatabase liteDatabase)
    {
        _col = liteDatabase.GetCollection<FavoriteEBookItemEntry>();
        _col.EnsureIndex(x => x.Path);
    }

    public bool IsFavoriteAny(string path)
    {
        return _col.Exists(x => x.Path.Equals(path, StringComparison.Ordinal));
    }

    public FavoriteEBookItemFacade AddFavorite(
        string path,
        string label,
        string htmlXPath,
        int highlightLength)
    {
        var entry = new FavoriteEBookItemEntry
        {
            Path = path,
            AddedAt = DateTimeOffset.Now,
            Label = label,
            HtmlXPath = htmlXPath,
            HighlightLength = highlightLength,
        };

        _col.Insert(entry);
        return new FavoriteEBookItemFacade(entry, _col);
    }

    public IEnumerable<FavoriteEBookItemFacade> GetFavorites(string path)
    {
        return _col.Find(x => x.Path.Equals(path, StringComparison.Ordinal) && x.FileName != null).Select(x => new FavoriteEBookItemFacade(x, _col));
    }

    IEnumerable<IFavoriteItemFacade> IFavoriteRepository.GetFavorites(string path)
    {
        return GetFavorites(path);
    }


    public sealed class FavoriteEBookItemFacade
        : DeferSaveAwareObservableObject
        , IFavoriteItemFacade
    {
        private readonly FavoriteEBookItemEntry _entry;
        private readonly ILiteCollection<FavoriteEBookItemEntry> _col;

        public FavoriteEBookItemFacade(FavoriteEBookItemEntry entry, ILiteCollection<FavoriteEBookItemEntry> col)
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
        public string? HtmlXPath
        {
            get => _entry.HtmlXPath;
            set => SetProperty(_entry.HtmlXPath, value, _entry, (m, v) => m.HtmlXPath = v);
        }
        public int HighlightLength
        {
            get => _entry.HighlightLength;
            set => SetProperty(_entry.HighlightLength, value, _entry, (m, v) => m.HighlightLength = v);
        }
    }


    public sealed class FavoriteEBookItemEntry
    {
        [BsonId(true)]
        public ObjectId Id { get; init; }
        public string Path { get; init; } = "";
        public string FileName { get; init; } = null;
        public DateTimeOffset AddedAt { get; set; }
        public string Label { get; set; } = "";
        public string HtmlXPath { get; set; } = null;
        public int HighlightLength { get; set; } = 0;
    }
}
