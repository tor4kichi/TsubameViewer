using System;
using TsubameViewer.Core.Models.ImageViewer;

namespace TsubameViewer.Core.Models.Albam;

public interface IAlbamImageSource : IImageSource
{
    Guid AlbamId { get; }
}
