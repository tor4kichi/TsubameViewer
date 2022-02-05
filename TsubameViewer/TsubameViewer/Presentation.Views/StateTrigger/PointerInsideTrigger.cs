using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.WinUI.UI.Triggers;

namespace TsubameViewer.Presentation.Views.StateTrigger
{
    public sealed class PointerInsideTrigger : StateTriggerBase
    {
        public UIElement Target
        {
            get { return (UIElement)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(UIElement), typeof(PointerInsideTrigger), new PropertyMetadata(null, OnTargetPropertyChanged));

        private static void OnTargetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is FrameworkElement item)
            {
                var _this = d as PointerInsideTrigger;
                item.PointerEntered += _this.Item_PointerEntered;
                item.PointerExited += _this.Item_PointerExited;
                item.Unloaded += _this .Item_Unloaded; 
            }
        }

        private void Item_Unloaded(object sender, RoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.PointerEntered -= Item_PointerEntered;
            item.PointerExited -= Item_PointerExited;
            item.Unloaded -= Item_Unloaded;
        }

        private void Item_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var oldIsActive = IsActive;
            SetActive(IsActive = true);

            if (oldIsActive != IsActive)
            {
                IsActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Item_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var oldIsActive = IsActive;
            SetActive(IsActive = false);

            if (oldIsActive != IsActive)
            {
                IsActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }


        public bool IsActive { get; set; }

        public event EventHandler IsActiveChanged;
    }
}
