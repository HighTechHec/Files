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

			if (toolbarCustomizationWindow is null)
			{
				var frame = new Frame { RequestedTheme = appThemeModeService.AppThemeMode };

				toolbarCustomizationWindow = new WindowEx(460, 400);
				toolbarCustomizationWindow.PersistenceId = WindowPersistenceId;
				toolbarCustomizationWindow.Closed += ToolbarCustomizationWindow_Closed;

				var appWindow = toolbarCustomizationWindow.AppWindow;
				appWindow.Title = Strings.CustomizeToolbar.GetLocalizedResource();
				appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
				toolbarCustomizationWindow.ExtendsContentIntoTitleBar = true;
				appWindow.SetIcon(AppLifecycleHelper.AppIconPath);
				toolbarCustomizationWindow.IsMaximizable = false;
				toolbarCustomizationWindow.Content = frame;
				toolbarCustomizationWindow.SystemBackdrop = new AppSystemBackdrop(true);

				frame.Navigate(typeof(ToolbarCustomizationPage), toolbarCustomizationWindow, new SuppressNavigationTransitionInfo());
				if (frame.Content is ToolbarCustomizationPage toolbarCustomizationPage)
					toolbarCustomizationWindow.SetTitleBar(toolbarCustomizationPage.TitleBarElement);

				var width = Math.Max(1, Convert.ToInt32(760 * App.AppModel.AppWindowDPI));
				var height = Math.Max(1, Convert.ToInt32(560 * App.AppModel.AppWindowDPI));
				appWindow.Resize(new SizeInt32(width, height));
			}
			else if (toolbarCustomizationWindow.Content is Frame frame)
			{
				frame.RequestedTheme = appThemeModeService.AppThemeMode;
				if (frame.Content is ToolbarCustomizationPage toolbarCustomizationPage)
					toolbarCustomizationWindow.SetTitleBar(toolbarCustomizationPage.TitleBarElement);
			}

			appThemeModeService.SetAppThemeMode(
				toolbarCustomizationWindow,
				toolbarCustomizationWindow.AppWindow.TitleBar,
				appThemeModeService.AppThemeMode,
				callThemeModeChangedEvent: false);

			toolbarCustomizationWindow.AppWindow.Show();
			toolbarCustomizationWindow.Activate();
		}

		private static void ToolbarCustomizationWindow_Closed(object sender, WindowEventArgs _)
		{
			if (toolbarCustomizationWindow is not null)
				toolbarCustomizationWindow.Closed -= ToolbarCustomizationWindow_Closed;

			toolbarCustomizationWindow = null;
		}
	}
}