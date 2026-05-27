using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using ZLinq;
using static Microsoft.Toolkit.Uwp.UI.Animations.Expressions.ExpressionValues;

namespace TsubameViewer.Views.Converters;

internal class UriHumanReadableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string target;
        if (value is string s)
        {
            target = s;
        }
        else if (value is Uri u)
        {
            target = u.AbsolutePath;
        }
        else
        {
            target = "";
        }

        return UriHelper.ToHumanReadable(target);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public static class UriHelper
{
    public static string ToHumanReadable(string s)
    {
        if (s == null) { return ""; }
        return s.Split('\\', StringSplitOptions.RemoveEmptyEntries).TakeLast(2).AsValueEnumerable().JoinToString('/');
    }
}
