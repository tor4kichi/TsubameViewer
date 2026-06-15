using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TsubameViewer.ViewModels;

public abstract class CommandBase : IRelayCommand, ICommand
{
    public event EventHandler CanExecuteChanged;

    public abstract bool CanExecute(object parameter);
    public abstract void Execute(object parameter);

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
