using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#nullable enable
namespace TsubameViewer.Views.Helpers;
public class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    /// <summary>
    /// Adds multiple items to the collection and raises a single CollectionChanged event.
    /// </summary>
    public void AddRange(IEnumerable<T> items, bool ignoreNotify = false)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        _suppressNotification = true;
        foreach (var item in items)
        {
            Items.Add(item); // Directly add to Items to avoid per-item notifications
        }
        _suppressNotification = false;

        if (!ignoreNotify)
        {
            // Notify that the collection has changed (Reset means "refresh everything")
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    public void ForceNotifyReset()
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Override to suppress notifications during bulk operations.
    /// </summary>
    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
        {
            base.OnCollectionChanged(e);
        }
    }
}
