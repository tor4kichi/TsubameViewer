using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using TsubameViewer.Models.Domain;
using TsubameViewer.Models.Domain.SourceFolders;
using TsubameViewer.Presentation.Views;
using TsubameViewer.Presentation.Views.ViewManagement.Commands;
using VersOne.Epub;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Web.Http;

namespace TsubameViewer.Presentation.ViewModels
{
    public sealed class EBookReaderPageViewModel : ViewModelBase
    {
        private string _currentToken;
        private StorageFolder _tokenGettingFolder;

        private string _currentPath;
        private StorageFile _currentFolderItem;



        private int _CurrentImageIndex;
        public int CurrentImageIndex
        {
            get => _CurrentImageIndex;
            set => SetProperty(ref _CurrentImageIndex, value);
        }




        EpubBook _currentBook;

        private string _PageHtml;
        public string PageHtml
        {
            get { return _PageHtml; }
            set { SetProperty(ref _PageHtml, value); }
        }

        CompositeDisposable _navigationDisposables;

        private CancellationTokenSource _leavePageCancellationTokenSource;
        private readonly SourceStorageItemsRepository _sourceStorageItemsRepository;

        public EBookReaderPageViewModel(
            SourceStorageItemsRepository sourceStorageItemsRepository,
            ToggleFullScreenCommand toggleFullScreenCommand
            )
        {
            _sourceStorageItemsRepository = sourceStorageItemsRepository;
            ToggleFullScreenCommand = toggleFullScreenCommand;
        }


        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _leavePageCancellationTokenSource?.Cancel();
            _leavePageCancellationTokenSource?.Dispose();
            _leavePageCancellationTokenSource = null;

            _navigationDisposables.Dispose();
            _navigationDisposables = null;

            base.OnNavigatedFrom(parameters);
        }

        public override void OnNavigatingTo(INavigationParameters parameters)
        {
            PrimaryWindowCoreLayout.CurrentNavigationParameters = parameters;

            base.OnNavigatingTo(parameters);
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposables = new CompositeDisposable();
            _leavePageCancellationTokenSource = new CancellationTokenSource();
            var mode = parameters.GetNavigationMode();

            if (mode == NavigationMode.New
                || mode == NavigationMode.Forward
                || mode == NavigationMode.Back)
            {
                if (parameters.TryGetValue("token", out string token))
                {
                    if (_currentToken != token)
                    {
                        _currentPath = null;
                        _currentFolderItem = null;

                        _currentToken = token;

                        var item = await _sourceStorageItemsRepository.GetItemAsync(token);

                        _tokenGettingFolder = item as StorageFolder;

                        // ファイルアクティベーションなど
                        if (item is StorageFile file)
                        {
                            _currentFolderItem = file;
                        }
                    }
                }
                else
                {
                    throw new ArgumentException("EBookReaderPage was require 'token' parameter.");
                }

                if (parameters.TryGetValue("path", out string path))
                {
                    var unescapedPath = Uri.UnescapeDataString(path);
                    if (_currentPath != unescapedPath)
                    {
                        _currentPath = unescapedPath;
                        if (_tokenGettingFolder == null)
                        {
                            // token がファイルを指す場合は _currentFolderItem を通じて表示する
                            if (_currentFolderItem.Name != unescapedPath)
                            {
                                throw new Exception("token parameter is require for path parameter.");
                            }
                        }
                        else
                        {
                            var item = await FolderHelper.GetFolderItemFromPath(_tokenGettingFolder, _currentPath);

                            if (item is StorageFile file)
                            {
                                _currentFolderItem = file;
                            }
                            else
                            {
                                throw new ArgumentException("EBookReaderPage can not open StorageFolder.");
                            }
                        }
                    }
                }
            }

            
            if (_tokenGettingFolder != null || _currentFolderItem != null)
            {
                await RefreshItems(_leavePageCancellationTokenSource.Token);
            }

            // ページの切り替え
            this.ObserveProperty(x => x.CurrentImageIndex)
                .Subscribe(index => 
                {
                    var html = _currentBook.ReadingOrder.ElementAtOrDefault(index);
                    if (html == null) { throw new IndexOutOfRangeException(); }

                    Debug.WriteLine(html.FileName);

                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(html.Content);

                    var root = xmlDoc.DocumentElement;
                    Stack<XmlNode> _nodes = new Stack<XmlNode>();
                    _nodes.Push(root);
                    while (_nodes.Any())
                    {
                        var node = _nodes.Pop();

                        // 画像リソースの埋め込み
                        {
                            XmlAttribute imageSourceAttr = null;
                            if (node.Name == "img")
                            {
                                imageSourceAttr = node.Attributes["src"];
                            }
                            else if (node.Name == "image")
                            {
                                imageSourceAttr = node.Attributes["href"] ?? node.Attributes["xlink:href"];
                            }
                            if (imageSourceAttr != null)
                            {
                                foreach (var image in _currentBook.Content.Images)
                                {
                                    if (imageSourceAttr.Value.EndsWith(image.Key))
                                    {
                                        var base64Image = Convert.ToBase64String(image.Value.Content);
                                        var sb = new StringBuilder();
                                        sb.Append("data:");
                                        sb.Append(image.Value.ContentMimeType);
                                        sb.Append(";base64,");
                                        sb.Append(base64Image);
                                        imageSourceAttr.Value = sb.ToString();
                                    }
                                }
                            }
                        }

                        // cssの埋め込み
                        {
                            if (node.Name == "link"
                                && node.Attributes["type"]?.Value == "text/css"
                            )
                            {
                                var hrefAttr = node.Attributes["href"];
                                if (hrefAttr != null)
                                {
                                    var hrefValue = hrefAttr.Value.Split("/").Last();
                                    if (_currentBook.Content.Css.TryGetValue(hrefValue, out var cssContent))
                                    {
                                        var parent = node.ParentNode;
                                        parent.RemoveChild(node);
                                        var styleNode = xmlDoc.CreateElement("style");
                                        var typeAttr = xmlDoc.CreateAttribute("type");
                                        typeAttr.Value = cssContent.ContentMimeType;
                                        styleNode.Attributes.Append(typeAttr);
                                        styleNode.InnerText = cssContent.Content;
                                        parent.AppendChild(styleNode);
                                    }
                                }
                            }
                        }

                        foreach (var child in node.ChildNodes)
                        {
                            _nodes.Push(child as XmlNode);
                        }
                    }

                    using (var stringWriter = new StringWriter())
                    using (var xmlTextWriter = XmlWriter.Create(stringWriter))
                    {
                        xmlDoc.WriteTo(xmlTextWriter);
                        xmlTextWriter.Flush();
                        PageHtml = stringWriter.GetStringBuilder().ToString();
                    }
                })
                .AddTo(_navigationDisposables);

            await base.OnNavigatedToAsync(parameters);
        }


        private async Task RefreshItems(CancellationToken ct)
        {
            using var fileStream = await _currentFolderItem.OpenStreamForReadAsync();

            var epubBook = await EpubReader.ReadBookAsync(fileStream);
            _currentBook = epubBook;

            Debug.WriteLine(epubBook.Title);

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        public HttpResponseMessage ResolveWebResourceRequest(Uri requestUri)
        {
            Debug.WriteLine("WebView: " + requestUri.OriginalString);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }





        #region Commands

        public ToggleFullScreenCommand ToggleFullScreenCommand { get; }

        private DelegateCommand _GoNextImageCommand;
        public DelegateCommand GoNextImageCommand =>
            _GoNextImageCommand ??= new DelegateCommand(ExecuteGoNextImageCommand, CanGoNextCommand);

        private void ExecuteGoNextImageCommand()
        {
            CurrentImageIndex += 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoNextCommand()
        {
            return _currentBook?.ReadingOrder.Count > CurrentImageIndex + 1;
        }

        private DelegateCommand _GoPrevImageCommand;
        public DelegateCommand GoPrevImageCommand =>
            _GoPrevImageCommand ??= new DelegateCommand(ExecuteGoPrevImageCommand, CanGoPrevCommand);

        private void ExecuteGoPrevImageCommand()
        {
            CurrentImageIndex -= 1;

            GoNextImageCommand.RaiseCanExecuteChanged();
            GoPrevImageCommand.RaiseCanExecuteChanged();
        }

        private bool CanGoPrevCommand()
        {
            return _currentBook?.ReadingOrder.Count >= 2 && CurrentImageIndex > 0;
        }



        private DelegateCommand _SizeChangedCommand;
        public DelegateCommand SizeChangedCommand =>
            _SizeChangedCommand ??= new DelegateCommand(async () =>
            {
                throw new NotImplementedException();
                //if (!(Images?.Any() ?? false)) { return; }

                //await Task.Delay(50);

                //RaisePropertyChanged(nameof(CurrentImageIndex));
            });


        #endregion
    }
}
