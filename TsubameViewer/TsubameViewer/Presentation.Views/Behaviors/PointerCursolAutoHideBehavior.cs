using System;
using Microsoft.UI.Xaml;
using Windows.Devices.Input;
using Microsoft.Xaml.Interactivity;
using Windows.Foundation;
using Windows.ApplicationModel.Core;
using System.Diagnostics;
using Windows.UI.Core;
using Microsoft.UI.Dispatching;

namespace TsubameViewer.Presentation.Views.Behaviors
{
    /* 
     * 使い方
     * 
     * ポインターを自動非表示させたいUI要素のIntaractivity.Behaviorsに配置することで利用できます。
     * 
     * IsAutoHideEnabledに動画プレイヤー等のコントロールUIのVisibilityを反転させたBool値をBindingする使い方を想定してます。
     */

    /*
     * 実装のポイント
     * 
     * Window上のUI要素が普段トリガーするポインター関連イベントが
     * ポインターカーソルを消した場合にはトリガーされなくなります。
     * 
     * そのため、カーソルを非表示にした場合は
     * MouseDeviceからマウスの移動を検出する必要があります。
     * 
     */


    /// <summary>
    /// ポインターが操作されていない時にカーソル表示を自動非表示するビヘイビア
    /// </summary>
    public class PointerCursolAutoHideBehavior : Behavior<FrameworkElement>
    {
        // カーソルを元に戻すためのやつ
        CoreCursor _DefaultCursor;

        Point _LastCursorPosition;

        // 自動非表示のためのタイマー
        // DispatcherTimerはUIスレッドフレンドリーなタイマー
        private readonly DispatcherQueueTimer _AutoHideTimer;

        private readonly bool IsDebugOutputEnabled = false;

        // このビヘイビアを保持しているElement内にカーソルがあるかのフラグ
        // PointerEntered/PointerExitedで変更される
        bool _IsCursorInsideAssociatedObject = false;



        #region IsAutoHideEnabled DependencyProperty

        public static readonly DependencyProperty IsAutoHideEnabledProperty =
           DependencyProperty.Register(nameof(IsAutoHideEnabled)
                   , typeof(Boolean)
                   , typeof(PointerCursolAutoHideBehavior)
                   , new PropertyMetadata(true, OnIsAutoHideEnabledPropertyChanged)
               );


        /// <summary>
        /// IsAutoHideEnabledがTrueに設定されるとマウスの移動が無くなった後、
        /// AutoHideDelayに設定された時間後、カーソルが非表示に自動設定されます。
        /// </summary>
        public Boolean IsAutoHideEnabled
        {
            get { return (Boolean)GetValue(IsAutoHideEnabledProperty); }
            set { SetValue(IsAutoHideEnabledProperty, value); }
        }

        public static void OnIsAutoHideEnabledPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            var source = (PointerCursolAutoHideBehavior)sender;
            source.ResetAutoHideTimer();
        }

        #endregion


        #region AutoHideDelay DependencyProperty

        public static readonly DependencyProperty AutoHideDelayProperty =
          DependencyProperty.Register(nameof(AutoHideDelay)
                  , typeof(TimeSpan)
                  , typeof(PointerCursolAutoHideBehavior)
                  , new PropertyMetadata(TimeSpan.FromSeconds(1), OnAutoHideDelayPropertyChanged)
              );

        /// <summary>
        /// マウスが動かなくなってから非表示になるまでの時間を指定します。<br />
        /// Delayに0秒を設定するとユーザーのマウス操作が困難になるので注意してください。
        /// </summary>
        public TimeSpan AutoHideDelay
        {
            get { return (TimeSpan)GetValue(AutoHideDelayProperty); }
            set { SetValue(AutoHideDelayProperty, value); }
        }

        public static void OnAutoHideDelayPropertyChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            PointerCursolAutoHideBehavior source = (PointerCursolAutoHideBehavior)sender;
            source._AutoHideTimer.Interval = source.AutoHideDelay;
        }

        #endregion


        private static bool GetIsWindowActive()
        {
            return App.Current.Window.CoreWindow.ActivationMode == CoreWindowActivationMode.ActivatedInForeground;
        }


        public PointerCursolAutoHideBehavior()
        {
            _AutoHideTimer = DispatcherQueue.CreateTimer();
            _AutoHideTimer.Tick += AutoHideTimer_Tick;
            _AutoHideTimer.IsRepeating = false;           
        }

        protected override void OnAttached()
        {
            _LastCursorPosition = GetPointerPosition();

            _AutoHideTimer.Interval = AutoHideDelay;
            _IsCursorInsideAssociatedObject = IsCursorInWindow();
            _prevIsVisible = true;
            ResetAutoHideTimer();

            AssociatedObject.PointerEntered -= AssociatedObject_PointerEntered;
            AssociatedObject.PointerExited -= AssociatedObject_PointerExited;
            AssociatedObject.PointerEntered += AssociatedObject_PointerEntered;
            AssociatedObject.PointerExited += AssociatedObject_PointerExited;

            App.Current.Window.Activated -= Current_Activated;
            App.Current.Window.Activated += Current_Activated;

            MouseDevice.GetForCurrentView().MouseMoved -= CursorSetter_MouseMoved;
            MouseDevice.GetForCurrentView().MouseMoved += CursorSetter_MouseMoved;

            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;
            AssociatedObject.Unloaded += AssociatedObject_Unloaded;

            base.OnAttached();
        }

        private void Current_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != WindowActivationState.Deactivated)
            {
                ResetAutoHideTimer();
            }
        }

        bool IsUnlaoded = false;
        private void AssociatedObject_Unloaded(object sender, RoutedEventArgs e)
        {
            IsUnlaoded = true;

            App.Current.Window.Activated -= Current_Activated;
            MouseDevice.GetForCurrentView().MouseMoved -= CursorSetter_MouseMoved;

            _AutoHideTimer.Stop();
            App.Current.Window.CoreWindow.PointerCursor = _DefaultCursor;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PointerEntered -= AssociatedObject_PointerEntered;
            AssociatedObject.PointerExited -= AssociatedObject_PointerExited;

            AssociatedObject.Unloaded -= AssociatedObject_Unloaded;

            base.OnDetaching();
        }

        private void ResetAutoHideTimer()
        {
            _AutoHideTimer.Stop();

            if (IsAutoHideEnabled)
            {
                _AutoHideTimer.Start();
            }
            
            CursorVisibilityChanged(true);
        }

        bool _prevIsVisible = true;
        
        private void CursorVisibilityChanged(bool isVisible)
        {
            if (_DefaultCursor == null) { throw new InvalidOperationException($"Default cursor is can not be null."); }

            // 表示状態変化のトリガーを検出して処理する
            if (_prevIsVisible != isVisible)
            {
                if (isVisible)
                {
                    RestoreCursorPosition();

                    Debug.WriteLineIf(IsDebugOutputEnabled, $"Show Mouse Cursor.");
                }
                else 
                {
                    RecordCursorPosition();

                    Debug.WriteLineIf(IsDebugOutputEnabled, $"Hide Mouse Cursor.");
                }
            }

            _prevIsVisible = isVisible;
        }


        private void RecordCursorPosition()
        {
            _LastCursorPosition = GetPointerPosition();
        }

        private void RestoreCursorPosition()
        {
            var windowBound = App.Current.Window.CoreWindow.Bounds;
            //App.Current.Window.CoreWindow.PointerPosition = new Point(windowBound.Left + _LastCursorPosition.X, windowBound.Top + _LastCursorPosition.Y);
        }

        private void AutoHideTimer_Tick(object sender, object e)
        {
            if (IsUnlaoded) { return; }
            if (GetIsWindowActive() is false) { return; }

            if (IsAutoHideEnabled && _IsCursorInsideAssociatedObject)
            {
                CursorVisibilityChanged(false);
            }

            Debug.WriteLineIf(IsDebugOutputEnabled, "AutoHideTimer Stop!");
        }

        private void CursorSetter_MouseMoved(MouseDevice sender, MouseEventArgs args)
        {
            if (IsUnlaoded) { return; }

            RecordCursorPosition();

            // マウスホイールを動かした時等には移動していなくても呼ばれるがその場合は無視する
            if (args.MouseDelta.X == 0 && args.MouseDelta.Y == 0) { return; }
            
            CursorVisibilityChanged(true);
            ResetAutoHideTimer();
        }


        private void AssociatedObject_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsUnlaoded) { return; }

            _IsCursorInsideAssociatedObject = true;

            Debug.WriteLineIf(IsDebugOutputEnabled, "PointerEntered");
        }

        private void AssociatedObject_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (IsUnlaoded) { return; }

            _IsCursorInsideAssociatedObject = false;

            CursorVisibilityChanged(true);
            ResetAutoHideTimer();

            Debug.WriteLineIf(IsDebugOutputEnabled, "PointerExited");
        }

        #region this code copy from VLC WinRT

        // source: https://code.videolan.org/videolan/vlc-winrt/blob/afb08b71d5989ebe03d9109c19c9aba541b37c6f/app/VLC_WinRT.Shared/Services/RunTime/MouseService.cs

        // lisence : https://code.videolan.org/videolan/vlc-winrt/blob/master/LICENSE
        /*
             Most of the media code engine is licensed under LGPL, like libVLC.
            The application is dual-licensed under GPLv2/MPL and the license might change later,
            if need be.
             */

        public static Point GetPointerPosition()
        {
            Window currentWindow = App.Current.Window;
            Point point;

            try
            {
                point = currentWindow.CoreWindow.PointerPosition;
            }
            catch (UnauthorizedAccessException)
            {
                return new Point(double.NegativeInfinity, double.NegativeInfinity);
            }

            Rect bounds = currentWindow.Bounds;
            return new Point(point.X - bounds.X, point.Y - bounds.Y);
        }

        public static bool IsCursorInWindow()
        {
            var pos = GetPointerPosition();
            //if (pos == null) return false;


            return pos.Y > 0 &&
                   pos.Y < App.Current.Window.Bounds.Height &&
                   pos.X > 0 &&
                   pos.X < App.Current.Window.Bounds.Width;
        }

        #endregion
    }
}