using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TsubameViewer.Core.Infrastructure;

namespace TsubameViewer.ViewModels;

public sealed class ViewerSettings : FlagsRepositoryBase
{
    public ViewerSettings()
    {
        _isDetectSimiralyFileNameNeighborsEnabled = Read(true, nameof(IsDetectSimiralyFileNameNeighborsEnabled));
        _thresholdOfSimilarityFileNameNaighborsNormalized = Read(0.60, nameof(ThresholdOfSimilarityFileNameNaighborsNormalized));
        _isAutoMoveToNextEnabled = Read(true, nameof(IsAutoMoveToNextEnabled));
    }


    bool _isDetectSimiralyFileNameNeighborsEnabled;
    public bool IsDetectSimiralyFileNameNeighborsEnabled
    {
        get => _isDetectSimiralyFileNameNeighborsEnabled;
        set => SetProperty(ref _isDetectSimiralyFileNameNeighborsEnabled, value);
    }

    double _thresholdOfSimilarityFileNameNaighborsNormalized;
    public double ThresholdOfSimilarityFileNameNaighborsNormalized
    {
        get => _thresholdOfSimilarityFileNameNaighborsNormalized;
        set => SetProperty(ref _thresholdOfSimilarityFileNameNaighborsNormalized, Math.Clamp(value, 0, 1));
    }

    bool _isAutoMoveToNextEnabled;
    public bool IsAutoMoveToNextEnabled
    {
        get => _isAutoMoveToNextEnabled;
        set => SetProperty(ref _isAutoMoveToNextEnabled, value);
    }

}
