using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.System;
using Windows.UI.Xaml;
using WindowsStateTriggers;

namespace TsubameViewer.Views.StateTrigger
{
    public sealed class VirualKeyPressingTrigger : StateTriggerBase, ITriggerValue, IDisposable
    {
        private bool _IsActive;
        public bool IsActive
        {
            get => _IsActive;
            set
            {
                if (_IsActive != value)
                {
                    SetActive(value);
                    _IsActive = value;
                    IsActiveChanged?.Invoke(this, EventArgs.Empty);
                    Debug.WriteLine($"{Key} key : {value}");
                }
            }
        }

        public event EventHandler IsActiveChanged;

        public VirualKeyPressingTrigger()
        {
        }

        public VirtualKey Key
        {
            get { return (VirtualKey)GetValue(KeyProperty); }
            set { SetValue(KeyProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Key.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty KeyProperty =
            DependencyProperty.Register("Key", typeof(VirtualKey), typeof(VirualKeyPressingTrigger), new PropertyMetadata(VirtualKey.None, OnKeyPropertyChanged));

        private static void OnKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as VirualKeyPressingTrigger).KeyChanged((VirtualKey)e.NewValue);
        }

        private void KeyChanged(VirtualKey key)
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp -= CoreWindow_KeyUp;
            Window.Current.Activated -= Current_Activated;
            IsActive = false;

            if (key == VirtualKey.None) { return; }

            IsActive = Window.Current.CoreWindow.GetAsyncKeyState(key) is Windows.UI.Core.CoreVirtualKeyStates.Down;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp += CoreWindow_KeyUp;
            Window.Current.Activated += Current_Activated;
        }
        

        private void CoreWindow_KeyDown(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            if (args.VirtualKey == Key)
            {
                IsActive = true;
            }
        }

        private void CoreWindow_KeyUp(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.KeyEventArgs args)
        {
            if (args.VirtualKey == Key)
            {
                IsActive = false;
            }
        }


        private void Current_Activated(object sender, Windows.UI.Core.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == Windows.UI.Core.CoreWindowActivationState.Deactivated)
            {
                IsActive = false;
            }
            else
            {
                IsActive = Window.Current.CoreWindow.GetAsyncKeyState(Key) is Windows.UI.Core.CoreVirtualKeyStates.Down;
            }
        }

        public void Dispose()
        {
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
            Window.Current.CoreWindow.KeyUp -= CoreWindow_KeyUp;
            Window.Current.Activated -= Current_Activated;
        }
    }
}
