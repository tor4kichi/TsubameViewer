using I18NPortable;
using Microsoft.Toolkit.Uwp.UI;
using System;
using Windows.UI.Xaml.Data;

namespace TsubameViewer.Locales;

public sealed class LocalizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Enum enumValue)
        {
            return enumValue.Translate();
        }
        else if (value is SortDescription sortDesc)
        {
            if (string.IsNullOrEmpty(sortDesc.PropertyName)) { throw new InvalidOperationException(); }
            return $"{sortDesc.PropertyName}_{(sortDesc.Direction == SortDirection.Ascending ? "Asc" : "Desc")}".Translate();
        }
        else if (value is string stringValue)
        {
            if (parameter is string str)
            {
                return stringValue.Translate(str);
            }
            else if (parameter is object[] p)
            {
                return stringValue.Translate(p);
            }
            else if (parameter is object obj)
            {
                return stringValue.Translate(obj.ToString());
            }
            else
            {
                return stringValue.Translate();
            }
        }
        else
        {
            return string.Empty;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class LocalizeKeyAsParametersConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        string key = (string)parameter;
        return key.Translate(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}