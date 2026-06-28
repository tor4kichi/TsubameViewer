using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.ViewModels;

namespace TsubameViewer.Views;

public class KeyIndexMappedAdvancedCollectionView<T> : AdvancedCollectionView, IKeyIndexMapping
{
    public KeyIndexMappedAdvancedCollectionView(Func<T, string> toKey)
    {
        this.VectorChanged += KeyIndexMappedAdvancedCollectionView_VectorChanged;
        _toKey = toKey;
    }

    public KeyIndexMappedAdvancedCollectionView(IList source, Func<T, string> toKey, bool isLiveShaping = false) : base(source, isLiveShaping)
    {
        this.VectorChanged += KeyIndexMappedAdvancedCollectionView_VectorChanged;
        _toKey = toKey;
    }

    void KeyIndexMappedAdvancedCollectionView_VectorChanged(Windows.Foundation.Collections.IObservableVector<object> sender, Windows.Foundation.Collections.IVectorChangedEventArgs @event)
    {
        int index = (int)@event.Index;
        switch (@event.CollectionChange)
        {
            case Windows.Foundation.Collections.CollectionChange.Reset:
                ClearKeyIndexCache();
                break;
            case Windows.Foundation.Collections.CollectionChange.ItemInserted:
                ClearKeyIndexCache();
                break;
            case Windows.Foundation.Collections.CollectionChange.ItemRemoved:
                {
                    if (_keyIndexMap.FirstOrDefault(x => x.Value == index) is { } old && old.Key != null)
                    {
                        _keyIndexMap.Remove(old.Key);
                    }
                }
                break;
            case Windows.Foundation.Collections.CollectionChange.ItemChanged:
                {
                    if (_keyIndexMap.FirstOrDefault(x => x.Value == index) is { } old && old.Key != null)
                    {
                        _keyIndexMap.Remove(old.Key);
                    }
                    var key = _toKey((T)this[index]);
                    _keyIndexMap.Add(key, index);
                }
                break;
        }
    }

    readonly Func<T, string> _toKey;
    readonly Dictionary<string, int> _keyIndexMap = [];

    public int IndexFromKey(string key)
    {
        TryBuildKeyIndexCache();
        return _keyIndexMap.TryGetValue(key, out int index) ? index : -1;
    }

    public string KeyFromIndex(int index)
    {
        if (index < 0 || index >= Count)
            return null;

        return _toKey((T)this[index]);
    }

    void TryBuildKeyIndexCache()
    {
        if (_keyIndexMap.Count != 0) { return; }

        for (int i = 0; i < Count; i++)
        {
            var key = _toKey((T)this[i]);
            _keyIndexMap.Add(key, i);
        }
    }

    void ClearKeyIndexCache()
    {
        _keyIndexMap.Clear();
    }

}
