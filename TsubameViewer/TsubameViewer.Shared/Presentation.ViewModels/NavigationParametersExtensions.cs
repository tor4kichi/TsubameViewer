using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;

namespace TsubameViewer.Presentation.ViewModels
{
    public static class NavigationParametersExtensions
    {
        public static bool TryGetValueSafe<T>(this INavigationParameters parameters, string key, out T outValue)
        {
            if (!parameters.ContainsKey(key))
            {
                outValue = default(T);
                return false;
            }
            else
            {
                return parameters.TryGetValue(key, out outValue);
            }
        }
    }
}
