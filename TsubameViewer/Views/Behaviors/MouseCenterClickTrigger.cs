using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Markup;

namespace TsubameViewer.Views.Behaviors
{
	[ContentProperty(Name = nameof(CenterClickActions))]
    public sealed class MouseCenterClickTrigger : Behavior<FrameworkElement>
    {
		public ActionCollection CenterClickActions
		{
			get
			{
				if (GetValue(CenterClickActionsProperty) == null)
				{
					this.CenterClickActions = new ActionCollection();
				}
				return (ActionCollection)GetValue(CenterClickActionsProperty);
			}
			set { SetValue(CenterClickActionsProperty, value); }
		}

		public static readonly DependencyProperty CenterClickActionsProperty =
			DependencyProperty.Register(
				nameof(CenterClickActions),
				typeof(ActionCollection),
				typeof(MouseCenterClickTrigger),
				new PropertyMetadata(null));

        protected override void OnAttached()
        {
			AssociatedObject.PointerPressed += AssociatedObject_PointerPressed;
            AssociatedObject.PointerReleased += AssociatedObject_PointerReleased;
			
			{
				//Window.Current.CoreWindow.PointerWheelChanged += CoreWindow_PointerWheelChanged;
			}
			base.OnAttached();
        }

        protected override void OnDetaching()
        {
			AssociatedObject.PointerPressed -= AssociatedObject_PointerPressed;
			AssociatedObject.PointerReleased -= AssociatedObject_PointerReleased;

			base.OnDetaching();
        }

        DateTime prevPressedTime;
        private void AssociatedObject_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
			prevPressedTime = DateTime.Now;
			AssociatedObject.ReleasePointerCapture(e.Pointer);
			e.Handled = true;
		}

        private void AssociatedObject_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
			var point = e.GetCurrentPoint(AssociatedObject);			
            if (point.Properties.IsMiddleButtonPressed && DateTime.Now - prevPressedTime > TimeSpan.FromMilliseconds(50))
            {
				AssociatedObject.CapturePointer(e.Pointer);
				Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(this, this.CenterClickActions, e);
				e.Handled = true;
			}
		}
    }
}
