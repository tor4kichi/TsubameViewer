using Microsoft.Toolkit.Uwp.UI.Behaviors;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Markup;

namespace TsubameViewer.Presentation.Views.Behaviors
{
    [ContentProperty(Name = nameof(Actions))]
    public class ListViewBaseFirstAppearTriggerBehavior : BehaviorBase<ListViewBase>
    {
        public ActionCollection Actions
        {
            get { return (ActionCollection)GetValue(ActionsProperty); }
            set { SetValue(ActionsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Actions.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register("Actions", typeof(ActionCollection), typeof(ListViewBaseFirstAppearTriggerBehavior), new PropertyMetadata(null));


        public ListViewBaseFirstAppearTriggerBehavior()
        {
            Actions = new ActionCollection();
        }

        protected override void OnAttached()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.ChoosingItemContainer += AssociatedObject_ChoosingItemContainer;
                AssociatedObject.ContainerContentChanging += AssociatedObject_ContainerContentChanging;
            }

            base.OnAttached();
        }

        private void AssociatedObject_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.Item is ViewModels.PageNavigation.StorageItemViewModel itemVM)
            {
                System.Diagnostics.Debug.WriteLine($"phase: {args.Phase}, {itemVM.Name}");
            }
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.ChoosingItemContainer -= AssociatedObject_ChoosingItemContainer;
                AssociatedObject.ContainerContentChanging -= AssociatedObject_ContainerContentChanging;
            }
            _map.Clear();
            base.OnDetaching();
        }

        public void Clear()
        {
            _map.Clear();
        }

        HashSet<object> _map = new HashSet<object>();

        private void AssociatedObject_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            var context = args.Item;
            if (_map.Contains(context))
            {
                return;
            }

            Microsoft.Xaml.Interactivity.Interaction.ExecuteActions(context, Actions, null);

            _map.Add(context);
        }
    }
}
