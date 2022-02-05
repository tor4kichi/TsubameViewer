using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace TsubameViewer.Presentation.Views.Behaviors
{
	// Note: IsHitTestVisible="True" Background="Transparent" が無いと
	//		 マウススクロールが反応しない問題が発生する

	public sealed class MouseWheelTrigger : Behavior<FrameworkElement>
    {
		public ActionCollection UpActions 
		{
			get
			{
				if (GetValue(UpActionsProperty) == null)
				{
					this.UpActions = new ActionCollection();
				}
				return (ActionCollection)GetValue(UpActionsProperty);
			}
			set { SetValue(UpActionsProperty, value); }
		}

		public static readonly DependencyProperty UpActionsProperty =
			DependencyProperty.Register(
				nameof(UpActions),
				typeof(ActionCollection),
				typeof(MouseWheelTrigger),
				new PropertyMetadata(null));



		public ActionCollection DownActions
		{
			get
			{
				if (GetValue(DownActionsProperty) == null)
				{
					this.DownActions = new ActionCollection();
				}
				return (ActionCollection)GetValue(DownActionsProperty);
			}
			set { SetValue(DownActionsProperty, value); }
		}

		public static readonly DependencyProperty DownActionsProperty =
			DependencyProperty.Register(
				nameof(DownActions),
				typeof(ActionCollection),
				typeof(MouseWheelTrigger),
				new PropertyMetadata(null));




        public bool IsEnabled
        {
            get { return (bool)GetValue(IsEnabledProperty); }
            set { SetValue(IsEnabledProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsEnabled.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.Register("IsEnabled", typeof(bool), typeof(MouseWheelTrigger), new PropertyMetadata(true));




        protected override void OnAttached()
        {
            this.Register();
        }

        protected override void OnDetaching()
        {
            this.Unregister();
        }


		private void Register()
		{
			var fe = this.AssociatedObject as FrameworkElement;
			if (fe == null) { return; }
			fe.Unloaded += this.Fe_Unloaded;

			if (AssociatedObject is UIElement)
			{
				var ui = AssociatedObject as UIElement;
				ui.PointerWheelChanged += Ui_PointerWheelChanged;
			}
			else
			{
				//App.Current.Window.CoreWindow.PointerWheelChanged += CoreWindow_PointerWheelChanged;
			}
		}

		private void Ui_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
		{
			if (!IsEnabled) { return; }

			var pointer = args.GetCurrentPoint(null);
			
			if (pointer.Properties.MouseWheelDelta > 0)
			{
                Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(this, this.UpActions, args);
				args.Handled = true;
			}
			else if (pointer.Properties.MouseWheelDelta < 0)
			{
                Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(this, this.DownActions, args);
				args.Handled = true;
			}

		}

		private void CoreWindow_PointerWheelChanged(Windows.UI.Core.CoreWindow sender, Windows.UI.Core.PointerEventArgs args)
		{
			if (!IsEnabled) { return; }

			var pointer = args.CurrentPoint;

			
			if (pointer.Properties.MouseWheelDelta > 0)
			{
                Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(this, this.UpActions, args);
				args.Handled = true;
			}
			else if (pointer.Properties.MouseWheelDelta < 0)
			{
                Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(this, this.DownActions, args);
				args.Handled = true;
			}
		}


		private void Process(PointerPoint pp)
		{

		}

		private void Unregister()
		{
			//App.Current.Window.CoreWindow.PointerWheelChanged -= CoreWindow_PointerWheelChanged;

			if (this.AssociatedObject is FrameworkElement fe)
            {
				fe.Unloaded -= this.Fe_Unloaded;
			}
		}


		

		private void Fe_Unloaded(object sender, RoutedEventArgs e)
		{
			(sender as FrameworkElement).Unloaded -= this.Fe_Unloaded;
			this.Unregister();
		}
	}
}
