using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;

namespace TsubameViewer.Presentation.ViewModels.PageNavigation
{    
    public sealed class NavigationRequestMessage : AsyncRequestMessage<INavigationResult>
    {
        public NavigationRequestMessage(string pageName)
        {
            PageName = pageName;
        }

        public NavigationRequestMessage(string pageName, INavigationParameters parameters)
        {
            PageName = pageName;
            Parameters = parameters;
        }

        public NavigationRequestMessage(string pageName, params (string key, object parameter)[] parameters)
        {
            PageName = pageName;
            Parameters = new NavigationParameters(parameters);
        }

        public string PageName { get; }
        public INavigationParameters Parameters { get; }
    }

    public static class NavigationRequestMessageExtensions
    {
        private static async Task<INavigationResult> NavigateAsync_Internal(IMessenger messenger, NavigationRequestMessage message)
        {
            return await messenger.Send(message);
        }

        public static Task<INavigationResult> NavigateAsync(this IMessenger messenger, string pageName)
        {
            return NavigateAsync_Internal(messenger, new NavigationRequestMessage(pageName));
        }

        public static Task<INavigationResult> NavigateAsync(this IMessenger messenger, string pageName, INavigationParameters parameters)
        {
            return NavigateAsync_Internal(messenger, new NavigationRequestMessage(pageName, parameters));
        }

        public static Task<INavigationResult> NavigateAsync(this IMessenger messenger, string pageName, params (string key, object parameter)[] parameters)
        {
            return NavigateAsync_Internal(messenger, new NavigationRequestMessage(pageName, parameters));
        }
    }
}
