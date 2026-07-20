using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.ViewModels;
using Windows.Foundation.Collections;

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
                InsertKeyIndexCache(@event);
                break;
            case Windows.Foundation.Collections.CollectionChange.ItemRemoved:
                {
                    if (_keyIndexMap.FirstOrDefault(x => x.Value == index) is { } old && old.Key != null)
                    {
                        lock (_indexUpdateLock)
                        {
                            _keyIndexMap.Remove(old.Key);
                        }
                    }
                }
                break;
            case Windows.Foundation.Collections.CollectionChange.ItemChanged:
                {
                    if (_keyIndexMap.FirstOrDefault(x => x.Value == index) is { } old && old.Key != null)
                    {
                        lock (_indexUpdateLock)
                        {
                            _keyIndexMap.Remove(old.Key);
                        }
                    }
                    var key = _toKey((T)this[index]);
                    lock (_indexUpdateLock)
                    {
                        _keyIndexMap.Add(key, index);
                    }
                }
                break;
        }
    }

    object _indexUpdateLock = new();
    private void InsertKeyIndexCache(IVectorChangedEventArgs @event)
    {
        lock (_indexUpdateLock)
        {
            int insertedIndex = (int)@event.Index;

            // Dictionary を直接走査してインデックスを更新
            foreach (var entry in _keyIndexMap.ToArray())
            {
                if (entry.Value >= insertedIndex)
                {
                    _keyIndexMap[entry.Key] = entry.Value + 1;
                }
            }

            // 挿入されたアイテムのキーを取得してマップに追加
            var insertedItem = this[insertedIndex];
            var key = _toKey((T)insertedItem);
            _keyIndexMap.Add(key, insertedIndex);
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

        lock (_indexUpdateLock)
        {
            for (int i = 0; i < Count; i++)
            {
                var key = _toKey((T)this[i]);
                _keyIndexMap.Add(key, i);
            }
        }
    }

    void ClearKeyIndexCache()
    {
        lock (_indexUpdateLock)
        {
            _keyIndexMap.Clear();
        }
    }

}
