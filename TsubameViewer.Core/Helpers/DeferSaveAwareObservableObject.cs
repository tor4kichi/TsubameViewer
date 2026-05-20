using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TsubameViewer.Core;


public abstract class DeferSaveAwareObservableObject : ObservableObject
{
    private readonly bool _isSaveWhenPropertyChanged;

    public DeferSaveAwareObservableObject(bool isSaveWhenPropertyChanged = true)
    {        
        _isSaveWhenPropertyChanged = isSaveWhenPropertyChanged;
    }

    protected abstract void OnSave();

    public bool TrySave()
    {
        if (_deferSaveCount != 0) 
        {
            _somePropertyChangedInDeferSave = true;
            return false; 
        }
        else 
        {
            _somePropertyChangedInDeferSave = false;
            OnSave();
            return true;
        }        
    }
    bool _somePropertyChangedInDeferSave;
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        if (_isSaveWhenPropertyChanged) 
        {
            TrySave();
        }

        base.OnPropertyChanged(e);
    }

    private int _deferSaveCount = 0;
    void DecrementDeferSave()
    {
        if (--_deferSaveCount <= 0 && _somePropertyChangedInDeferSave)
        {
            TrySave();
        }
    }

    public IDisposable GetDeferSave()
    {
        _deferSaveCount++;
        return new DeferSaveDisposable(this);
    }

    struct DeferSaveDisposable : IDisposable
    {
        private readonly DeferSaveAwareObservableObject _this;

        public DeferSaveDisposable(DeferSaveAwareObservableObject _this)
        {
            this._this = _this;
        }
        bool _disposed;
        void IDisposable.Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            _this.DecrementDeferSave();            
        }
    }
}
