using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
#nullable enable
namespace TsubameViewer.Views.Behaviors;
public sealed class SwipeDistanceBehavior : ManipulationBehaviorBase
{
    public override ManipulationModes ManipulationModes => ManipulationModes.TranslateX | ManipulationModes.TranslateRailsX | ManipulationModes.TranslateY | ManipulationModes.TranslateRailsY;

    public double ProgressX
    {
        get { return (double)GetValue(ProgressXProperty); }
        set { SetValue(ProgressXProperty, value); }
    }

    public static readonly DependencyProperty ProgressXProperty =
        DependencyProperty.Register(nameof(ProgressX), typeof(double), typeof(SwipeDistanceBehavior), new PropertyMetadata(0d));

    public double ProgressY
    {
        get { return (double)GetValue(ProgressYProperty); }
        set { SetValue(ProgressYProperty, value); }
    }

    public static readonly DependencyProperty ProgressYProperty =
        DependencyProperty.Register(nameof(ProgressY), typeof(double), typeof(SwipeDistanceBehavior), new PropertyMetadata(0d));




    public double XPixelsToOneUnit
    {
        get { return (double)GetValue(XPixelsToOneUnitProperty); }
        set { SetValue(XPixelsToOneUnitProperty, value); }
    }

    public static readonly DependencyProperty XPixelsToOneUnitProperty =
        DependencyProperty.Register(nameof(XPixelsToOneUnit), typeof(double), typeof(SwipeDistanceBehavior), new PropertyMetadata(1d));




    public double YPixelsToOneUnit
    {
        get { return (double)GetValue(YPixelsToOneUnitProperty); }
        set { SetValue(YPixelsToOneUnitProperty, value); }
    }

    public static readonly DependencyProperty YPixelsToOneUnitProperty =
        DependencyProperty.Register(nameof(YPixelsToOneUnit), typeof(double), typeof(SwipeDistanceBehavior), new PropertyMetadata(1d));



    public event TypedEventHandler<SwipeDistanceBehavior, SwipeDistanceInvokedEventArgs>? Invoked;

    protected override bool ManipulationStating(object sender, ManipulationStartingRoutedEventArgs e)
    {
        return true;
    }
    long _startedTime;
    double _invToXUnit;
    double _invToYUnit;
    protected override bool ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
    {
        _startedTime = TimeProvider.System.GetTimestamp();
        ProgressX = 0;
        ProgressY = 0;
        _invToXUnit = 1d / XPixelsToOneUnit;
        _invToYUnit = 1d / YPixelsToOneUnit;
        return true;
    }

    protected override bool ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
    {        
        ProgressX = Math.Round(e.Cumulative.Translation.X * _invToXUnit);
        ProgressY = Math.Round(e.Cumulative.Translation.Y * _invToYUnit);
        return true;
    }

    protected override bool ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (Invoked == null) { return false; }

        Invoked.Invoke(this, new SwipeDistanceInvokedEventArgs(
            Math.Round(e.Cumulative.Translation.X * _invToXUnit),
            Math.Round(e.Cumulative.Translation.Y * _invToYUnit), 
            TimeProvider.System.GetElapsedTime(_startedTime)));

        ProgressX = 0;
        ProgressY = 0;
        return true;
    }
}

public readonly struct SwipeDistanceInvokedEventArgs(double X, double Y, TimeSpan Elapsed)
{
    public readonly double X = X;
    public readonly double Y = Y;
    public readonly TimeSpan Elapsed = Elapsed;
}