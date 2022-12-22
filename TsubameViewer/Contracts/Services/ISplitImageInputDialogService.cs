using System;
using System.Threading.Tasks;
using TsubameViewer.Views.Dialogs;

namespace TsubameViewer.Contracts.Services;

public interface ISplitImageInputDialogService
{
    Task<SplitImageInputDialogResult> GetSplitImageInputAsync();
}

public enum BookBindingDirection
{
    Right,
    Left,
}

public record struct SplitImageInputDialogResult(bool IsConfirm, double? AspectRatio, BookBindingDirection BindingDirection, Guid? encoderId);

