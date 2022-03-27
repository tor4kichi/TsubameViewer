using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamItemAddCommand : ImageSourceCommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly AlbamEntry _albamEntry;

        public AlbamItemAddCommand(
            AlbamRepository albamRepository,
            AlbamEntry albamEntry
            )
        {
            _albamRepository = albamRepository;
            _albamEntry = albamEntry;
        }

        
        protected override void Execute(IImageSource imageSource)
        {
            if (_albamRepository.IsExistAlbamItem(_albamEntry._id, imageSource.Path))
            {
                _albamRepository.DeleteAlbamItem(_albamEntry._id, imageSource.Path, imageSource.GetAlbamItemType());
            }
            else
            {
                _albamRepository.AddAlbamItem(_albamEntry._id, imageSource.Path, imageSource.Name, imageSource.GetAlbamItemType());
            }
        }

        protected override void Execute(IEnumerable<IImageSource> imageSources)
        {
            base.Execute(imageSources.ToArray());
        }
    }
}
