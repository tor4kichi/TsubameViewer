using ColorCode.Compilation.Languages;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using TsubameViewer.Core.Helpers;
using TsubameViewer.Core.Models.EBook;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

#nullable enable
namespace TsubameViewer.Views.EBookControls;

public sealed partial class EPubRenderer : UserControl
{


    public bool IsVerticalLayout
    {
        get { return (bool)GetValue(IsVerticalLayoutProperty); }
        private set { SetValue(IsVerticalLayoutProperty, value); }
    }

    public static readonly DependencyProperty IsVerticalLayoutProperty =
        DependencyProperty.Register("IsVerticalLayout", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));





    public bool NowRightToLeftReadingMode
    {
        get { return (bool)GetValue(NowRightToLeftReadingModeProperty); }
        set { SetValue(NowRightToLeftReadingModeProperty, value); }
    }

    public static readonly DependencyProperty NowRightToLeftReadingModeProperty =
        DependencyProperty.Register("NowRightToLeftReadingMode", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));


    public bool NowOnlyImageView
    {
        get { return (bool)GetValue(NowOnlyImageViewProperty); }
        private set { SetValue(NowOnlyImageViewProperty, value); }
    }

    public static readonly DependencyProperty NowOnlyImageViewProperty =
        DependencyProperty.Register("NowOnlyImageView", typeof(bool), typeof(EPubRenderer), new PropertyMetadata(false));



    public Color PageBackgroundColor
    {
        get { return (Color)GetValue(PageBackgroundColorProperty); }
        set { SetValue(PageBackgroundColorProperty, value); }
    }

    public static readonly DependencyProperty PageBackgroundColorProperty =
        DependencyProperty.Register("PageBackgroundColor", typeof(Color), typeof(EPubRenderer), new PropertyMetadata(Colors.Transparent));




    public int TotalInnerPageCount
    {
        get { return (int)GetValue(TotalInnerPageCountProperty); }
        private set { SetValue(TotalInnerPageCountProperty, value); }
    }

    public static readonly DependencyProperty TotalInnerPageCountProperty =
        DependencyProperty.Register("TotalInnerPageCount", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));




    public int CurrentInnerPage
    {
        get { return (int)GetValue(CurrentInnerPageProperty); }
        private set { SetValue(CurrentInnerPageProperty, value); }
    }

    public static readonly DependencyProperty CurrentInnerPageProperty =
        DependencyProperty.Register("CurrentInnerPage", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));





    public int FirstApproachingPageIndex
    {
        get { return (int)GetValue(FirstApproachingPageIndexProperty); }
        set { SetValue(FirstApproachingPageIndexProperty, value); }
    }

    public static readonly DependencyProperty FirstApproachingPageIndexProperty =
        DependencyProperty.Register("FirstApproachingPageIndex", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));



    public int PreservedCurrentInnerPageIndex
    {
        get { return (int)GetValue(PreservedCurrentInnerPageIndexProperty); }
        set { SetValue(PreservedCurrentInnerPageIndexProperty, value); }
    }

    public static readonly DependencyProperty PreservedCurrentInnerPageIndexProperty =
        DependencyProperty.Register("PreservedCurrentInnerPageIndex", typeof(int), typeof(EPubRenderer), new PropertyMetadata(0));







    public int ColumnCount
    {
        get { return (int)GetValue(ColumnCountProperty); }
        set { SetValue(ColumnCountProperty, value); }
    }

    public static readonly DependencyProperty ColumnCountProperty =
        DependencyProperty.Register("ColumnCount", typeof(int), typeof(EPubRenderer), new PropertyMetadata(1, OnSomeStylePropertyChanged));


    public string ContentsFontFamily
    {
        get { return (string)GetValue(ContentsFontFamilyProperty); }
        set { SetValue(ContentsFontFamilyProperty, value); }
    }

    public static readonly DependencyProperty ContentsFontFamilyProperty =
        DependencyProperty.Register("ContentsFontFamily", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null, OnSomeStylePropertyChanged));



    public string RubyFontFamily
    {
        get { return (string)GetValue(RubyFontFamilyProperty); }
        set { SetValue(RubyFontFamilyProperty, value); }
    }

    public static readonly DependencyProperty RubyFontFamilyProperty =
        DependencyProperty.Register("RubyFontFamily", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null, OnSomeStylePropertyChanged));





    public double LetterSpacingInPixel
    {
        get { return (double)GetValue(LetterSpacingInPixelProperty); }
        set { SetValue(LetterSpacingInPixelProperty, value); }
    }

    public static readonly DependencyProperty LetterSpacingInPixelProperty =
        DependencyProperty.Register("LetterSpacingInPixel", typeof(double), typeof(EPubRenderer), new PropertyMetadata(0.0, OnSomeStylePropertyChanged));





    public double LineHeightInNoUnit
    {
        get { return (double)GetValue(LineHeightInNoUnitProperty); }
        set { SetValue(LineHeightInNoUnitProperty, value); }
    }

    public static readonly DependencyProperty LineHeightInNoUnitProperty =
        DependencyProperty.Register("LineHeightInNoUnit", typeof(double), typeof(EPubRenderer), new PropertyMetadata(1.5, OnSomeStylePropertyChanged));




    public double RubyFontSizeInPixel
    {
        get { return (double)GetValue(RubyFontSizeInPixelProperty); }
        set { SetValue(RubyFontSizeInPixelProperty, value); }
    }

    public static readonly DependencyProperty RubyFontSizeInPixelProperty =
        DependencyProperty.Register("RubyFontSizeInPixel", typeof(double), typeof(EPubRenderer), new PropertyMetadata(10.0, OnSomeStylePropertyChanged));




    public Color FontColor
    {
        get { return (Color)GetValue(FontColorProperty); }
        set { SetValue(FontColorProperty, value); }
    }

    public static readonly DependencyProperty FontColorProperty =
        DependencyProperty.Register("FontColor", typeof(Color), typeof(EPubRenderer), new PropertyMetadata(Colors.Transparent, OnSomeStylePropertyChanged));




    public WritingMode OverrideWritingMode
    {
        get { return (WritingMode)GetValue(OverrideWritingModeProperty); }
        set { SetValue(OverrideWritingModeProperty, value); }
    }

    public static readonly DependencyProperty OverrideWritingModeProperty =
        DependencyProperty.Register("OverrideWritingMode", typeof(WritingMode), typeof(EPubRenderer), new PropertyMetadata(WritingMode.Inherit, OnSomeStylePropertyChanged));

    public string PageHtml
    {
        get { return (string)GetValue(PageHtmlProperty); }
        set { SetValue(PageHtmlProperty, value); }
    }

    public static readonly DependencyProperty PageHtmlProperty =
        DependencyProperty.Register("PageHtml", typeof(string), typeof(EPubRenderer), new PropertyMetadata(null, OnPageHtmlPropertyChanged ));

    private static async void OnPageHtmlPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var _this = (EPubRenderer)d;

        using var _ = await _this._domUpdateLock.LockAsync(default);

        if (_this.isFirstContent)
        {
            await Task.Delay(100);
            //await Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
            //    h => _this.SizeChanged += h,
            //    h => _this.SizeChanged -= h
            //    )
            //    .Take(1)
            //    .ToAsyncAction()
            //    .AsTask();
        }
        
        _this._sw.Restart();
        if (e.NewValue is string newPageHtml && !string.IsNullOrEmpty(newPageHtml))
        {
            _this.ContentRefreshStarting?.Invoke(_this, EventArgs.Empty);
            _this.WebView.NavigateToString(_this.ToStyleEmbedHtml(newPageHtml));
        }
        else
        {
            _this.WebView.NavigateToString(string.Empty);
        }
    }

    private readonly Stopwatch _sw = new Stopwatch();

    private static void OnSomeStylePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((EPubRenderer)d).ResetEmbedStyleText();
    }


    string? _embedStyleText;

    private void ResetEmbedStyleText()
    {
        _embedStyleText = null;
    }
    private string GetOrCreateEmbedStyleText()
    {
        return _embedStyleText ??= MakeEmbedStyleText();
    }
    private string MakeEmbedStyleText()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("html, body {");
        if (OverrideWritingMode == WritingMode.Horizontal_TopToBottom)
        {
            sb.Append($"writing-mode: horizontal-tb !important;");
        }
        else if (OverrideWritingMode == WritingMode.Vertical_RightToLeft)
        {
            sb.Append($"writing-mode: vertical-rl !important;");
        }
        else if (OverrideWritingMode == WritingMode.Vertical_LeftToRight)
        {
            sb.Append($"writing-mode: vertical-lr !important;");
        }
        sb.Append("}");

        sb.Append("body, p, span, strong, small, h1, h2, h3, h4, h5, h6{");
        sb.Append($"letter-spacing: {LetterSpacingInPixel}px !important;");
        sb.Append($"line-height: {LineHeightInNoUnit} !important;");
        if (ContentsFontFamily != null)
        {
            sb.Append($"font-family: \"{ContentsFontFamily}\" !important;");
        }
        if (FontColor != Colors.Transparent)
        {
            var color = FontColor;
            color.A = 0xff;
            sb.Append($"color: rgba({color.R},{color.G},{color.B},{color.A}) !important;");
        }

        sb.Append("}");

        if (FontColor != Colors.Transparent)
        {
            var color = FontColor;
            color.A = 0xff;
            sb.Append($"a:link {{ color: rgba({color.R},{color.G},{color.B},{color.A}) !important; }}");
            sb.Append($"a:visited {{ color: rgba({color.R},{color.G},{color.B},{color.A}) !important; }}");
            sb.Append($"a:hover {{ color: rgba({color.R},{color.G},{color.B},{color.A}) !important; }}");
            sb.Append($"a:active {{ color: rgba({color.R},{color.G},{color.B},{color.A}) !important; }}");
        }

        if (ColumnCount > 1)
        {
            sb.Append(@"
                   img, image {
                    max-height: 100%!important;
                    max-width: 100%!important;
                    margin-left: 32px!important;
                    margin-right: 32px!important;

                    display: block!important;
                    break-before: always!important;
                    break-after: always!important;
                    break-inside: avoid!important;
                }
                ");
        }
        else // if (ColumnCount == 1)
        {
            sb.Append(@"
                   img, image {
                    max-height: 100vh!important;
                    max-width: 100%!important;
                    margin-left: 32px!important;
                    margin-right: 32px!important;

                    display: block!important;
                    break-before: always!important;
                    break-after: always!important;
                    break-inside: avoid!important;
                }
                ");
        }


        sb.Append("rt {");
        sb.Append($"font-size: {RubyFontSizeInPixel}px !important;");
        if (RubyFontFamily != null)
        {
            sb.Append($"font-family: \"{RubyFontFamily}\" !important;");
        }
        else if (ContentsFontFamily != null)
        {
            sb.Append($"font-family: \"{ContentsFontFamily}\" !important;");
        }
        sb.Append("}");
        return sb.ToString();
    }

    // evalでdocument.headにstyleタグを追加しても反映されないため
    // html文字列に直接埋め込む
    string ToStyleEmbedHtml(string pageHtml)
    {
        string ePubRendererCss = GetOrCreateEmbedStyleText();

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(pageHtml);

        var head = xmlDoc.DocumentElement["head"];
        var cssItems = new[] { ePubRendererCss };
        foreach (var css in cssItems)
        {
            var cssNode = xmlDoc.CreateElement("style");
            var typeAttr = xmlDoc.CreateAttribute("type");
            typeAttr.Value = "text/css";
            cssNode.Attributes.Append(typeAttr);
            cssNode.InnerText = css;
            head.AppendChild(cssNode);
        }

        //{
        //    StringBuilder sb = new StringBuilder();
        //    AppendAllScript(sb);
        //    var scriptNode = xmlDoc.CreateElement("script");
        //    var typeAttr = xmlDoc.CreateAttribute("type");
        //    typeAttr.Value = "text/javascript";
        //    scriptNode.Attributes.Append(typeAttr);
        //    scriptNode.InnerText = sb.ToString();
        //    head.AppendChild(scriptNode);
        //}
        StringBuilder sb = new StringBuilder();
        foreach (var appendScript in _scriptsAppender)
        {
            sb.Clear();
            appendScript(sb);
            var scriptNode = xmlDoc.CreateElement("script");
            var typeAttr = xmlDoc.CreateAttribute("type");
            typeAttr.Value = "text/javascript";
            scriptNode.Attributes.Append(typeAttr);
            scriptNode.InnerText = sb.ToString();
            head.AppendChild(scriptNode);
        }

        var body = xmlDoc.DocumentElement["body"];
        var tailMarkerElement = xmlDoc.CreateElement("span");
        tailMarkerElement.SetAttribute("id", "tv_tail_markar");
        body.AppendChild(tailMarkerElement);

        using (var stringWriter = new StringWriter())
        using (var xmlTextWriter = XmlWriter.Create(stringWriter))
        {
            xmlDoc.WriteTo(xmlTextWriter);
            xmlTextWriter.Flush();
            return stringWriter.GetStringBuilder().ToString();
        }
    }

    public EPubRenderer()
    {
        this.InitializeComponent();

        Loaded += EPubRenderer_Loaded;
        Unloaded += EPubRenderer_Unloaded;            
    }



    CompositeDisposable? _compositeDisposable;

    private void EPubRenderer_Loaded(object sender, RoutedEventArgs e)
    {
        WebView.NavigationStarting -= WebView_NavigationStarting;
        WebView.NavigationCompleted -= WebView_NavigationCompleted;
        WebView.DOMContentLoaded -= WebView_DOMContentLoaded;
        WebView.NavigationStarting += WebView_NavigationStarting;
        WebView.NavigationCompleted += WebView_NavigationCompleted;
        WebView.DOMContentLoaded += WebView_DOMContentLoaded;

        _compositeDisposable = new CompositeDisposable();
        var dispatcher = Dispatcher;
        
        isFirstContent = true;

        Observable.Merge(
            Observable.FromEventPattern<WindowSizeChangedEventHandler, WindowSizeChangedEventArgs>(
                h => Window.Current.SizeChanged += h,
                h => Window.Current.SizeChanged -= h
                ).ToUnit()
            , Observable.FromEventPattern<SizeChangedEventHandler, SizeChangedEventArgs>(
                h => SizeChanged += h,
                h => SizeChanged -= h
                ).ToUnit()
            )
            .Where(x => !isFirstContent)
            .Throttle(TimeSpan.FromMilliseconds(100), CurrentThreadScheduler.Instance)
            .Where(x => !isFirstContent)
            .Where(x => this.Visibility == Visibility.Visible)
            .Subscribe(async args =>
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => 
                {
                    await Task.Delay(50);
                    if (IsLoaded is false) { return; }
                    
                    ContentRefreshStarting?.Invoke(this, EventArgs.Empty);

                    using (await _domUpdateLock.LockAsync(default))
                    {
                        // WebView内部のリサイズが完了してからリサイズさせることで表示崩れを防ぐ
                        //await Task.Delay(50);

                        // リサイズしたら再描画しないとレイアウトが崩れるっぽい
                        WebView.Refresh();
                    }
                });
            })
            .AddTo(_compositeDisposable);

        new[]
        {
            this.ObserveDependencyProperty(FontSizeProperty),
            this.ObserveDependencyProperty(LetterSpacingInPixelProperty),
            this.ObserveDependencyProperty(LineHeightInNoUnitProperty),
            this.ObserveDependencyProperty(RubyFontSizeInPixelProperty),
            this.ObserveDependencyProperty(ContentsFontFamilyProperty),
            this.ObserveDependencyProperty(RubyFontFamilyProperty),
            this.ObserveDependencyProperty(FontColorProperty),
            this.ObserveDependencyProperty(OverrideWritingModeProperty),
            this.ObserveDependencyProperty(ColumnCountProperty),
        }
        .Merge()
        .Throttle(TimeSpan.FromMilliseconds(50))
        .Subscribe(_ => { var __ = dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => ReloadPageHtml()); })
        .AddTo(_compositeDisposable);
    }

    private void EPubRenderer_Unloaded(object sender, RoutedEventArgs e)
    {
        WebView.NavigationStarting -= WebView_NavigationStarting;
        WebView.NavigationCompleted -= WebView_NavigationCompleted;
        WebView.DOMContentLoaded -= WebView_DOMContentLoaded;

        _compositeDisposable?.Dispose();
    }



    private void WebView_WebResourceRequested(WebView sender, WebViewWebResourceRequestedEventArgs args)
    {
        WebResourceRequested?.Invoke(this, args);
    }

    public event EventHandler<WebViewWebResourceRequestedEventArgs> WebResourceRequested;




    private async void WebView_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
    {
        //using var _ = await _domUpdateLock.LockAsync(default);
    }

    private async void WebView_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
    {
        using var _ = await _domUpdateLock.LockAsync(default);
        ContentRefreshComplete?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ContentRefreshStarting;
    public event EventHandler? ContentRefreshComplete;

    Core.AsyncLock _domUpdateLock = new ();

    bool isFirstContent = true;
    int _innerCurrentPage;
    int _innerPageCount;
    int _webViewScrollableSize;
    double _onePageScrollSize;


    public bool CanGoNext()
    {
        return _innerCurrentPage + 1 < _innerPageCount;
    }

    public async void GoNext()
    {
        using var _ = await _domUpdateLock.LockAsync(default);

        if (!CanGoNext()) { throw new Exception(); }

        _innerCurrentPage++;
        CurrentInnerPage = _innerCurrentPage;
        Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
        await SetScrollPositionAsync();
    }


    public bool CanGoPreview()
    {
        return _innerCurrentPage > 0;
    }

    public async void GoPreview()
    {
        using var _ = await _domUpdateLock.LockAsync(default);

        if (!CanGoPreview()) { throw new Exception(); }

        _innerCurrentPage--;
        CurrentInnerPage = _innerCurrentPage;
        Debug.WriteLine($"InnerPage: {_innerCurrentPage}/{_innerPageCount}");
        await SetScrollPositionAsync();
    }


    public void Refresh()
    {
        WebView.Refresh();
    }


    private void ReloadPageHtml()
    {
        ContentRefreshStarting?.Invoke(this, EventArgs.Empty);
        WebView.NavigateToString(this.ToStyleEmbedHtml(PageHtml));
    }


    public void PrepareTocSelectionChange()
    {
        PreservedCurrentInnerPageIndex = 0;
        _isGoNextOrPreview = true;
    }


    public void PrepareGoNext()
    {
        PreservedCurrentInnerPageIndex = 0;
        _isGoNextOrPreview = true;
    }

    public void PrepareGoPreview()
    {
        PreservedCurrentInnerPageIndex = int.MaxValue;
        _isGoNextOrPreview = true;
    }

    private bool _isGoNextOrPreview;


    static Action<StringBuilder>[] _scriptsAppender = new[] 
    { 
        AppendGetWritingModeScript,
        AppendSetVerticalBodyStyleScript,
        AppendSetHorizontalBodyStyleScript,
        AppendGetSizeListScript,
    };

    private StringBuilder AppendAllScript(StringBuilder sb)
    {
        foreach (var appendScript in _scriptsAppender)
        {
            appendScript(sb);            
        }
        return sb;
    }


    // javascript のテンプレートリテラルを利用する場合は バッククオート ` で 文字列を囲み、変数は ${} で囲む
    // see@ https://developer.mozilla.org/ja/docs/Web/JavaScript/Reference/Template_literals
    // WebView は ES5 のため、let const などが使えない
    // < や > を使うとパースエラーが発生する

    private static void AppendGetWritingModeScript(StringBuilder sb)
    {
        sb.AppendLine(@"function GetWritingMode() { return window.getComputedStyle(document.body).getPropertyValue('writing-mode'); }");
    }
    private async Task<string> GetWritingModeAsync()
    {
        return await WebView.InvokeScriptAsync("GetWritingMode", null);
    }

    private static void AppendSetVerticalBodyStyleScript(StringBuilder sb)
    {
        sb.AppendLine(
@"function SetVerticalBodyStyle(webViewHeight, columnCount, fontSize) {
document.body.style = `width: 100vw; overflow: hidden; max-height: ${webViewHeight}px; column-count: ${columnCount}; column-rule-width: 0px; column-gap: 1em; font-size: ${fontSize}px;`;
}"
);        
    }

    private async Task SetVerticalBodyStyleAsync(double height, int columnCount, double fontSize)
    {
        await WebView.InvokeScriptAsync("SetVerticalBodyStyle", new [] { height.ToString("F0"), columnCount.ToString(), fontSize.ToString("F0") });
    }

    private static void AppendSetHorizontalBodyStyleScript(StringBuilder sb)
    {
        sb.AppendLine(
@"function SetHorizontalBodyStyle(webViewWidth, webViewHeight, columnCount, fontSize){
document.body.style = `overflow: hidden; width: ${webViewWidth}; max-height: ${webViewHeight}px; column-count: ${columnCount}; column-rule-width: 0px; column-gap: 1em; font-size:${fontSize}px;`;
};"
);
    }

    private async Task SetHorizontalBodyStyleAsync(double webViewWidth, double webViewHeight, int columnCount, double fontSize)
    {
        await WebView.InvokeScriptAsync("SetHorizontalBodyStyle", new[] { webViewWidth.ToString("F0"), webViewHeight.ToString("F0"), columnCount.ToString(), fontSize.ToString("F0") });
    }


    private static void AppendGetSizeListScript(StringBuilder sb)
    {        
        sb.AppendLine(
@"function GetSizeList(isVerticalString)
{
let isVertical = JSON.parse(isVerticalString.toLowerCase()) == true;
var heightArray = [];
var count = 0;    
let pList = document.body.getElementsByTagName(`p`);
let divList = document.body.getElementsByTagName(`div`);
let spanList = document.body.getElementsByTagName(`span`);
let imgList = document.body.getElementsByTagName(`img`);
let allElmeentList = [...pList, ...divList, ...spanList, ...imgList];
let set = new Set();
for (var item of allElmeentList)
{
    var elementScrollTop = isVertical ? item.offsetTop : item.offsetLeft;
    if (elementScrollTop == null) { continue; }

    if (!set.has(elementScrollTop))
    {
        set.add(elementScrollTop);
    }
}
return JSON.stringify([...set]);
};");
    }

    private async Task<int[]> GetSizeListAsync(bool isVertical)
    {
        var sizeList = await WebView.InvokeScriptAsync("GetSizeList", new[] { isVertical.ToString() });
        Debug.WriteLine(sizeList);
        return JsonSerializer.Deserialize<int[]>(sizeList)!;        
    }


    private async void WebView_DOMContentLoaded(WebView sender, WebViewDOMContentLoadedEventArgs args)
    {
        if (string.IsNullOrEmpty(PageHtml)) { return; }

        PerfomanceStopWatch sw = PerfomanceStopWatch.StartNew("DOMContentLoaded");
        using var _ = await _domUpdateLock.LockAsync(default);


        var oldPageCount = _innerPageCount == 0 ? 1 : _innerPageCount;
        var oldCurrentPageIndex = _innerCurrentPage;
        double oldPageInPercentage = _innerCurrentPage / (double)_innerPageCount;


        //
        // 段組みレイアウトについて
        // column-countを1以上に指定してやると単純にそれだけの段に分かれた表示にできるが
        // 例えば縦書きなら横幅をViewPortサイズに限定することで縦長の段組みを描画できる
        // 縦に等間隔に並んだページに対してページ高さと同等のスクロール量をページ毎に設定することで
        // ページ送りを表現している。
        //

        {
            // 縦書きかをチェック
            var writingModeString = await GetWritingModeAsync();
            IsVerticalLayout = writingModeString switch
            {
                "vertical-rl" => true,
                "vertical-lr" => true,
                "sideways-rl" => true,
                "sideways-lr" => true,
                _ => false,
            };

            NowRightToLeftReadingMode = writingModeString.EndsWith("rl");

            Debug.WriteLine($"writingModeString: {writingModeString}, IsVerticalLayout: {IsVerticalLayout}");
        }

        sw.ElapsedWrite("check vertical writting");

        var columnCount = ColumnCount;
        if (IsVerticalLayout)
        {
            // heightを指定しないと overflow: hidden; が機能しない
            // width: 100vwとすることで表示領域に幅を限定する。段組みをビューポートの高さを越えて縦長に描画させるために必要。
            // column-countは表示領域に対して分割数の上限。段組み描画のために必要。
            // column-rule-widthはデフォルトでmidium。アプリ側での細かい高さ計算の省略ために0pxに指定。
            //await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"width: 100vw; overflow: hidden; max-height: {WebView.ActualHeight}px; column-count: {columnCount}; column-rule-width: 0px; column-gap: 1em; font-size:{FontSize}px; \";" });
            await SetVerticalBodyStyleAsync(WebView.ActualHeight, columnCount, FontSize);
        }
        else
        {
            // Note: -8は下側と右側の見切れ対策
            //await WebView.InvokeScriptAsync("eval", new[] { $"document.body.style = \"overflow: hidden; width:{WebView.ActualWidth - 8}; max-height:{WebView.ActualHeight - 8}px; column-count: {columnCount}; column-rule-width: 0px; column-gap: 1em; font-size:{FontSize}px; \";" });
            await SetHorizontalBodyStyleAsync(WebView.ActualWidth - 8, WebView.ActualHeight - 8, columnCount, FontSize);
        }

        sw.ElapsedWrite("check content width/height limitation.");
        //
        // １ページの高さを求める
        // ページの各要素のoffsetが各ページごとのスクロール基準位置の候補となる。
        // 章のタイトルなどズレているものもあるため、
        // アプリ側でスクロール基準位置として使えない値を切り落とす。
        // 先頭からいくつか候補の値をピックアップして、
        // ある値がより多くの他のスクロール基準位置の値を割り切れた(X mod value == 0)場合に１ページの高さとして扱う。
        // 
        {
            string offsetText = IsVerticalLayout ? "offsetTop" : "offsetLeft";
            //var sizeList = await WebView.InvokeScriptAsync("eval", new[]
            //{
            //    $@"
            //        const pList = document.querySelectorAll('p, div, img, span');
            //        const heightArray = [];
            //        var count = 0;
            //        for (var [i, item] of pList.entries())
            //        {{
            //            const elementScrollTop = item.{offsetText};
            //            if (elementScrollTop == null) {{ continue; }}

            //            if (heightArray.length == 0)
            //            {{
            //                heightArray.push(elementScrollTop);
            //            }}
            //            else if (heightArray[count] != elementScrollTop)
            //            {{
            //                heightArray.push(elementScrollTop);
            //                count++;
            //            }}                        
            //        }}
            //        JSON.stringify(heightArray);
            //        "
            //});
            //Debug.WriteLine(sizeList);
            //var sizeItems = JsonSerializer.Deserialize<int[]>(sizeList)!.Distinct().ToArray();

            // 1ページの高さを求める
            // ページ全体の高さを求める
            // ページ数を求める
            // 現状は重すぎる、特にquerySelectorAllが激重、eval関係なくこれが原因

            var sizeItems = (await GetSizeListAsync(IsVerticalLayout))!.Distinct().OrderBy(x => x).ToArray();
            sw.ElapsedWrite("check page sizeList.");
            var first = sizeItems.ElementAtOrDefault(0);
            sizeItems = sizeItems.Select(x => x - first).ToArray();
            var pageRealSize = IsVerticalLayout ? await GetPageHeight() : await GetPageWidth();
            const int candidateSampleCount = 5;
            const int compareSampleCount = 10;
            int heroPageHeight = -1;
            int heroHitCount = -1;
            foreach (var candidatePageSize in sizeItems.Skip(1).Where(x => x > pageRealSize).Take(candidateSampleCount))
            {
                var hitCount = sizeItems.TakeLast(compareSampleCount).Count(x => x % candidatePageSize == 0);
                if (hitCount > heroHitCount)
                {
                    heroPageHeight = candidatePageSize;
                    heroHitCount = hitCount;
                }
            }

            sw.ElapsedWrite("culc hero page size.");

            if (pageRealSize > heroPageHeight)
            {
                _innerPageCount = 1;
                _onePageScrollSize = heroPageHeight;
                _webViewScrollableSize = heroPageHeight;

                // 1ページに収まってる場合は画像のみのページかどうかをチェックする
                var pCount = int.Parse(await WebView.InvokeScriptAsync("eval", new[] { "document.querySelectorAll('p').length.toString();" }));
                NowOnlyImageView = pCount == 0;
                Debug.WriteLine("NowOnlyImageView: " + NowOnlyImageView);
            }
            else
            {
                var pageScrollPositions = sizeItems.Where(x => x % heroPageHeight == 0).ToArray();

                _innerPageCount = pageScrollPositions.Length;
                _onePageScrollSize = heroPageHeight;
                _webViewScrollableSize = pageScrollPositions.Last() + heroPageHeight;

                NowOnlyImageView = false;
            }

            Debug.WriteLine($"WebViewSize: {_webViewScrollableSize}, pageCount: {_innerPageCount}, onePageScrollSize: {_onePageScrollSize}");
            sw.ElapsedWrite("culc page counts.");

            // ページ最後尾にスクロール用の余白を作る
            // 最後のページのスクロール位置が前ページを含んだ形になってしまう問題を回避する
            await WebView.InvokeScriptAsync("eval", new[]
            {
                $@"
                    for (var i = 0; i < 100; i++)
                    {{
                        document.body.appendChild(document.createElement('p'));
                    }}
                    "
            });

            sw.ElapsedWrite("add padding at last page.");
        }

        if (isFirstContent)
        {
            isFirstContent = false;

            _innerCurrentPage = Math.Clamp(FirstApproachingPageIndex, 0, _innerPageCount - 1);
        }
        else if (!_isGoNextOrPreview) // Refresh
        {
            var newPageCount = _innerPageCount;
            var newCurrentPageIndex = _innerCurrentPage;

            _innerCurrentPage = (int)Math.Round(_innerPageCount * oldPageInPercentage);

            Debug.WriteLine($"{oldCurrentPageIndex}/{oldPageCount} -> {_innerCurrentPage}/{newPageCount}");
        }
        else
        {
            _innerCurrentPage = Math.Min(PreservedCurrentInnerPageIndex, _innerPageCount - 1);
        }

        _isGoNextOrPreview = false;


        sw.ElapsedWrite("set innerCurrentPage");

        await SetScrollPositionAsync();

        TotalInnerPageCount = _innerPageCount;
        CurrentInnerPage = _innerCurrentPage;

        sw.ElapsedWrite("set scroll position");        

        _sw.Stop();
        Debug.WriteLine($"EPub loading time: {_sw.Elapsed.TotalSeconds:F3}");
    }




    private async Task SetScrollPositionAsync()
    {
        double position = _innerCurrentPage * _onePageScrollSize;

        // Note: vertical-rlでは縦スクロールが横倒しして扱われるので縦書き横書きどちらもXにだけ設定すればOK
        await WebView.InvokeScriptAsync("eval", new[] { $"window.scrollTo({position:F3}, 0);" });

#if DEBUG
        //if (IsVerticalLayout)
        //{
        //    Debug.WriteLine("ScrollPosition: " + await GetScrollTop());
        //}
        //else
        //{
        //    Debug.WriteLine("ScrollPosition: " + await GetScrollLeft());
        //}
#endif
    }

    private async Task<double> GetScrollableWidth()
    {
        var widthText = await WebView.InvokeScriptAsync("eval", new[] { "Math.max(document.body.scrollWidth, document.documentElement.scrollWidth,document.body.offsetWidth, document.documentElement.offsetWidth,document.body.clientWidth, document.documentElement.clientWidth).toString();" });
        return double.TryParse(widthText, out var value) ? value : 0;
    }

    private async Task<double> GetScrollableHeight()
    {
        var heightText = await WebView.InvokeScriptAsync("eval", new[] { "Math.max(document.body.scrollHeight, document.documentElement.scrollHeight,document.body.offsetHeight, document.documentElement.offsetHeight,document.body.clientHeight, document.documentElement.clientHeight).toString();" });
        return double.TryParse(heightText, out var value) ? value : 0;
    }

    private async Task<double> GetPageWidth()
    {
        var widthText = await WebView.InvokeScriptAsync("eval", new[] { "window.innerWidth.toString()" });
        return double.TryParse(widthText, out var value) ? value : 0;
    }

    private async Task<double> GetPageHeight()
    {
        var heightText = await WebView.InvokeScriptAsync("eval", new[] { "window.innerHeight.toString()" });
        return double.TryParse(heightText, out var value) ? value : 0;
    }

    private async Task<double> GetScrollLeft()
    {
        var XOffsetText = await WebView.InvokeScriptAsync("eval", new[] { "window.pageXOffset.toString()" });
        return double.TryParse(XOffsetText, out var value) ? value : 0;
    }

    private async Task<double> GetScrollTop()
    {
        // Note: writing-mode:vertical-rlが指定されてるとページオフセットの値が上下左右で入れ替わる挙動があるのでpageXOffsetから取得している
        var scrollTop = await WebView.InvokeScriptAsync("eval", new[] { "window.pageYOffset.toString()" });
        return double.TryParse(scrollTop, out var value) ? value : 0;
    }

    private void WebView_GettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        args.TryCancel();
    }
}
