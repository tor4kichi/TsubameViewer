using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;

namespace TsubameViewer.Views.Behaviors;


public abstract class ManipulationBehaviorBase : Behavior<UIElement>
{
    public bool IsEnabled
    {
        get { return (bool)GetValue(IsEnabledProperty); }
        set { SetValue(IsEnabledProperty, value); }
    }

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.Register("IsEnabled", typeof(bool), typeof(ManipulationBehaviorBase), new PropertyMetadata(true, OnIsEnabledPropertyChanged));

    static void OnIsEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var _this = (ManipulationBehaviorBase)d;
        if ((bool)e.NewValue)
        {
            _this.AttachHandler();
        }
        else
        {
            _this.DetacheHandler();
        }
    }

    public abstract ManipulationModes ManipulationModes { get; }


    protected override void OnAttached()
    {
        AttachHandler();
        base.OnAttached();
    }

    protected override void OnDetaching()
    {
        DetacheHandler();
        base.OnDetaching();
    }

    bool _isAttached;
    void AttachHandler()
    {
        DetacheHandler();

        if (_isAttached) { return; }
        if (!IsEnabled) { return; }
        if (AssociatedObject == null) { return; }

        if (_handlersMap.TryGetValue(AssociatedObject, out var list) is false)
        {
            list = new List<ManipulationBehaviorBase>();
            _handlersMap.TryAdd(AssociatedObject, list);
        }

        int oldCount = list.Count;
        list.Add(this);
        _isAttached = true;

        if (oldCount == 0 && list.Count >= 1)
        {
            AssociatedObject.PointerPressed += AssociatedObject_PointerPressed;
            AssociatedObject.PointerReleased += AssociatedObject_PointerReleased;
            AssociatedObject.PointerCanceled += AssociatedObject_PointerCanceled;            
            AssociatedObject.ManipulationMode = list.Aggregate(ManipulationModes.None, (seed, x) => seed | x.ManipulationModes);
            AssociatedObject.ManipulationStarting += AssociatedObject_ManipulationStarting;
            AssociatedObject.ManipulationStarted += AssociatedObject_ManipulationStarted;
            AssociatedObject.ManipulationDelta += AssociatedObject_ManipulationDelta;
            AssociatedObject.ManipulationCompleted += AssociatedObject_ManipulationCompleted;
            AssociatedObject.ManipulationInertiaStarting += AssociatedObject_ManipulationInertiaStarting;            
        }
    }

    static ConcurrentDictionary<UIElement, bool> _pointerPressedMap = new();
    static ConcurrentDictionary<UIElement, List<ManipulationBehaviorBase>> _handlersMap = new();
    void DetacheHandler()
    {
        if (!_isAttached) { return; }
        var removedHandler = _handlersMap.Remove(AssociatedObject, out var list);
        list.Remove(this);
        var removedPressedMap = _pointerPressedMap.Remove(AssociatedObject, out var pressed);

        _isAttached = false;

        if (list.Count == 0)
        {
            AssociatedObject.PointerPressed -= AssociatedObject_PointerPressed;
            AssociatedObject.PointerReleased -= AssociatedObject_PointerReleased;
            AssociatedObject.PointerCanceled -= AssociatedObject_PointerCanceled;
            AssociatedObject.ManipulationMode = Windows.UI.Xaml.Input.ManipulationModes.System;
            AssociatedObject.ManipulationStarting -= AssociatedObject_ManipulationStarting;
            AssociatedObject.ManipulationStarted -= AssociatedObject_ManipulationStarted;
            AssociatedObject.ManipulationDelta -= AssociatedObject_ManipulationDelta;
            AssociatedObject.ManipulationCompleted -= AssociatedObject_ManipulationCompleted;
            AssociatedObject.ManipulationInertiaStarting -= AssociatedObject_ManipulationInertiaStarting;
        }
    }

    void AssociatedObject_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var item = (UIElement)sender;
        var pt = e.GetCurrentPoint(null);        
        if (pt.Properties.IsLeftButtonPressed && item.CapturePointer(e.Pointer))
        {
            _pointerPressedMap[item] = true;
        }
    }

    void AssociatedObject_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        var item = (UIElement)sender;
        item.ReleasePointerCapture(e.Pointer);
        _pointerPressedMap[item] = false;
        NowManipulation = false;
    }

    void AssociatedObject_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var item = (UIElement)sender;
        item.ReleasePointerCapture(e.Pointer);
        _pointerPressedMap[item] = false;
        NowManipulation = false;
    }


    protected virtual bool ManipulationStating(object sender, Windows.UI.Xaml.Input.ManipulationStartingRoutedEventArgs e) => false;
    protected virtual bool ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e) => false;
    protected virtual bool ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e) => false;
    protected virtual bool ManipulationInertiaStarting(object sender, Windows.UI.Xaml.Input.ManipulationInertiaStartingRoutedEventArgs e) => false;
    protected virtual bool ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e) => false;

    static void AssociatedObject_ManipulationStarting(object sender, Windows.UI.Xaml.Input.ManipulationStartingRoutedEventArgs e)
    {                            
        var item = (UIElement)sender;        
        if (e.Handled) { return; }        
        if (!_pointerPressedMap.TryGetValue(item, out bool isPointerPressed) || !isPointerPressed) { return; }
        if (_handlersMap.TryGetValue(item, out var behaviors) is false) { throw new InvalidOperationException(); }
        foreach (var behavior in behaviors)
        {            
            e.Handled |= behavior.ManipulationStating(sender, e);
        }
    }

    static void AssociatedObject_ManipulationStarted(object sender, Windows.UI.Xaml.Input.ManipulationStartedRoutedEventArgs e)
    {        
        var item = (UIElement)sender;
        if (!_pointerPressedMap.TryGetValue(item, out bool isPointerPressed) || !isPointerPressed) { return; }
        if (e.Handled) { return; }
        if (_handlersMap.TryGetValue(item, out var behaviors) is false) { throw new InvalidOperationException(); }        
        foreach (var behavior in behaviors)
        {
            e.Handled |= behavior.ManipulationStarted(sender, e);
            behavior.NowManipulation = true;
        }
    }

    static void AssociatedObject_ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
    {        
        var item = (UIElement)sender;
        if (!_pointerPressedMap.TryGetValue(item, out bool isPointerPressed) || !isPointerPressed) { return; }
        if (_handlersMap.TryGetValue(item, out var behaviors) is false) { throw new InvalidOperationException(); }
        foreach (var behavior in behaviors)
        {
            e.Handled |= behavior.ManipulationDelta(sender, e);
        }
    }

    static void AssociatedObject_ManipulationInertiaStarting(object sender, Windows.UI.Xaml.Input.ManipulationInertiaStartingRoutedEventArgs e)
    {        
        if (_handlersMap.TryGetValue((UIElement)sender, out var behaviors) is false) { throw new InvalidOperationException(); }
        foreach (var behavior in behaviors)
        {
            e.Handled |= behavior.ManipulationInertiaStarting(sender, e);
        }
    }

    static void AssociatedObject_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
    {        
        var item = (UIElement)sender;
        if (!_pointerPressedMap.TryGetValue(item, out bool isPointerPressed) || !isPointerPressed) { return; }
        if (_handlersMap.TryGetValue(item, out var behaviors) is false) { throw new InvalidOperationException(); }
        foreach (var behavior in behaviors)
        {
            e.Handled |= behavior.ManipulationCompleted(sender, e);
            behavior.NowManipulation = false;
        }

        _pointerPressedMap[item] = false;
    }



    public bool NowManipulation
    {
        get { return (bool)GetValue(NowManipulationProperty); }
        set { SetValue(NowManipulationProperty, value); }
    }

    public static readonly DependencyProperty NowManipulationProperty =
        DependencyProperty.Register(nameof(NowManipulation), typeof(bool), typeof(ManipulationBehaviorBase), new PropertyMetadata(false));
}


public sealed class TranslationManipulationBehavior : ManipulationBehaviorBase
{
    public override ManipulationModes ManipulationModes => ManipulationModes.TranslateX | ManipulationModes.TranslateY;

    public double TranslationX
    {
        get { return (double)GetValue(TranslationXProperty); }
        set { SetValue(TranslationXProperty, value); }
    }

    public static readonly DependencyProperty TranslationXProperty =
        DependencyProperty.Register("TranslationX", typeof(double), typeof(TranslationManipulationBehavior), new PropertyMetadata(0d));

    public double TranslationY
    {
        get { return (double)GetValue(TranslationYProperty); }
        set { SetValue(TranslationYProperty, value); }
    }

    public static readonly DependencyProperty TranslationYProperty =
        DependencyProperty.Register("TranslationY", typeof(double), typeof(TranslationManipulationBehavior), new PropertyMetadata(0d));


    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register("Min", typeof(double), typeof(TranslationManipulationBehavior), new PropertyMetadata(0.1d));

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register("Max", typeof(double), typeof(TranslationManipulationBehavior), new PropertyMetadata(5d));


    double _startTranslationX;
    double _startTranslationY;
    void SetTranslation(double x, double y)
    {
        TranslationX = Math.Clamp(x, Min, Max);
        TranslationY = Math.Clamp(y, Min, Max);        
    }

    protected override bool ManipulationStating(object sender, ManipulationStartingRoutedEventArgs e)
    {
        _startTranslationX = TranslationX;
        _startTranslationY = TranslationY;
        e.Mode |= ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        return true;
    }

    protected override bool ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        return true;
    }

    protected override bool ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        SetTranslation(_startTranslationX + e.Cumulative.Translation.X, _startTranslationY + e.Cumulative.Translation.Y);
        return true;
    }

    protected override bool ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        SetTranslation(_startTranslationX + e.Cumulative.Translation.X, _startTranslationY + e.Cumulative.Translation.Y);        
        return true;
    }
}


public sealed class ScaleManipulationBehavior : ManipulationBehaviorBase
{
    public override ManipulationModes ManipulationModes => ManipulationModes.Scale;

    public double Scale
    {
        get { return (double)GetValue(ScaleProperty); }
        set { SetValue(ScaleProperty, value); }
    }

    public static readonly DependencyProperty ScaleProperty =
        DependencyProperty.Register("Scale", typeof(double), typeof(ScaleManipulationBehavior), new PropertyMetadata(0d));

    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }
    
    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register("Min", typeof(double), typeof(ScaleManipulationBehavior), new PropertyMetadata(0.1d));

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register("Max", typeof(double), typeof(ScaleManipulationBehavior), new PropertyMetadata(5d));


    double _startScale;
    void SetScale(double scale)
    {
        Scale = Math.Clamp(scale, Min, Max);
    }

    protected override bool ManipulationStating(object sender, ManipulationStartingRoutedEventArgs e)
    {
        _startScale = Scale;
        return true;
    }

    protected override bool ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        return true;
    }

    protected override bool ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        SetScale(_startScale + e.Cumulative.Scale - 1);
        return true;
    }

    protected override bool ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        SetScale(_startScale + e.Cumulative.Scale - 1);
        return true;
    }
}


public sealed class RotationManipulationBehavior : ManipulationBehaviorBase
{
    public override ManipulationModes ManipulationModes => ManipulationModes.Rotate;

    public double Rotation
    {
        get { return (double)GetValue(RotationProperty); }
        set { SetValue(RotationProperty, value); }
    }

    public static readonly DependencyProperty RotationProperty =
        DependencyProperty.Register("Rotation", typeof(double), typeof(RotationManipulationBehavior), new PropertyMetadata(0d));

    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly DependencyProperty MinProperty =
        DependencyProperty.Register("Min", typeof(double), typeof(RotationManipulationBehavior), new PropertyMetadata(0d));

    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register("Max", typeof(double), typeof(RotationManipulationBehavior), new PropertyMetadata(0d));

    double _startRotation;
    void SetRotation(double scale)
    {
        Rotation = Math.Clamp(scale, Min, Max);
    }

    protected override bool ManipulationStating(object sender, ManipulationStartingRoutedEventArgs e)
    {
        _startRotation = Rotation;
        return true;
    }

    protected override bool ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {        
        return true;
    }

    protected override bool ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        SetRotation(_startRotation + e.Cumulative.Rotation);
        return true;
    }

    protected override bool ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        SetRotation(_startRotation + e.Cumulative.Rotation);
        return true;
    }
}
