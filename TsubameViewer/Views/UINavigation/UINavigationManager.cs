using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Gaming.Input;
using Windows.UI.Xaml;
using System.Threading;
using Windows.UI.Core;
using System.Diagnostics;
using TsubameViewer.Core.Infrastructure;
#nullable enable
namespace TsubameViewer.Views.UINavigation;

public delegate void UINavigationButtonEventHandler(UINavigationManager sender, UINavigationButtons buttons);

public class UINavigationManager : IDisposable
{
    public static event UINavigationButtonEventHandler? OnPressing;

    /// <summary>
    /// ボタンを離した瞬間を通知するイベントです。
    /// </summary>
    public static event UINavigationButtonEventHandler? OnPressed;

    /// <summary>
    /// ボタンを押し続けた場合に通知されるイベントです。<br />
    /// 一度のボタン押下中に対して一回だけホールドを検出して通知します。
    /// </summary>
    public static event UINavigationButtonEventHandler? OnHolding;




    static UINavigationManager _instance;

    static readonly TimeSpan _inputPollingInterval = TimeSpan.FromMilliseconds(16); 
    static readonly TimeSpan _holdDetectTime = TimeSpan.FromSeconds(1);
    static readonly UINavigationButtons[] _inputDetectTargets = ((UINavigationButtons[])Enum.GetValues(typeof(UINavigationButtons))).Skip(1).ToArray();

    Timer? _pollingTimer;

    UINavigationButtons _prevPressingButtons;
    UINavigationButtons _processedHoldingButtons;
    Dictionary<UINavigationButtons, TimeSpan> _buttonHold = new Dictionary<UINavigationButtons, TimeSpan>();

    Core.AsyncLock _updateLock = new ();

    bool _isDisposed;

    public static bool NowControllerConnected => UINavigationController.UINavigationControllers.Count > 0;

    public static bool InitialEnabling = true;

    static UINavigationManager()
    {
        _instance = new UINavigationManager();
    }


    private UINavigationManager()
    {
        foreach (var target in _inputDetectTargets)
        {
            _buttonHold[target] = TimeSpan.Zero;
        }

        Window.Current.Activated += Current_Activated;

        UINavigationController.UINavigationControllerAdded += UINavigationController_UINavigationControllerAdded;
        UINavigationController.UINavigationControllerRemoved += UINavigationController_UINavigationControllerRemoved;
        
        IsEnabled = InitialEnabling;
    }

    
    bool _isEnabled;
    public bool IsEnabled
    {
        get { return _isEnabled; }
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;

                if (_isEnabled)
                {
                    ActivatePolling();
                }
                else
                {
                    DeactivatePolling();
                }
            }
        }
    }

    void Current_Activated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == CoreWindowActivationState.Deactivated)
        {
            DeactivatePolling();
        }
        else
        {
            ActivatePolling();
        }
    }

    void UINavigationController_UINavigationControllerAdded(object sender, UINavigationController e)
    {
        ActivatePolling();
    }

    void UINavigationController_UINavigationControllerRemoved(object sender, UINavigationController e)
    {
        DeactivatePolling();
    }



    public void Dispose()
    {
        _pollingTimer?.Dispose();
        _pollingTimer = null;
        _isDisposed = true;
    }


    /// <summary>
    /// 入力検出処理を開始する。
    /// ただし、Kindが None である場合はアクティブ化を行わない。
    /// 検出終了には DeactivatePolling を呼び出す。
    /// </summary>
    void ActivatePolling()
    {
        if (_isDisposed) { return; }

        if (!IsEnabled) { return; }

        if (UINavigationController.UINavigationControllers.Count == 0) { return; }

        if (_pollingTimer == null)
        {
            _pollingTimer = new Timer(
                _ => _DispatcherTimer_Tick()
                , null
                , TimeSpan.Zero
                , _inputPollingInterval
                );
        }
    }

    void DeactivatePolling()
    {
        if (_isDisposed) { return; }

        if (UINavigationController.UINavigationControllers.Count > 0) { return; }

        _pollingTimer?.Dispose();
        _pollingTimer = null;
    }


    bool _nowUpdating = false;
    void _DispatcherTimer_Tick()
    {
        d().FireAndForgetSafe();
        async Task d()
        {

            if (_nowUpdating)
            {
                return;
            }

            using (var releaser = await _updateLock.LockAsync(default))
            {
                try
                {
                    _nowUpdating = true;

                    // コントローラー入力をチェック
                    foreach (var controller in UINavigationController.UINavigationControllers.Take(1))
                    {
                        var currentInput = controller.GetCurrentReading();

                        // ボタンを離した瞬間を検出
                        var pressing = RequiredUINavigationButtonsHelper.ToUINavigationButtons(currentInput.RequiredButtons)
                            | OptionalUINavigationButtonsHelper.ToUINavigationButtons(currentInput.OptionalButtons);

                        var trigger = pressing & (_prevPressingButtons ^ pressing);
                        if (trigger != UINavigationButtons.None)
                        {
                            Debug.WriteLine($"pressing : {trigger}");
                            OnPressing?.Invoke(this, trigger);
                        }

                        var released = _prevPressingButtons & (_prevPressingButtons ^ pressing);
                        if (released != UINavigationButtons.None)
                        {
                            Debug.WriteLine($"released : {released}");
                            OnPressed?.Invoke(this, released);
                        }

                        // ホールド入力の検出
                        UINavigationButtons holdingButtons = UINavigationButtons.None;
                        foreach (var target in _inputDetectTargets)
                        {
                            if (pressing.HasFlag(target))
                            {
                                if (!_processedHoldingButtons.HasFlag(target))
                                {
                                    var time = _buttonHold[target] += _inputPollingInterval;

                                    if (time > _holdDetectTime)
                                    {
                                        holdingButtons |= target;
                                        _processedHoldingButtons |= target;
                                    }
                                }
                            }
                            else
                            {
                                _buttonHold[target] = TimeSpan.Zero;
                                _processedHoldingButtons = (((UINavigationButtons)0) ^ target) & _processedHoldingButtons;
                            }
                        }

                        if (holdingButtons != UINavigationButtons.None)
                        {
                            OnHolding?.Invoke(this, holdingButtons);
                        }

                        // トリガー検出用に前フレームの入力情報を保存
                        _prevPressingButtons = pressing;
                    }
                }
                finally
                {
                    _nowUpdating = false;
                }
            }
        }
    }
}