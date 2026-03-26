// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Graphics;

namespace Files.App.Views.Settings
{
	internal static class ToolbarCustomizationDialog
	{
		private const string WindowPersistenceId = "ToolbarCustomizationWindow";
		private static WindowEx? customizationWindow;

		public static void Show()
		{
			var themeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
			var window = customizationWindow;
			if (window is null)
				customizationWindow = window = CreateCustomizationWindow(themeService);
			else if (window.Content is Frame frame)
				frame.RequestedTheme = themeService.AppThemeMode;

			UpdateWindowTitleBar(window);

			themeService.SetAppThemeMode(
				window,
				window.AppWindow.TitleBar,
				themeService.AppThemeMode,
				callThemeModeChangedEvent: false);

			window.AppWindow.Show();
			window.Activate();
		}

		private static WindowEx CreateCustomizationWindow(IAppThemeModeService themeService)
		{
			var frame = new Frame { RequestedTheme = themeService.AppThemeMode };
			var window = new WindowEx(460, 400)
			{
				PersistenceId = WindowPersistenceId,
				ExtendsContentIntoTitleBar = true,
				IsMaximizable = false,
				Content = frame,
				SystemBackdrop = new AppSystemBackdrop(true),
			};

			window.Closed += ToolbarCustomizationWindow_Closed;

			var appWindow = window.AppWindow;
			appWindow.Title = Strings.CustomizeToolbar.GetLocalizedResource();
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
			appWindow.SetIcon(AppLifecycleHelper.AppIconPath);

			frame.Navigate(typeof(ToolbarCustomizationPage), window, new SuppressNavigationTransitionInfo());
			ResizeWindow(appWindow);
			return window;
		}

		private static void UpdateWindowTitleBar(WindowEx window)
		{
			// The page owns the draggable title bar element, so reapply it after navigation and theme updates.
			if ((window.Content as Frame)?.Content is ToolbarCustomizationPage page)
				window.SetTitleBar(page.TitleBarElement);
		}

		private static void ResizeWindow(Microsoft.UI.Windowing.AppWindow appWindow)
		{
			var width = Math.Max(1, Convert.ToInt32(760 * App.AppModel.AppWindowDPI));
			var height = Math.Max(1, Convert.ToInt32(560 * App.AppModel.AppWindowDPI));
			appWindow.Resize(new SizeInt32(width, height));
		}

		private static void ToolbarCustomizationWindow_Closed(object sender, WindowEventArgs _)
		{
			if (customizationWindow is not null)
				customizationWindow.Closed -= ToolbarCustomizationWindow_Closed;

			customizationWindow = null;
		}
	}
}