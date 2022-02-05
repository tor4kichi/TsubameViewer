using I18NPortable;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsubameViewer.Models.Domain.Albam;
using TsubameViewer.Models.Domain.ImageViewer;
using TsubameViewer.Presentation.Services;
using TsubameViewer.Presentation.ViewModels.PageNavigation;

namespace TsubameViewer.Presentation.ViewModels.Albam.Commands
{
    public class AlbamItemEditCommand : ImageSourceCommandBase
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
       

        protected override async void Execute(IImageSource imageSource)
        {
            var albamSelectDialog = new Views.Dialogs.SelectItemDialog("ChoiceTargetAlbam".Translate(), "Apply".Translate());
            albamSelectDialog.XamlRoot = App.Current.XamlRoot;

            var albams = _albamRepository.GetAlbams();
            var existed = albams.Where(x => _albamRepository.IsExistAlbamItem(x._id, imageSource.Path)).ToList();
            albamSelectDialog.OptionButtonText = "CreateAlbam".Translate();
            albamSelectDialog.ItemsSource = albams;
            albamSelectDialog.DisplayMemberPath = nameof(AlbamEntry.Name);
            
            bool isCompleted = false;
            while (isCompleted is false)
            {
                albamSelectDialog.SetSelectedItems(existed);
                var result = await albamSelectDialog.ShowAsync();

                if (albamSelectDialog.IsOptionRequested)
                {
                    var (isSuccess, albamName) = await _albamDialogService.GetNewAlbamNameAsync();
                    if (isSuccess && string.IsNullOrWhiteSpace(albamName) is false)
                    {
                        if (string.IsNullOrEmpty(albamName) is false)
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
                                    createdAlbam = _albamRepository.CreateAlbam(Guid.NewGuid(), albamName);
                                }
                                catch { }
                            }

                            _albamRepository.AddAlbamItem(createdAlbam._id, imageSource.Path, imageSource.Name);
                        }
                        isCompleted = true;
                    }
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary 
                    && albamSelectDialog.GetSelectedItems() is not null and var selectedAlbams)
                {
                    var selectedAlbamsHash = selectedAlbams.Cast<AlbamEntry>().Select(x => x._id).ToHashSet();
                    var oldSelectedAlbamsHash = existed.Select(x => x._id).ToHashSet();

                    var removedAlbamIds = oldSelectedAlbamsHash.Except(selectedAlbamsHash);
                    var addedAlbamIds = selectedAlbamsHash.Except(oldSelectedAlbamsHash);

                    Debug.WriteLine($"prev selected albams : {string.Join(',', existed.Select(x => x.Name))}");
                    Debug.WriteLine($"selected albams : {string.Join(',', selectedAlbams.Select(x => (x as AlbamEntry).Name))}");

                    foreach (var albamId in removedAlbamIds)
                    {
                        _albamRepository.DeleteAlbamItem(albamId, imageSource.Path);
                    }

                    foreach (var albamId in addedAlbamIds)
                    {
                        _albamRepository.AddAlbamItem(albamId, imageSource.Path, imageSource.Name);
                    }

                    isCompleted = true;
                }
                else
                {
                    isCompleted = true;
                }

            }
        }

        protected override async void Execute(IEnumerable<IImageSource> imageSources)
        {
            var albamSelectDialog = new Views.Dialogs.SelectItemDialog("ChoiceTargetAlbam".Translate(), "Apply".Translate());
            albamSelectDialog.XamlRoot = App.Current.XamlRoot;

            var albams = _albamRepository.GetAlbams();
            var existed = albams.Where(x => imageSources.Any(image => _albamRepository.IsExistAlbamItem(x._id, image.Path))).ToList();
            albamSelectDialog.OptionButtonText = "CreateAlbam".Translate();
            albamSelectDialog.ItemsSource = albams;
            albamSelectDialog.DisplayMemberPath = nameof(AlbamEntry.Name);
            
            bool isCompleted = false;
            while (isCompleted is false)
            {
                albamSelectDialog.SetSelectedItems(existed);
                var result = await albamSelectDialog.ShowAsync();
                if (albamSelectDialog.IsOptionRequested)
                {
                    var (isSuccess, albamName) = await _albamDialogService.GetNewAlbamNameAsync();
                    if (isSuccess && string.IsNullOrWhiteSpace(albamName) is false)
                    {
                        if (string.IsNullOrEmpty(albamName) is false)
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
                                    createdAlbam = _albamRepository.CreateAlbam(Guid.NewGuid(), albamName);
                                }
                                catch { }
                            }

                            foreach (var imageSource in imageSources)
                            {
                                _albamRepository.AddAlbamItem(createdAlbam._id, imageSource.Path, imageSource.Name);
                            }
                        }
                        isCompleted = true;
                    }
                }
                else if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary 
                    && albamSelectDialog.GetSelectedItems() is not null and var selectedAlbams)
                {
                    var selectedAlbamsHash = selectedAlbams.Cast<AlbamEntry>().Select(x => x._id).ToHashSet();
                    var oldSelectedAlbamsHash = existed.Select(x => x._id).ToHashSet();

                    var removedAlbamIds = oldSelectedAlbamsHash.Except(selectedAlbamsHash);
                    var addedAlbamIds = selectedAlbamsHash.Except(oldSelectedAlbamsHash);

                    Debug.WriteLine($"prev selected albams : {string.Join(',', existed.Select(x => x.Name))}");
                    Debug.WriteLine($"selected albams : {string.Join(',', selectedAlbams.Select(x => (x as AlbamEntry).Name))}");

                    foreach (var albamId in removedAlbamIds)
                    {
                        foreach (var imageSource in imageSources)
                        {
                            _albamRepository.DeleteAlbamItem(albamId, imageSource.Path);
                        }
                    }

                    foreach (var albamId in addedAlbamIds)
                    {
                        foreach (var imageSource in imageSources)
                        {
                            _albamRepository.AddAlbamItem(albamId, imageSource.Path, imageSource.Name);
                        }
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
