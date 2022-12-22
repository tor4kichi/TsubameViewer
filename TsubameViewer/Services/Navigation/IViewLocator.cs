using System;
using TsubameViewer.Contracts.Navigation;

namespace TsubameViewer.Services;

public sealed class ViewLocator : IViewLocator
{
    public Type ResolveView(string viewName)
    {
        return Type.GetType($"TsubameViewer.Views.{viewName}");
    }
}
