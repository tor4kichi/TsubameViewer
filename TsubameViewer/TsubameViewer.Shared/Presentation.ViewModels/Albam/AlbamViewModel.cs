using I18NPortable;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.UseCase;

namespace TsubameViewer.Presentation.ViewModels.Albam
{
    public sealed class AlbamViewModel : ObservableObject
    {
        private readonly AlbamEntry _albam;

        public bool IsFavoriteAlbam => _albam._id == FavoriteAlbam.FavoriteAlbamId;

        public Guid AlbamId => _albam._id;

        public AlbamViewModel(AlbamEntry albam)
        {
#if DEBUG
            Guard.IsNotNull(albam, nameof(albam));
#endif
            _albam = albam;
        }

        public string Name => IsFavoriteAlbam ? "FavoriteAlbam".Translate() : _albam.Name;
    }
}
