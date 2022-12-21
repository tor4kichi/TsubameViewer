﻿using I18NPortable;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using TsubameViewer.Core.Models.Albam;
using TsubameViewer.Core.Models.ImageViewer;
using TsubameViewer.Core.Models.ImageViewer.ImageSource;
using TsubameViewer.Services;
using TsubameViewer.ViewModels.PageNavigation;

namespace TsubameViewer.ViewModels.Albam.Commands
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

            var itemType = imageSource.GetAlbamItemType();
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

                            _albamRepository.AddAlbamItem(createdAlbam._id, imageSource.Path, imageSource.Name, imageSource.GetAlbamItemType());
                        }
                        isCompleted = true;
                    }
                }
                else if (result == Windows.UI.Xaml.Controls.ContentDialogResult.Primary 
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
                        _albamRepository.DeleteAlbamItem(albamId, imageSource.Path, itemType);
                    }

                    foreach (var albamId in addedAlbamIds)
                    {
                        _albamRepository.AddAlbamItem(albamId, imageSource.Path, imageSource.Name, itemType);
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
                                _albamRepository.AddAlbamItem(createdAlbam._id, imageSource.Path, imageSource.Name, imageSource.GetAlbamItemType());
                            }
                        }
                        isCompleted = true;
                    }
                }
                else if (result == Windows.UI.Xaml.Controls.ContentDialogResult.Primary 
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
                            _albamRepository.DeleteAlbamItem(albamId, imageSource.Path, imageSource.GetAlbamItemType());
                        }
                    }

                    foreach (var albamId in addedAlbamIds)
                    {
                        foreach (var imageSource in imageSources)
                        {
                            _albamRepository.AddAlbamItem(albamId, imageSource.Path, imageSource.Name, imageSource.GetAlbamItemType());
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
