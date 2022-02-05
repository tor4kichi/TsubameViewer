using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using CommunityToolkit.WinUI.UI.Triggers;
using CommunityToolkit.WinUI.UI.Triggers_Custom;

namespace TsubameViewer.Presentation.Views.StateTrigger
{
    public sealed class PointerCollisionTrigger : StateTriggerBase, ITriggerValue
    {
        public UIElement Target
        {
            get { return (UIElement)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(UIElement), typeof(PointerCollisionTrigger), new PropertyMetadata(null, OnTargetPropertyChanged));

        private static void OnTargetPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is FrameworkElement item)
            {
                var _this = d as PointerCollisionTrigger;
                //item.PointerMoved += _this.Item_PointerMoved;
                item.Unloaded += _this.Item_Unloaded;
            }
        }

        private void Item_Unloaded(object sender, RoutedEventArgs e)
        {
            var item = sender as FrameworkElement;
            item.PointerMoved -= Item_PointerMoved;
            item.Unloaded -= Item_Unloaded;
        }

        private void Item_PointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var collisionRect = CollisionRect;
            var oldIsActive = IsActive;
            if (!collisionRect.IsEmpty)
            {
                var pt = e.GetCurrentPoint((UIElement)sender);
                SetActive(IsActive = collisionRect.Contains(pt.Position));
            }
            else
            {
                SetActive(IsActive = e.Pointer.IsInRange);
            }

            if (oldIsActive != IsActive)
            { 
                IsActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }


        public Rect CollisionRect
        {
            get { return (Rect)GetValue(CollisionRectProperty); }
            set { SetValue(CollisionRectProperty, value); }
        }

        public bool IsActive { get; set; }

        // Using a DependencyProperty as the backing store for MyProperty.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CollisionRectProperty =
            DependencyProperty.Register("CollisionRect", typeof(Rect), typeof(PointerCollisionTrigger), new PropertyMetadata(Rect.Empty));

        public event EventHandler IsActiveChanged;
    }
}
