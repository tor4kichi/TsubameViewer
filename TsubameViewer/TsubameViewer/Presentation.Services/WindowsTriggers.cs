using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;

namespace TsubameViewer.Presentation.Services
{
    public sealed class WindowsTriggers : ObservableObject
    {
		private readonly Window _window;
        private readonly AppWindow _appWindow;

        private static AppWindow GetAppWindowForWindow(Window window)
		{
			IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
			WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
			return AppWindow.GetFromWindowId(myWndId);
		}

		private static Windows.UI.ViewManagement.UIViewSettings GetUIViewSettings(Window window)
		{
			IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
			return Windows.UI.ViewManagement.UIViewSettingsInterop.GetForWindow(hWnd);
		}

		public WindowsTriggers(Window window)
        {
			_window = window;
			_appWindow = GetAppWindowForWindow(_window);

			IsFullScreen = _appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
			InteractionMode = GetUIViewSettings(_window).UserInteractionMode;

			if (!Windows.ApplicationModel.DesignMode.DesignModeEnabled)
			{
				var weakEvent =
					new WeakEventListener<WindowsTriggers, object, WindowSizeChangedEventArgs>(this)
					{
						OnEventAction = (instance, source, eventArgs) => instance.OnWindowSizeChanged(source, eventArgs),
						OnDetachAction = (weakEventListener) => _window.SizeChanged -= weakEventListener.OnEvent
					};

				_window.SizeChanged += weakEvent.OnEvent;
			}
		}


		private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs args)
		{
			IsFullScreen = _appWindow.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
			InteractionMode = GetUIViewSettings(_window).UserInteractionMode;
		}

		public DeviceFamily DeviceFamily { get; } = GetDeviceFamily();

		private static DeviceFamily GetDeviceFamily()
		{
			var deviceFamily = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily;
			var idiom = Xamarin.Essentials.DeviceInfo.Idiom;
			if (idiom == Xamarin.Essentials.DeviceIdiom.Phone)
				return DeviceFamily.Phone;
			else if (idiom == Xamarin.Essentials.DeviceIdiom.Tablet)
				return DeviceFamily.Tablet;
			else if (idiom == Xamarin.Essentials.DeviceIdiom.Desktop)
				return DeviceFamily.Desktop;
			else if (idiom == Xamarin.Essentials.DeviceIdiom.TV)
				return DeviceFamily.TV;
			else if (idiom == Xamarin.Essentials.DeviceIdiom.Watch)
				return DeviceFamily.Watch;
			else
			{
				if (deviceFamily == "Windows.Desktop")
					return DeviceFamily.Desktop;
				else if (deviceFamily == "Windows.Team")
					return DeviceFamily.Team;
				else if (deviceFamily == "Windows.IoT")
					return DeviceFamily.IoT;
				else if (deviceFamily == "Windows.Holographic")
					return DeviceFamily.Holographic;
				else if (deviceFamily == "Windows.Xbox")
					return DeviceFamily.TV;
				else
					return DeviceFamily.Unknown;
			}
		}



		private bool _isFullScreen;
        public bool IsFullScreen
		{
			get => _isFullScreen;
			private set => SetProperty(ref _isFullScreen, value);
		}



		private UserInteractionMode _interactionMode;
		public UserInteractionMode InteractionMode
        {
			get => _interactionMode;
			private set => SetProperty(ref _interactionMode, value);
        }
	}


	/// <summary>
	/// Device Families
	/// </summary>
	public enum DeviceFamily
	{
		Unknown = 0,
		Phone = 1,
		Tablet = 2,
		Desktop = 3,
		TV = 4,
		Watch = 5,
		Team = 6,
		IoT = 7,
		Holographic = 8,
	}
}
