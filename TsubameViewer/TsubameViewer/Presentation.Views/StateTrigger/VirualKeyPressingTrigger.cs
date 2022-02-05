using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.System;
using Microsoft.UI.Xaml;
using CommunityToolkit.WinUI.UI.Triggers;
using Microsoft.UI.Input;
using CommunityToolkit.WinUI.UI.Triggers_Custom;

namespace TsubameViewer.Presentation.Views.StateTrigger
{
    public sealed class VirualKeyPressingTrigger : StateTriggerBase, IDisposable, ITriggerValue
    {
        public FrameworkElement Target
        {
            get { return (FrameworkElement)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(FrameworkElement), typeof(VirualKeyPressingTrigger), new PropertyMetadata(null, OnTargetPropertyChanged));

        private static void OnTargetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var _this = d as VirualKeyPressingTrigger;
            if (e.NewValue is FrameworkElement fe)
            {
                fe.KeyDown += _this.Fe_KeyDown;
                fe.KeyUp += _this.Fe_KeyUp;
            }
        }

        private void Fe_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Key)
            {
                IsActive = true;
            }
        }

        private void Fe_KeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Key)
            {
                IsActive = false;
            }
        }


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
            App.Current.Window.Activated -= Current_Activated;
            IsActive = false;

            if (key == VirtualKey.None) { return; }

            IsActive = InputKeyboardSource.GetKeyStateForCurrentThread(key) is Windows.UI.Core.CoreVirtualKeyStates.Down;

            App.Current.Window.Activated += Current_Activated;
        }       

        private void Current_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated)
            {
                IsActive = false;
            }
            else
            {
                IsActive = InputKeyboardSource.GetKeyStateForCurrentThread(Key) is Windows.UI.Core.CoreVirtualKeyStates.Down;
            }
        }

        public void Dispose()
        {
            if (Target is not null and var fe)
            {
                fe.KeyDown -= Fe_KeyDown;
                fe.KeyUp -= Fe_KeyUp;
            }
            App.Current.Window.Activated -= Current_Activated;
        }
    }
}
