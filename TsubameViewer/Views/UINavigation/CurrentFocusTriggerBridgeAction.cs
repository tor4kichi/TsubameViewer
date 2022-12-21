using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;

namespace TsubameViewer.Views.UINavigation
{
	// UINatigationTriggerBehaviorをListViewなどに適用した際に
	// Focusされた子アイテムをFocusManager.GetFocusedElement()で強引に取得し
	// 入れ子になったInvokeCommandAction等で子アイテムのDataContextを渡せるようにする

	[ContentProperty(Name = nameof(Actions))]
    public sealed class BypassToCurrentFocusElementDataContextAction : DependencyObject, IAction
    {
		public ActionCollection Actions
		{
			get
			{
				if (GetValue(ActionsProperty) == null)
				{
					this.Actions = new ActionCollection();
				}
				return (ActionCollection)GetValue(ActionsProperty);
			}
			set { SetValue(ActionsProperty, value); }
		}

		public static readonly DependencyProperty ActionsProperty =
			DependencyProperty.Register(
				nameof(Actions),
				typeof(ActionCollection),
				typeof(BypassToCurrentFocusElementDataContextAction),
				new PropertyMetadata(null));

		public object Execute(object sender, object parameter)
        {
            var currentFocusElement = FocusManager.GetFocusedElement();

			Interaction.ExecuteActions(sender, Actions, (currentFocusElement as FrameworkElement).DataContext ?? (currentFocusElement as SelectorItem).Content);

            return true;
        }
    }

	[ContentProperty(Name = nameof(Actions))]
	public sealed class BypassToCurrentFocusElementAction : DependencyObject, IAction
	{
		public ActionCollection Actions
		{
			get
			{
				if (GetValue(ActionsProperty) == null)
				{
					this.Actions = new ActionCollection();
				}
				return (ActionCollection)GetValue(ActionsProperty);
			}
			set { SetValue(ActionsProperty, value); }
		}

		public static readonly DependencyProperty ActionsProperty =
			DependencyProperty.Register(
				nameof(Actions),
				typeof(ActionCollection),
				typeof(BypassToCurrentFocusElementAction),
				new PropertyMetadata(null));

		public object Execute(object sender, object parameter)
		{
			var currentFocusElement = FocusManager.GetFocusedElement();

			Interaction.ExecuteActions(sender, Actions, currentFocusElement);

			return true;
		}
	}
}
