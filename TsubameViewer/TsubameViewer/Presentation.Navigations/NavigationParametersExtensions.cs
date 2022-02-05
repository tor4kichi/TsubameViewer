using Microsoft.UI.Xaml.Navigation;
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

        const string NavigationModeKey = "__nm";
        public static NavigationMode GetNavigationMode(this INavigationParameters parameters)
        {
            return parameters.TryGetValue<NavigationMode>(NavigationModeKey, out var mode) ? mode : throw new InvalidOperationException();
        }

        public static void SetNavigationMode(this INavigationParameters parameters, NavigationMode mode)
        {
            parameters.Remove(NavigationModeKey);
            parameters.Add(NavigationModeKey, mode);
        }
    }
}
