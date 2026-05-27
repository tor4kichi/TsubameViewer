using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Views.Converters;

internal class TimeSpanToStringConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan timeSpan)
        {
            return TimeSpanHelper.FormatTimeSpan(timeSpan);
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// TimeSpan の時間・分・秒を文字列にフォーマットするヘルパークラス
/// </summary>
internal static class TimeSpanHelper
{
    /// <summary>
    /// TimeSpan を文字列に変換します
    /// TotalHours がゼロの場合は「分:秒」、それ以外は「時:分:秒」の形式で返します
    /// </summary>
    /// <param name="timeSpan">変換対象の TimeSpan</param>
    /// <returns>フォーマットされた時間文字列</returns>
    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        var duration = timeSpan.Duration();
        int hours = (int)duration.TotalHours;
        int minutes = duration.Minutes;
        int seconds = duration.Seconds;
        bool isAnyNegation = timeSpan < TimeSpan.Zero;
        if (hours != 0)
        {
            return $"{(isAnyNegation?"-":"")}{hours:D2}:{minutes:D2}:{seconds:D2}";
        }
        else
        {
            return $"{(isAnyNegation ? "-" : "")}{minutes:D2}:{seconds:D2}";
        }
    }
}
