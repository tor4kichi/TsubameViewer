﻿using Microsoft.Toolkit.Uwp.UI.Behaviors;
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
            }

            base.OnAttached();
        }

        protected override void OnDetaching()
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.ChoosingItemContainer -= AssociatedObject_ChoosingItemContainer;
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
