using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace TsubameViewer.Presentation.Views.UINavigation
{
    /// <summary>
    /// UINavigationControllerの入力をBehavior(Trigger)として扱えるようにします。
    /// Kindが設定されると定期検出処理が走るようになります。
    /// </summary>
    [ContentProperty(Name = "Actions")]
    public sealed class UINavigationTriggerBehavior : Behavior<FrameworkElement>
    {



        #region Dependency Properties


        // フォーカスがある時だけ入力を処理するか
        public bool IsRequireFocus
        {
            get { return (bool)GetValue(IsRequireFocusProperty); }
            set { SetValue(IsRequireFocusProperty, value); }
        }

        public static readonly DependencyProperty IsRequireFocusProperty =
            DependencyProperty.Register(
                nameof(IsRequireFocus),
                typeof(bool),
                typeof(UINavigationTriggerBehavior),
                new PropertyMetadata(false)
                );



        public UINavigationButtons Kind
        {
            get { return (UINavigationButtons)GetValue(KindProperty); }
            set { SetValue(KindProperty, value); }
        }

        public static readonly DependencyProperty KindProperty =
            DependencyProperty.Register(
                nameof(Kind),
                typeof(UINavigationButtons), 
                typeof(UINavigationTriggerBehavior), 
                new PropertyMetadata(UINavigationButtons.None)
                );

        
        public bool Hold
        {
            get { return (bool)GetValue(HoldProperty); }
            set { SetValue(HoldProperty, value); }
        }

        public static readonly DependencyProperty HoldProperty =
            DependencyProperty.Register(
                nameof(Hold),
                typeof(bool),
                typeof(UINavigationTriggerBehavior),
                new PropertyMetadata(false)
                );

        public bool IsEnabled
        {
            get { return (bool)GetValue(IsEnabledProperty); }
            set { SetValue(IsEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.Register(
                nameof(IsEnabled), 
                typeof(bool), 
                typeof(UINavigationTriggerBehavior), 
                new PropertyMetadata(true)
                );



        public ActionCollection Actions
        {
            get
            {
                if (GetValue(ActionsProperty) == null)
                {
                    return this.Actions = new ActionCollection();
                }
                return (ActionCollection)GetValue(ActionsProperty);
            }
            set { SetValue(ActionsProperty, value); }
        }

        public static readonly DependencyProperty ActionsProperty =
            DependencyProperty.Register(
                nameof(Actions),
                typeof(ActionCollection),
                typeof(UINavigationTriggerBehavior),
                new PropertyMetadata(null));


        #endregion

        bool _NowFocusingElement = false;

        DispatcherQueue _dispatcherQueue;

        protected override void OnAttached()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            this.AssociatedObject.GotFocus += AssociatedObject_GotFocus;
            this.AssociatedObject.LostFocus += AssociatedObject_LostFocus;

            UINavigationManager.OnPressing += UINavigationManager_OnPressing;
            UINavigationManager.OnPressed += Instance_Pressed;
            UINavigationManager.OnHolding += Instance_Holding;
        }

        bool _Holding = false;
        private void Instance_Holding(UINavigationManager sender, UINavigationButtons button)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (Hold && button.HasFlag(Kind))
                {
                    _Holding = true;
                }
            });
        }

        protected override void OnDetaching()
        {
            this.AssociatedObject.GotFocus -= AssociatedObject_GotFocus;
            this.AssociatedObject.LostFocus -= AssociatedObject_LostFocus;

            UINavigationManager.OnPressing -= UINavigationManager_OnPressing;
            UINavigationManager.OnPressed -= Instance_Pressed;
            UINavigationManager.OnHolding -= Instance_Holding;
            base.OnDetaching();
        }

        // Note: コンテキストメニュー上で押下した場合のエラーを回避する目的で押下時のフォーカス状態を取っている
        //       コンテキストメニュー内のインタラクションはAccept押下開始時に判定するため
        //       Accept押下終了後、ButtonUpタイミングでは既にコンテキストメニューからフォーカスが元要素に戻っている可能性がある
        bool _IsFocusingWhenPressing = false;
        private void UINavigationManager_OnPressing(UINavigationManager sender, UINavigationButtons buttons)
        {
            _IsFocusingWhenPressing = _NowFocusingElement;
        }

        private void Instance_Pressed(UINavigationManager sender, UINavigationButtons button)
        {
            _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
            {
                if (IsRequireFocus && !_IsFocusingWhenPressing)
                {
                    return;
                }

                if (!IsEnabled) { return; }

                if (InputPane.GetForCurrentView().Visible)
                {
                    return;
                }

                if (!button.HasFlag(Kind))
                {
                    return;
                }

                if (Hold && !_Holding)
                {
                    return;
                }

                _Holding = false;

                foreach (var action in Actions.Cast<IAction>())
                {
                    action.Execute(this.AssociatedObject, null);
                }
            });
        }

        private void AssociatedObject_LostFocus(object sender, RoutedEventArgs e)
        {
            _NowFocusingElement = false;
        }

        private void AssociatedObject_GotFocus(object sender, RoutedEventArgs e)
        {
            _NowFocusingElement = true;
        }
    }
}
