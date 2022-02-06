using Microsoft.UI.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TsubameViewer.Models.Infrastructure;
using Windows.Graphics;

namespace TsubameViewer.Models.Domain
{
    public sealed class WindowSettings : FlagsRepositoryBase
    {
        public WindowSettings()
        {
            _lastOverlappedWindowPosition = Read(default(Point), nameof(LastOverlappedWindowPosition));
            _lastOverrapedWindowSize = Read(default(Size), nameof(LastOverlappedWindowSize));
            _lastCompactOverlayWindowPosition = Read(default(Point), nameof(LastOverlappedWindowPosition));
            _lastCompactOverlayWindowSize = Read(default(Size), nameof(LastOverlappedWindowSize));
            _lastWindowPresenterKind = Read(default(AppWindowPresenterKind), nameof(LastWindowPresenterKind));
        }

        private Point _lastOverlappedWindowPosition;
        public Point LastOverlappedWindowPosition
        {
            get => _lastOverlappedWindowPosition;
            set => SetProperty(ref _lastOverlappedWindowPosition, value);
        }

        private Size _lastOverrapedWindowSize;
        public Size LastOverlappedWindowSize
        {
            get => _lastOverrapedWindowSize;
            set => SetProperty(ref _lastOverrapedWindowSize, value);
        }


        private Point _lastCompactOverlayWindowPosition;
        public Point LastCompactOverlayWindowPosition
        {
            get => _lastCompactOverlayWindowPosition;
            set => SetProperty(ref _lastCompactOverlayWindowPosition, value);
        }

        private Size _lastCompactOverlayWindowSize;
        public Size LastCompactOverlayWindowSize
        {
            get => _lastCompactOverlayWindowSize;
            set => SetProperty(ref _lastCompactOverlayWindowSize, value);
        }

        private AppWindowPresenterKind _lastWindowPresenterKind;
        public AppWindowPresenterKind LastWindowPresenterKind
        {
            get => _lastWindowPresenterKind;
            set => SetProperty(ref _lastWindowPresenterKind, value);
        }
    }
}
