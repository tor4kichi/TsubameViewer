using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TsubameViewer.Presentation.Navigations
{
    public interface INavigationResult
    {
        bool IsSuccess { get; }
        Exception Exception { get; }
    }

    public class NavigationResult : INavigationResult
    {
        public bool IsSuccess { get; init; }

        public Exception Exception { get; init; }
    }

    public interface INavigationParameters : IDictionary<string, object>
    {
        bool TryGetValue<T>(string key, out T outValue);
    }

    public class NavigationParameters : Dictionary<string, object>, INavigationParameters
    {
        public NavigationParameters(IEnumerable<KeyValuePair<string, object>> parameters)
            : base(parameters)
        {
        }

        public NavigationParameters(params (string Key, object Value)[] parameters)
            : base(parameters.Select(x => new KeyValuePair<string, object>(x.Key, x.Value)))
        {
        }

        public bool TryGetValue<T>(string key, out T outValue)
        {
            if (base.TryGetValue(key, out object temp))
            {
                var type = typeof(T);
                if (type.IsEnum && temp is string strTemp)
                {
                    outValue = (T)Enum.Parse(type, strTemp);
                }
                else
                {
                    outValue = (T)temp;
                }
                return true;
            }
            else
            {
                outValue = default(T);
                return false;
            }
        }
    }

    public interface INavigationAware
    {
        void OnNavigatedFrom(INavigationParameters parameters);
        void OnNavigatedTo(INavigationParameters parameters);
        Task OnNavigatedToAsync(INavigationParameters parameters);
    }

    public abstract class NavigationAwareViewModelBase : ObservableObject, INavigationAware
    {
        public virtual void OnNavigatedFrom(INavigationParameters parameters)
        {

        }

        public virtual void OnNavigatedTo(INavigationParameters parameters)
        {

        }

        public virtual Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            return Task.CompletedTask;
        }
    }
}
