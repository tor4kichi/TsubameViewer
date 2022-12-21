using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace TsubameViewer.Views.Helpers
{
    public static class VisualTreeExtentions
    {
        public static T FindFirstChild<T>(this FrameworkElement element) where T : FrameworkElement
        {
            int childrenCount = VisualTreeHelper.GetChildrenCount(element);
            var children = new FrameworkElement[childrenCount];

            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                children[i] = child;
                if (child is T)
                    return (T)child;
            }

            for (int i = 0; i < childrenCount; i++)
                if (children[i] != null)
                {
                    var subChild = FindFirstChild<T>(children[i]);
                    if (subChild != null)
                        return subChild;
                }

            return null;
        }


        public static async ValueTask WaitFillingValue<TElement>(this TElement element, Predicate<TElement> whenComplete, CancellationToken ct)
        {
            while (whenComplete(element) is false)
            {
                await Task.Delay(1, ct);
            }
        }

        public static async ValueTask WaitFillingValue(Func<bool> whenComplete, CancellationToken ct)
        {
            while (whenComplete() is false)
            {
                await Task.Delay(1, ct);
            }
        }
    }
}
