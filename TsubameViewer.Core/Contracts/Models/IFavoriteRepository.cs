using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Core.Contracts.Models;


public interface IFavoriteRepository
{
    bool IsFavoriteAny(string path);
    IEnumerable<IFavoriteItemFacade> GetFavorites(string path);
}

public interface IFavoriteRepository<T> where T : IFavoriteItemFacade
{
    IEnumerable<T> GetFavorites(string path);
}

public interface IFavoriteItemFacade
{
    bool IsFavorite { get; set; }
    string Label { get; set; }
    DateTimeOffset AddedAt { get; set; }
}

