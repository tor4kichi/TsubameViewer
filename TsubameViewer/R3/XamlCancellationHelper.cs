using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;

#nullable enable
namespace TsubameViewer.Views;
public static class XamlCancellationHelper
{
    [ThreadStatic]
    static readonly Dictionary<FrameworkElement, CancellationTokenSource> _ctsMap = new();

    public static CancellationToken GetCancellationTokenOnUnloaded(this FrameworkElement element, bool skipIfIsUnaloded = false)
    {
        static void Element_Unloaded(object sender, RoutedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            element.Unloaded -= Element_Unloaded;
            bool canceled = Cancel(element);
            if (canceled)
            {
                if (_ctsMap.Remove(element, out var cts))
                {
                    cts.Dispose();
                }
            }
            Debug.WriteLineIf(canceled, $"[GetCancellationTokenOnNavigatingFrom] unregistered: {element.GetType().Name}");
        }

        if (skipIfIsUnaloded && element.IsLoaded == false)
        {
            return new CancellationToken(true); // 既にUnloaded状態なので、即座にキャンセルされたトークンを返す
        }

        if (_ctsMap.TryGetValue(element, out var cts) is false)
        {
            cts = new CancellationTokenSource();
            _ctsMap.Add(element, cts);
            // Unloadedイベントを登録しておく
            element.Unloaded += Element_Unloaded;
            Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] registered: {element.GetType().Name}");
        }

        return cts.Token;
    }

    public static CancellationToken GetCancellationTokenOnNavigatingFrom(this Page page)
    {
        static void Frame_Navigating(object sender, Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            var frame = (Frame)sender;
            frame.Navigating -= Frame_Navigating;
            var page = (FrameworkElement)frame.Content;
            Cancel(page);
            Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] unregistered: {page.GetType().Name}");
        }

        // Frameベースでキャンセル対応する
        // Frameに対して常に１つまでしかキャンセルトークンを必要としないはず
        // 新しいCtはFrame.Navigatedのタイミングで生成されるよう期待した実装
        if (_ctsMap.TryGetValue(page, out var cts) is false)
        {
            cts = new CancellationTokenSource();
            _ctsMap.Add(page, cts);
            if (page.Frame.Content != page)
            {
                // 既に別のページに移動しているキャンセル済みとして扱う
                cts.Cancel();
                Debug.WriteLine($"GetCancellationTokenOnNavigatingFrom already canceled: {page.GetType().Name}");
            }
            else
            {
                page.Frame.Navigating += Frame_Navigating;
                Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] registered: {page.GetType().Name}");
            }
        }
        else if (cts.IsCancellationRequested)
        {
            cts.Dispose();
            _ctsMap.Remove(page);
            cts = new CancellationTokenSource();
            _ctsMap.Add(page, cts);
            if (page.Frame.Content != page)
            {
                // 既に別のページに移動しているキャンセル済みとして扱う
                cts.Cancel();
                Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] already canceled: {page.GetType().Name}");
            }
            else
            {
                page.Frame.Navigating += Frame_Navigating;
                Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] registered: {page.GetType().Name}");
            }
        }

        return cts.Token;
    }

    public static bool Cancel(this FrameworkElement element)
    {
        if (_ctsMap.TryGetValue(element, out var cts))
        {
            try
            {
                cts.Cancel();
            }
            catch { }
            Debug.WriteLine($"[GetCancellationTokenOnNavigatingFrom] Cancel: {element.GetType().Name}");
            return true;
        }
        else
        {            
            return false;
        }
    }   
}
