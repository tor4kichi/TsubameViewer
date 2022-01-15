using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using TsubameViewer.Models.Domain.Albam;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public sealed class AlbamCreateCommand : DelegateCommandBase
    {
        private readonly AlbamRepository _albamRepository;
        private readonly IMessenger _messenger;

        public AlbamCreateCommand(
            AlbamRepository albamRepository,
            IMessenger messenger
            )
        {
            _albamRepository = albamRepository;
            _messenger = messenger;
        }

        protected override bool CanExecute(object parameter)
        {
            return true;
        }

        protected override async void Execute(object parameter)
        {
            var textInputDialog = new Views.Dialogs.TextInputDialog("CreateAlbam".Translate(), "CreateAlbam_Placeholder".Translate(), "Create".Translate());
            await textInputDialog.ShowAsync();
            if (textInputDialog.GetInputText() is not null and var title && string.IsNullOrEmpty(title) is false)
            {
                AlbamEntry createdAlbam = null;

                // Guidの衝突可能性を潰すべく数回リトライする
                int count = 0;
                while (createdAlbam == null)
                {
                    if (++count >= 5)
                    {
                        throw new InvalidOperationException();
                    }

                    try
                    {
                        createdAlbam = _albamRepository.CreateAlbam(Guid.NewGuid(), title);
                    }
                    catch { }
                }

                _messenger.Send(new AlbamCreatedMessage(createdAlbam));
            }
        }
    }
}
