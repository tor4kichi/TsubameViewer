using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;

namespace TsubameViewer.Presentation.Views.Helpers
{
    public static class CodeBehindExtensions
    {
        public static Visibility TrueToVisible(this bool b) => b ? Visibility.Visible : Visibility.Collapsed;
        public static Visibility FalseToVisible(this bool b) => TrueToVisible(!b);
    }
}
