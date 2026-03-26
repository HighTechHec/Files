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
		private static WindowEx? toolbarCustomizationWindow;

		public static void Show()
		{
			var appThemeModeService = Ioc.Default.GetRequiredService<IAppThemeModeService>();
			var window = toolbarCustomizationWindow;
			if (window is null)
				toolbarCustomizationWindow = window = CreateWindow(appThemeModeService);
			else if (window.Content is Frame frame)
				frame.RequestedTheme = appThemeModeService.AppThemeMode;

			UpdateTitleBar(window);

			appThemeModeService.SetAppThemeMode(
				window,
				window.AppWindow.TitleBar,
				appThemeModeService.AppThemeMode,
				callThemeModeChangedEvent: false);

			window.AppWindow.Show();
			window.Activate();
		}

		private static WindowEx CreateWindow(IAppThemeModeService appThemeModeService)
		{
			var frame = new Frame { RequestedTheme = appThemeModeService.AppThemeMode };
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
			Resize(appWindow);
			return window;
		}

		private static void UpdateTitleBar(WindowEx window)
		{
			if ((window.Content as Frame)?.Content is ToolbarCustomizationPage page)
				window.SetTitleBar(page.TitleBarElement);
		}

		private static void Resize(Microsoft.UI.Windowing.AppWindow appWindow)
		{
			var width = Math.Max(1, Convert.ToInt32(760 * App.AppModel.AppWindowDPI));
			var height = Math.Max(1, Convert.ToInt32(560 * App.AppModel.AppWindowDPI));
			appWindow.Resize(new SizeInt32(width, height));
		}

		private static void ToolbarCustomizationWindow_Closed(object sender, WindowEventArgs _)
		{
			if (toolbarCustomizationWindow is not null)
				toolbarCustomizationWindow.Closed -= ToolbarCustomizationWindow_Closed;

			toolbarCustomizationWindow = null;
		}
	}
}