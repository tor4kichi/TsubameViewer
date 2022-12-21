using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;

namespace TsubameViewer.Views.Helpers
{
    public static class CodeBehindExtensions
    {
        public static Visibility TrueToVisible(this bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility FalseToVisible(this bool b) => TrueToVisible(!b);
    }
}
