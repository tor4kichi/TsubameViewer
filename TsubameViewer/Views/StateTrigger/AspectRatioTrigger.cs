using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Windows.UI.Xaml;
using WindowsStateTriggers;

namespace TsubameViewer.Views.StateTrigger
{
    public class AspectRatioTrigger : StateTriggerBase, ITriggerValue
    {
        public FrameworkElement Target
        {
            get { return (FrameworkElement)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Target.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(FrameworkElement), typeof(AspectRatioTrigger), new PropertyMetadata(null, OnTargetPropertyChnaged));

        private static void OnTargetPropertyChnaged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var _this = (AspectRatioTrigger)d;
            _this.Target.SizeChanged += _this.Target_SizeChanged;
        }

        private void Target_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var isActive = IsActive;
            var targetRatio = WidthHeightRatio;
            if (e.NewSize.Height == 0 
                || e.NewSize.Width == 0
                || targetRatio == 0
                )
            {
                IsActive = false;
            }
            else
            {
                var ratio = e.NewSize.Width / e.NewSize.Height;
                IsActive = ActiveInHigher
                    ? ratio >= targetRatio
                    : ratio <= targetRatio
                    ;
                Debug.WriteLine($"AspectRatio: {ratio}, Target: {Target}");
            }

            
            if (isActive != IsActive)
            {
                SetActive(IsActive);
                IsActiveChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            private set { SetValue(IsActiveProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsActive.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register("IsActive", typeof(bool), typeof(AspectRatioTrigger), new PropertyMetadata(false));




        public double WidthHeightRatio
        {
            get { return (double)GetValue(WidthHeightRatioProperty); }
            set { SetValue(WidthHeightRatioProperty, value); }
        }

        // Using a DependencyProperty as the backing store for WidthHeightRatio.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty WidthHeightRatioProperty =
            DependencyProperty.Register("WidthHeightRatio", typeof(double), typeof(AspectRatioTrigger), new PropertyMetadata(0.0));




        public bool ActiveInHigher
        {
            get { return (bool)GetValue(ActiveInHigherProperty); }
            set { SetValue(ActiveInHigherProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ActiveInHigher.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ActiveInHigherProperty =
            DependencyProperty.Register("ActiveInHigher", typeof(bool), typeof(AspectRatioTrigger), new PropertyMetadata(true));




        public event EventHandler IsActiveChanged;
    }
}
