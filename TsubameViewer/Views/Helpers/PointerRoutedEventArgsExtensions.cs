using CommunityToolkit.Diagnostics;
using CommunityToolkit.WinUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
#nullable enable
namespace TsubameViewer.Views;
public static class PointerRoutedEventArgsExtensions
{
    public static bool IsContactUIElement(this PointerEventArgs e, UIElement target, UIElement rootElement)
    {
        var ts = rootElement.TransformToVisual(target);
        var p = ts.TransformPoint(e.CurrentPoint.Position);        
        Vector2 pointerRelativePos = p.ToVector2();
        if (pointerRelativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > pointerRelativePos.X
            && target.ActualSize.Y > pointerRelativePos.Y)
        {
            return true;
        }
        else { return false; }
    }

    public static bool IsContactUIElement(this PointerRoutedEventArgs e, UIElement target)
    {
        var p = e.GetCurrentPoint(target);
        Vector2 pointerRelativePos = p.Position.ToVector2();
        if (pointerRelativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > pointerRelativePos.X
            && target.ActualSize.Y > pointerRelativePos.Y)
        {
            return true;
        }
        else { return false; }
    }

    public static bool IsContactUIElement(this TappedRoutedEventArgs e, UIElement target)
    {
        var p = e.GetPosition(target);
        Vector2 pointerRelativePos = p.ToVector2();
        if (pointerRelativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > pointerRelativePos.X
            && target.ActualSize.Y > pointerRelativePos.Y)
        {
            return true;
        }
        else { return false; }
    }

    public static bool IsContactUIElement(this DragEventArgs e, UIElement target)
    {
        var p = e.GetPosition(target);
        Vector2 pointerRelativePos = p.ToVector2();
        if (pointerRelativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > pointerRelativePos.X
            && target.ActualSize.Y > pointerRelativePos.Y)
        {
            return true;
        }
        else { return false; }
    }
    

    /// <summary>
    /// 子孫要素であるかをUIElement.Nameを通じて
    /// </summary>
    /// <param name="e"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool IsDecendantOrSelfOfElement(this PointerRoutedEventArgs e, FrameworkElement element)
    {
        var os = (FrameworkElement)e.OriginalSource;
        return os.FindAscendantOrSelf(element.Name) != null;
    }

    public static bool IsContactUIElement(this Point pointerRelativePos, UIElement target)
    {        
        if (pointerRelativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > pointerRelativePos.X
            && target.ActualSize.Y > pointerRelativePos.Y)
        {
            return true;
        }
        else { return false; }
    }

    public static bool IsContactUIElementRelativeFrom(this Point tapPos, UIElement relativeFrom, UIElement target)
    {
        var transform = relativeFrom.TransformToVisual(target);
        var relativePos = transform.TransformPoint(tapPos);
        if (relativePos is { X: > 0, Y: > 0 }
            && target.ActualSize.X > relativePos.X
            && target.ActualSize.Y > relativePos.Y)
        {
            return true;
        }
        else { return false; }
    }
}
