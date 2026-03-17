// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Files.App.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;

namespace Files.App.UserControls
{
	public sealed partial class Toolbar : UserControl
	{
		private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
		private readonly ICommandManager Commands = Ioc.Default.GetRequiredService<ICommandManager>();
		private readonly IModifiableCommandManager ModifiableCommands = Ioc.Default.GetRequiredService<IModifiableCommandManager>();
		private readonly IAddItemService addItemService = Ioc.Default.GetRequiredService<IAddItemService>();
		private bool isToolbarRefreshQueued;
		private bool isRefreshingToolbar;


		[GeneratedDependencyProperty]
		public partial NavigationToolbarViewModel? ViewModel { get; set; }

		[GeneratedDependencyProperty]
		public partial bool ShowViewControlButton { get; set; }

		[GeneratedDependencyProperty]
		public partial bool ShowPreviewPaneButton { get; set; }

		public Toolbar()
		{
			InitializeComponent();
			Loaded += Toolbar_Loaded;
			Unloaded += Toolbar_Unloaded;
		}

		private void Toolbar_Loaded(object sender, RoutedEventArgs e)
		{
			SubscribeCommandStateChanges();
			PopulateToolbarItems();
			UserSettingsService.AppearanceSettingsService.PropertyChanged += AppearanceSettings_PropertyChanged;
		}

		private void Toolbar_Unloaded(object sender, RoutedEventArgs e)
		{
			UnsubscribeCommandStateChanges();
			UserSettingsService.AppearanceSettingsService.PropertyChanged -= AppearanceSettings_PropertyChanged;
		}

		private void SubscribeCommandStateChanges()
		{
			foreach (var command in Commands)
				command.PropertyChanged += Command_PropertyChanged;
		}

		private void UnsubscribeCommandStateChanges()
		{
			foreach (var command in Commands)
				command.PropertyChanged -= Command_PropertyChanged;
		}

		private void Command_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(IRichCommand.IsExecutable))
				RequestToolbarRefresh();
		}

		private void RequestToolbarRefresh()
		{
			if (isToolbarRefreshQueued)
				return;

			isToolbarRefreshQueued = true;
			_ = DispatcherQueue.TryEnqueue(() =>
			{
				isToolbarRefreshQueued = false;
				if (isRefreshingToolbar)
					return;

				isRefreshingToolbar = true;
				try
				{
					PopulateToolbarItems();
				}
				finally
				{
					isRefreshingToolbar = false;
				}
			});
		}

		partial void OnViewModelChanged(NavigationToolbarViewModel? newValue)
		{
			if (newValue?.InstanceViewModel is not null)
			{
				newValue.InstanceViewModel.PropertyChanged += InstanceViewModel_PropertyChanged;
				PopulateToolbarItems();
			}
		}

		private void InstanceViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(CurrentInstanceViewModel.IsPageTypeNotHome)
				or nameof(CurrentInstanceViewModel.IsPageTypeRecycleBin))
				RequestToolbarRefresh();
		}

		private void AppearanceSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(IAppearanceSettingsService.CustomToolbarItems))
				RequestToolbarRefresh();
		}

		private void ContextCommandBar_Loaded(object sender, RoutedEventArgs e)
		{
			PopulateToolbarItems();
		}

		private void PopulateToolbarItems()
		{
			if (ContextCommandBar is null)
				return;

			ContextCommandBar.PrimaryCommands.Clear();
			var activeContexts = GetActiveToolbarContexts();

			foreach (var (contextId, settingsEntry, contextItemIndex) in EnumerateToolbarSettingsEntries())
			{
				if (!ShouldShowContext(contextId, activeContexts))
					continue;

				if (CreateToolbarElement(contextId, settingsEntry) is { } element)
				{
					if (element is AppBarButton button)
						AttachHideToolbarItemContextFlyout(button, contextId, settingsEntry, contextItemIndex);

					ContextCommandBar.PrimaryCommands.Add(element);
				}
			}

			UpdateSeparatorVisibility();
		}

		private HashSet<string> GetActiveToolbarContexts()
		{
			var activeContexts = new HashSet<string>(StringComparer.Ordinal)
			{
				ToolbarDefaultsTemplate.AlwaysVisibleContextId,
			};

			foreach (var command in Commands)
			{
				if (command.Code is CommandCodes.None
					|| !command.IsAccessibleGlobally
					|| !command.IsExecutable)
					continue;

var contextId = ToolbarItemDescriptor.ResolveToolbarSectionId(command.Code.ToString(), Commands);

				if (string.Equals(contextId, ToolbarDefaultsTemplate.RecycleBinContextId, StringComparison.Ordinal)
					&& ViewModel?.InstanceViewModel?.IsPageTypeRecycleBin != true)
				{
					continue;
				}

				activeContexts.Add(contextId);
			}

			return activeContexts;
		}

		private static bool ShouldShowContext(string contextId, ISet<string> activeContexts)
		{
			if (string.Equals(contextId, ToolbarDefaultsTemplate.AlwaysVisibleContextId, StringComparison.Ordinal))
				return true;

			if (ToolbarItemDescriptor.IsOtherContextsId(contextId))
				return !activeContexts.Any(ToolbarItemDescriptor.IsSpecificContextId);

			return activeContexts.Contains(contextId);
		}

		private IEnumerable<(string ContextId, ToolbarItemSettingsEntry SettingsEntry, int? ContextItemIndex)> EnumerateToolbarSettingsEntries()
		{
			var itemsByContext = GetMutableToolbarItemsByContext();

			foreach (var contextId in ToolbarDefaultsTemplate.ContextOrder)
			{
				if (!itemsByContext.TryGetValue(contextId, out var contextItems) || contextItems.Count == 0)
					continue;

				// Auto-inject a separator before non-AlwaysVisible contexts
				if (!string.Equals(contextId, ToolbarDefaultsTemplate.AlwaysVisibleContextId, StringComparison.Ordinal))
					yield return (contextId, new ToolbarItemSettingsEntry(ToolbarItemDescriptor.SeparatorCommandCode, showIcon: false, showLabel: false), null);

				for (int index = 0; index < contextItems.Count; index++)
					yield return (contextId, contextItems[index], index);
			}
		}

		private void AttachHideToolbarItemContextFlyout(AppBarButton button, string contextId, ToolbarItemSettingsEntry settingsEntry, int? contextItemIndex)
		{
			var commandCode = settingsEntry.CommandCode;
			if (string.IsNullOrEmpty(commandCode) || ToolbarItemDescriptor.IsSeparatorCommandCode(commandCode) || contextItemIndex is null)
				return;

			var customizeToolbarMenuItem = new MenuFlyoutItem
			{
				Text = Strings.CustomizeToolbar.GetLocalizedResource(),
			};

			customizeToolbarMenuItem.Click += (_, _) => Commands.CustomizeToolbar.Execute(null);

			var unpinMenuItem = new MenuFlyoutItem
			{
				Text = Strings.Unpin.GetLocalizedResource(),
			};

			unpinMenuItem.Click += (_, _) => HideToolbarItem(contextId, contextItemIndex.Value, settingsEntry);

			var contextFlyout = new MenuFlyout();
			contextFlyout.Items.Add(customizeToolbarMenuItem);
			contextFlyout.Items.Add(unpinMenuItem);
			button.ContextFlyout = contextFlyout;
		}

		private void HideToolbarItem(string contextId, int contextItemIndex, ToolbarItemSettingsEntry settingsEntry)
		{
			var itemsByContext = GetMutableToolbarItemsByContext();
			if (!itemsByContext.TryGetValue(contextId, out var contextItems))
				return;

			if (contextItemIndex < 0 || contextItemIndex >= contextItems.Count)
				return;

			if (!AreSameSettingsEntry(contextItems[contextItemIndex], settingsEntry))
				return;

			contextItems.RemoveAt(contextItemIndex);

			UserSettingsService.AppearanceSettingsService.CustomToolbarItems = itemsByContext;

			PopulateToolbarItems();
		}

		private static bool AreSameSettingsEntry(ToolbarItemSettingsEntry left, ToolbarItemSettingsEntry right)
			=> left.ShowIcon == right.ShowIcon
				&& left.ShowLabel == right.ShowLabel
&& string.Equals(left.CommandCode, right.CommandCode, StringComparison.Ordinal);

		private Dictionary<string, List<ToolbarItemSettingsEntry>> GetMutableToolbarItemsByContext()
		{
			if (UserSettingsService.AppearanceSettingsService.CustomToolbarItems is { Count: > 0 } savedContextItems)
			{
				var itemsByContext = savedContextItems.ToDictionary(
					pair => pair.Key,
						pair => pair.Value.Select(static item => new ToolbarItemSettingsEntry(commandCode: item.CommandCode, commandGroup: item.CommandGroup, showIcon: item.ShowIcon, showLabel: item.ShowLabel)).ToList(),
					StringComparer.Ordinal);

				ApplyNewDefaultToolbarItemsIfNeeded(itemsByContext, hasExistingToolbarConfig: true);
				return itemsByContext;
			}

			var defaultItems = ToolbarDefaultsTemplate.CreateDefaultItemsByContext();
			ApplyNewDefaultToolbarItemsIfNeeded(defaultItems, hasExistingToolbarConfig: false);
			return defaultItems;
		}

		private void ApplyNewDefaultToolbarItemsIfNeeded(Dictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext, bool hasExistingToolbarConfig)
		{
			var appearanceSettings = UserSettingsService.AppearanceSettingsService;
			var currentDefaultTemplate = ToolbarDefaultsTemplate.CreateDefaultItemsByContext();
			var currentIdentifiers = ToolbarDefaultsTemplate.CreateDefaultIdentifiersByContext();

			if (!hasExistingToolbarConfig)
			{
				if (!ToolbarDefaultsTemplate.AreTemplatesEqual(appearanceSettings.LastKnownToolbarDefaults, currentIdentifiers))
					appearanceSettings.LastKnownToolbarDefaults = currentIdentifiers;

				return;
			}

			var previousDefaultTemplate = appearanceSettings.LastKnownToolbarDefaults;
			if (previousDefaultTemplate is null || previousDefaultTemplate.Count == 0)
			{
				appearanceSettings.LastKnownToolbarDefaults = currentIdentifiers;
				return;
			}

			var hasChanges = ToolbarDefaultsTemplate.TryMergeNewDefaults(itemsByContext, previousDefaultTemplate, currentDefaultTemplate);

			appearanceSettings.LastKnownToolbarDefaults = currentIdentifiers;

			if (hasChanges)
			{
				appearanceSettings.CustomToolbarItems = itemsByContext;
			}
		}

		private ICommandBarElement? CreateToolbarElement(string contextId, ToolbarItemSettingsEntry settingsEntry)
		{
			var showIcon = settingsEntry.ShowIcon;
			var showLabel = settingsEntry.ShowLabel;
			var keepVisibleWhenDisabled = string.Equals(contextId, ToolbarDefaultsTemplate.AlwaysVisibleContextId, StringComparison.Ordinal);

			// Check if separator first (separators are always added)
			var commandCode = settingsEntry.CommandCode;
			if (!string.IsNullOrEmpty(commandCode) && ToolbarItemDescriptor.IsSeparatorCommandCode(commandCode))
				return new AppBarSeparator();

			// Handle group
			if (!string.IsNullOrEmpty(settingsEntry.CommandGroup))
			{
				var group = Commands.Groups.All.FirstOrDefault(g => g.Name == settingsEntry.CommandGroup);
				if (group is null)
					return null;

				showIcon &= !string.IsNullOrEmpty(group.Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(group.Glyph.BaseGlyph);
				return !showIcon && !showLabel
					? null
					: CreateGroupButton(group, showIcon, showLabel);
			}

			if (string.IsNullOrEmpty(commandCode))
				return null;

			if (Enum.TryParse<CommandCodes>(commandCode, out var code) && code != CommandCodes.None)
			{
				var mod = ModifiableCommands[code];
				var command = mod.Code != CommandCodes.None ? mod : Commands[code];
				showIcon &= !string.IsNullOrEmpty(command.Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(command.Glyph.BaseGlyph);
				return !showIcon && !showLabel
					? null
					: CreateCommandButton(command, showIcon, showLabel, keepVisibleWhenDisabled: keepVisibleWhenDisabled);
			}

			return null;
		}

		/// <summary>
		/// Creates a base AppBarButton with label, tooltip, icon, access key, automation ID and hotkey.
		/// </summary>
		private AppBarButton CreateBaseButton(bool showIcon, bool showLabel, string label, string tooltip, string? accessKey, string? automationId, RichGlyph glyph, string? hotKeyText = null)
		{
			var button = new AppBarButton
			{
				Width = double.NaN,
				MinWidth = showIcon ? 40 : 0,
				Label = label,
				LabelPosition = showLabel ? CommandBarLabelPosition.Default : CommandBarLabelPosition.Collapsed,
			};

			if (!showIcon)
				button.Loaded += CollapseButtonIconViewbox;

			ToolTipService.SetToolTip(button, tooltip);

			if (!string.IsNullOrEmpty(accessKey))
			{
				button.AccessKey = accessKey;
				button.AccessKeyInvoked += AppBarButton_AccessKeyInvoked;
			}

			if (!string.IsNullOrEmpty(automationId))
				AutomationProperties.SetAutomationId(button, automationId);

			if (hotKeyText is not null)
				button.KeyboardAcceleratorTextOverride = hotKeyText;

			return button;
		}

		/// <summary>
		/// Creates a base AppBarButton for a command, extracting all display properties from the command.
		/// </summary>
		private static string GetExtendedLabelWithHotKey(IRichCommand command)
			=> command.HotKeyText is null ? command.ExtendedLabel : $"{command.ExtendedLabel} ({command.HotKeyText})";

		/// <summary>
		/// Creates a base AppBarButton for a command, using the extended label for top-level toolbar items.
		/// </summary>
		private AppBarButton CreateBaseButton(IRichCommand command, bool showIcon, bool showLabel)
			=> CreateBaseButton(showIcon, showLabel, command.ExtendedLabel, GetExtendedLabelWithHotKey(command),
				command.AccessKey, command.AutomationId, command.Glyph, command.HotKeyText);

		/// <summary>
		/// Creates a base AppBarButton for a command group using the group's own display text.
		/// </summary>
		private AppBarButton CreateBaseButton(CommandGroup group, bool showIcon, bool showLabel)
			=> CreateBaseButton(showIcon, showLabel,
				group.DisplayName,
				group.DisplayName,
				group.AccessKey, group.AutomationId, group.Glyph,
				null);

		private static void CollapseButtonIconViewbox(object sender, RoutedEventArgs e)
		{
			var button = (AppBarButton)sender;
			button.Loaded -= CollapseButtonIconViewbox;
			if (button.FindDescendant("ContentViewbox") is Viewbox viewbox)
				viewbox.Visibility = Visibility.Collapsed;
		}

		private void BindVisibilityToIsExecutable(AppBarButton button, IRichCommand command)
		{
			button.SetBinding(VisibilityProperty, new Binding { Source = command, Path = new PropertyPath("IsExecutable"), Mode = BindingMode.OneWay, Converter = (IValueConverter)Resources["BoolToVisibilityConverter"] });
			button.RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => UpdateSeparatorVisibility());
		}

		private AppBarButton CreateGroupButton(CommandGroup group, bool showIcon, bool showLabel)
		{
			var button = CreateBaseButton(group, showIcon, showLabel);

			var flyout = new MenuFlyout
			{
				Placement = group is NewItemCommandGroup
					? Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft
					: Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom
			};

			flyout.Opening += (s, _) => PopulateGroupFlyout((MenuFlyout)s, group);

			button.Flyout = flyout;
			button.Style = (Style)Resources["ToolBarAppBarButtonFlyoutStyle"];
			if (showIcon)
				ToolbarButtonGlyphHelper.Apply(button, group.Glyph, useStyledTemplate: true);
			button.IsEnabled = group.Commands.Any(code => code is not CommandCodes.None && Commands[code].IsExecutable);

			return button;
		}

		private AppBarButton CreateCommandButton(IRichCommand command, bool showIcon, bool showLabel, bool keepVisibleWhenDisabled)
		{
			var button = CreateBaseButton(command, showIcon, showLabel);
			var useStyledTemplate = showIcon && !string.IsNullOrEmpty(command.Glyph.ThemedIconStyle);

			if (useStyledTemplate)
				button.Style = (Style)Resources["ToolBarAppBarButtonFlyoutStyle"];

			if (showIcon)
				ToolbarButtonGlyphHelper.Apply(button, command.Glyph, useStyledTemplate);

			button.Command = command;

			if (!keepVisibleWhenDisabled)
				BindVisibilityToIsExecutable(button, command);

			return button;
		}

		private void UpdateSeparatorVisibility()
		{
			if (ContextCommandBar is null)
				return;

			var commands = ContextCommandBar.PrimaryCommands;
			bool prevWasSeparator = true;
			AppBarSeparator? lastSeparator = null;

			for (int i = 0; i < commands.Count; i++)
			{
				if (commands[i] is AppBarSeparator sep)
				{
					sep.Visibility = prevWasSeparator ? Visibility.Collapsed : Visibility.Visible;
					if (!prevWasSeparator)
						lastSeparator = sep;
					prevWasSeparator = true;
				}
				else if (commands[i] is UIElement { Visibility: Visibility.Visible })
				{
					prevWasSeparator = false;
				}
			}

			if (lastSeparator is not null && prevWasSeparator)
				lastSeparator.Visibility = Visibility.Collapsed;
		}

		private void PopulateGroupFlyout(MenuFlyout flyout, CommandGroup group)
		{
			flyout.Items.Clear();
			foreach (var code in group.Commands)
				if (Commands[code] is { Code: not CommandCodes.None } cmd)
					flyout.Items.Add(CreateGroupMenuItem(cmd));

			if (group is NewItemCommandGroup && ViewModel is not null && ViewModel.InstanceViewModel.CanCreateFileInPage)
			{
				var entries = addItemService.GetEntries();
				if (entries is not null && entries.Count > 0)
				{
					flyout.Items.Add(new MenuFlyoutSeparator());
					var keyFormat = $"D{entries.Count.ToString().Length}";
					for (int i = 0; i < entries.Count; i++)
					{
						var menuItem = CreateShellNewEntryMenuItem(entries[i]);
						menuItem.AccessKey = (i + 1).ToString(keyFormat);
						menuItem.Command = ViewModel.CreateNewFileCommand;
						menuItem.CommandParameter = entries[i];
						flyout.Items.Add(menuItem);
					}
				}
			}
		}

		private static MenuFlyoutItem CreateShellNewEntryMenuItem(ShellNewEntry newEntry)
		{
			if (!string.IsNullOrEmpty(newEntry.IconBase64))
			{
				byte[] bitmapData = Convert.FromBase64String(newEntry.IconBase64);
				using var ms = new MemoryStream(bitmapData);
				var image = new BitmapImage();
				_ = image.SetSourceAsync(ms.AsRandomAccessStream());

				return new MenuFlyoutItemWithImage
				{
					Text = newEntry.Name,
					BitmapIcon = image,
				};
			}

			return new MenuFlyoutItem
			{
				Text = newEntry.Name,
				Icon = new FontIcon
				{
					Glyph = "\xE7C3"
				},
			};
		}

		private static MenuFlyoutItem CreateGroupMenuItem(IRichCommand command)
		{
			var item = new MenuFlyoutItem
			{
				Text = command.Label,
				Command = command,
				Visibility = command.IsExecutable ? Visibility.Visible : Visibility.Collapsed,
				Icon = command.Glyph.ToFontIcon(),
			};

			if (!string.IsNullOrWhiteSpace(command.AccessKey))
				item.AccessKey = command.AccessKey;
			if (command.HotKeyText is string hotKey)
				item.KeyboardAcceleratorTextOverride = hotKey;
			if (!string.IsNullOrEmpty(command.AutomationId))
				AutomationProperties.SetAutomationId(item, command.AutomationId);

			return item;
		}

		private void SortGroup_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
		{
			if (sender is MenuFlyoutSubItem menu)
			{
				var items = menu.Items
					.TakeWhile(item => item is not MenuFlyoutSeparator)
					.Where(item => item.IsEnabled)
					.ToList();

				string format = $"D{items.Count.ToString().Length}";

				for (ushort index = 0; index < items.Count; ++index)
				{
					items[index].AccessKey = (index + 1).ToString(format);
				}
			}

		}

		private void AppBarButton_AccessKeyInvoked(UIElement sender, AccessKeyInvokedEventArgs args)
		{
			// Suppress access key invocation if any dialog is open
			if (VisualTreeHelper.GetOpenPopupsForXamlRoot(MainWindow.Instance.Content.XamlRoot).Any())
				args.Handled = true;
		}

		private void LayoutButton_Click(object sender, RoutedEventArgs e)
		{
			// Hide flyout after choosing a layout
			// Check if LayoutFlyout is not null to handle cases where UI elements are unloaded via x:Load
			LayoutFlyout?.Hide();
		}

		private void CustomizeToolbar_Click(object sender, RoutedEventArgs e)
		{
			Commands.CustomizeToolbar.Execute(null);
		}
	}
}
