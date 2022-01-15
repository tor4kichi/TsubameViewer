using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    internal class AlbamItemEditCommand : DelegateCommandBase
    {
        private readonly IMessenger _messenger;
        private readonly AlbamRepository _albamRepository;
        private readonly AlbamDialogService _albamDialogService;

        public AlbamItemEditCommand(
            IMessenger messenger,
            AlbamRepository albamRepository,
            AlbamDialogService albamDialogService
            )
        {
            _albamRepository = albamRepository;
            _messenger = messenger;
            _albamDialogService = albamDialogService;
        }
        protected override bool CanExecute(object parameter)
        {
            return parameter is StorageItemViewModel;
        }

        protected override async void Execute(object parameter)
        {
            if (parameter is StorageItemViewModel itemVM)
            {
                var albamSelectDialog = new Views.Dialogs.SelectItemDialog("ChoiceTargetAlbam".Translate(), "Apply".Translate());

                var albams = _albamRepository.GetAlbams();
                var existed = albams.Where(x => _albamRepository.IsExistAlbamItem(x._id, itemVM.Path)).ToList();
                albamSelectDialog.ItemsSource = albams;
                albamSelectDialog.DisplayMemberPath = nameof(AlbamEntry.Name);
                albamSelectDialog.SetSelectedItems(existed);

                bool isCompleted = false;
                while (isCompleted is false)
                {
                    await albamSelectDialog.ShowAsync();

                    if (albamSelectDialog.IsOptionRequrested)
                    {
                        var title = await _albamDialogService.GetAlbamTitleAsync();
                        if (title != null)
                        {
                            if (string.IsNullOrEmpty(title) is false)
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

                                _albamRepository.AddAlbamItem(createdAlbam._id, itemVM.Path);
                            }
                            isCompleted = true;
                        }
                    }
                    else if (albamSelectDialog.GetSelectedItems() is not null and var selectedAlbams && selectedAlbams.Any())
                    {

                        //foreach (var albam in albams)
                        //{
                        //    _albamRepository.DeleteAlbamItem(albam._id, itemVM.Path);
                        //}

                        //foreach (var selectAlbam in selectedAlbams.Cast<AlbamEntry>())
                        //{
                        //    _albamRepository.AddAlbamItem(selectAlbam._id, itemVM.Path);
                        //}


                        var selectedAlbamsHash = selectedAlbams.Cast<AlbamEntry>().Select(x => x._id).ToHashSet();
                        var oldSelectedAlbamsHash = existed.Select(x=> x._id).ToHashSet();

                        var removedAlbamIds = oldSelectedAlbamsHash.Except(selectedAlbamsHash);
                        var addedAlbamIds = selectedAlbamsHash.Except(oldSelectedAlbamsHash);

                        Debug.WriteLine($"prev selected albams : {string.Join(',', existed.Select(x => x.Name))}");
                        Debug.WriteLine($"selected albams : {string.Join(',', selectedAlbams.Select(x => (x as AlbamEntry).Name))}");

                        foreach (var albamId in removedAlbamIds)
                        {
                            _albamRepository.DeleteAlbamItem(albamId, itemVM.Path);
                        }

                        foreach (var albamId in addedAlbamIds)
                        {
                            _albamRepository.AddAlbamItem(albamId, itemVM.Path);
                        }

                        isCompleted = true;
                    }
                    else
                    {
                        isCompleted = true;
                    }

                }
            }
        }
    }
}
