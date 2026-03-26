// Copyright (c) Files Community
// Licensed under the MIT License.

using CommunityToolkit.WinUI;
using Files.App.Helpers;
using Files.App.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Specialized;
using Windows.ApplicationModel.DataTransfer;

namespace Files.App.Views.Settings
{
	public sealed partial class ToolbarCustomizationPage : Page
	{
		private ToolbarCustomizationViewModel ViewModel => (ToolbarCustomizationViewModel)DataContext;
		private Style PreviewFlyoutButtonStyle => (Style)Resources["ToolBarAppBarButtonFlyoutStyle"];
		private ToolbarItemDescriptor? draggedAvailableItem;
		private ObservableCollection<ToolbarItemDescriptor>? subscribedContextToolbarItems;
		private WindowEx? hostWindow;
		// Unloaded also runs after Save/Cancel closes the host window, so only auto-restore when the close was not requested by the view model.
		private bool skipSessionRestoreOnUnload;
		private static readonly Thickness ItemDividerThickness = new(0, 0, 0, 1);
		private static readonly Thickness NoBorderThickness = new(0);
		public FrameworkElement TitleBarElement => WindowTitleBar;

		public ToolbarCustomizationPage()
		{
			DataContext = Ioc.Default.GetRequiredService<ToolbarCustomizationViewModel>();

			InitializeComponent();
			Loaded += ToolbarCustomizationPage_Loaded;
			Unloaded += ToolbarCustomizationPage_Unloaded;
		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			base.OnNavigatedTo(e);
			hostWindow = e.Parameter as WindowEx;
		}

		private void ToolbarCustomizationPage_Loaded(object sender, RoutedEventArgs e)
		{
			ViewModel.BeginToolbarCustomizationSession();
			skipSessionRestoreOnUnload = false;
			ViewModel.CloseRequested += ViewModel_CloseRequested;

			UpdatePreviewSubscriptions(subscribe: true);
			RebuildPreviewCommandBar();
		}

		private void ToolbarCustomizationPage_Unloaded(object sender, RoutedEventArgs e)
		{
			ViewModel.CloseRequested -= ViewModel_CloseRequested;
			UpdatePreviewSubscriptions(subscribe: false);

			if (!skipSessionRestoreOnUnload)
				ViewModel.CancelToolbarCustomizationSession();
		}

		private void UpdatePreviewSubscriptions(bool subscribe)
		{
			if (subscribe)
				ViewModel.PropertyChanged += ViewModel_PropertyChanged;
			else
				ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

			UpdateToolbarCollectionSubscription(ViewModel.AlwaysVisibleToolbarItems, subscribe);
			UpdateContextPreviewSubscription(subscribe ? ViewModel.ToolbarItems : null);
		}

		private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ToolbarCustomizationViewModel.SelectedToolbarContextId))
				return;

			UpdateContextPreviewSubscription(ViewModel.ToolbarItems);
			RebuildPreviewCommandBar();
		}

		private void UpdateContextPreviewSubscription(ObservableCollection<ToolbarItemDescriptor>? items)
		{
			var nextItems = ReferenceEquals(items, ViewModel.AlwaysVisibleToolbarItems) ? null : items;
			if (ReferenceEquals(nextItems, subscribedContextToolbarItems))
				return;

			UpdateToolbarCollectionSubscription(subscribedContextToolbarItems, subscribe: false);
			subscribedContextToolbarItems = nextItems;
			UpdateToolbarCollectionSubscription(subscribedContextToolbarItems, subscribe: true);
		}

		private void UpdateToolbarCollectionSubscription(ObservableCollection<ToolbarItemDescriptor>? items, bool subscribe)
		{
			if (items is null)
				return;

			if (subscribe)
				items.CollectionChanged += PreviewItems_CollectionChanged;
			else
				items.CollectionChanged -= PreviewItems_CollectionChanged;

			UpdatePreviewItemSubscriptions(items, subscribe);
		}

		private void PreviewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			UpdatePreviewItemSubscriptions(e.OldItems, subscribe: false);
			UpdatePreviewItemSubscriptions(e.NewItems, subscribe: true);
			RebuildPreviewCommandBar();
		}

		private void UpdatePreviewItemSubscriptions(System.Collections.IEnumerable? items, bool subscribe)
		{
			if (items is null)
				return;

			foreach (var item in items.OfType<ToolbarItemDescriptor>())
			{
				if (subscribe)
					item.PropertyChanged += ToolbarItem_PropertyChanged;
				else
					item.PropertyChanged -= ToolbarItem_PropertyChanged;
			}
		}

		private void ToolbarItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(ToolbarItemDescriptor.ShowIcon)
				or nameof(ToolbarItemDescriptor.ShowLabel))
				RebuildPreviewCommandBar();
		}

		private void RebuildPreviewCommandBar()
		{
			PreviewCommandBar.PrimaryCommands.Clear();

			foreach (var item in GetPreviewItems())
			{
				if (CreatePreviewCommandElement(item) is { } command)
					PreviewCommandBar.PrimaryCommands.Add(command);
			}

			UpdatePreviewSeparatorVisibility();
		}

		private IEnumerable<ToolbarItemDescriptor> GetPreviewItems()
			=> ViewModel.IsSelectedContextAlwaysVisible ? ViewModel.AlwaysVisibleToolbarItems : ViewModel.ToolbarItems;

		private ICommandBarElement? CreatePreviewCommandElement(ToolbarItemDescriptor item)
		{
			if (item.IsSeparator)
				return new AppBarSeparator();

			var showIcon = item.ShowIcon && CanShowIcon(item);
			if (!showIcon && !item.ShowLabel)
				return null;

			var useStyledTemplate = item.IsGroup || (showIcon && !string.IsNullOrEmpty(item.Glyph.ThemedIconStyle));
			var button = new AppBarButton
			{
				Width = double.NaN,
				MinWidth = showIcon ? 40 : 0,
				Label = item.IsGroup ? item.DisplayName : item.ExtendedDisplayName,
				LabelPosition = item.ShowLabel ? CommandBarLabelPosition.Default : CommandBarLabelPosition.Collapsed,
				IsEnabled = true,
				IsHitTestVisible = false,
				Style = useStyledTemplate ? PreviewFlyoutButtonStyle : null,
				Flyout = item.IsGroup ? new MenuFlyout() : null,
			};

			if (showIcon)
				ToolbarButtonGlyphHelper.Apply(button, item.Glyph, useStyledTemplate);
			else
				button.Loaded += CollapseButtonIconViewbox;

			return button;
		}

		private static bool CanShowIcon(ToolbarItemDescriptor item)
			=> !string.IsNullOrEmpty(item.Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(item.Glyph.BaseGlyph);

		private static void CollapseButtonIconViewbox(object sender, RoutedEventArgs e)
		{
			var button = (AppBarButton)sender;
			button.Loaded -= CollapseButtonIconViewbox;
			// WinUI keeps the icon viewbox in the template even when we intentionally omit the icon.
			if (button.FindDescendant("ContentViewbox") is Viewbox viewbox)
				viewbox.Visibility = Visibility.Collapsed;
		}

		private void ToolbarItemsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
		{
			if (args.ItemContainer is not ListViewItem container)
				return;

			var isLastItem = args.ItemIndex == sender.Items.Count - 1;
			container.BorderThickness = isLastItem ? NoBorderThickness : ItemDividerThickness;
		}

		private void UpdatePreviewSeparatorVisibility()
		{
			bool previousWasSeparator = true;
			AppBarSeparator? lastSeparator = null;

			foreach (var command in PreviewCommandBar.PrimaryCommands)
			{
				if (command is AppBarSeparator separator)
				{
					separator.Visibility = previousWasSeparator ? Visibility.Collapsed : Visibility.Visible;
					if (!previousWasSeparator)
						lastSeparator = separator;

					previousWasSeparator = true;
					continue;
				}

				if (command is UIElement { Visibility: Visibility.Visible })
					previousWasSeparator = false;
			}

			if (lastSeparator is not null && previousWasSeparator)
				lastSeparator.Visibility = Visibility.Collapsed;
		}

		private void AvailableToolbarItemsTree_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs e)
		{
			if (GetDraggedAvailableItem(sender, e) is not { } draggedItem)
			{
				draggedAvailableItem = null;
				e.Cancel = true;
				e.Data.RequestedOperation = DataPackageOperation.None;
				return;
			}

			draggedAvailableItem = draggedItem;
			e.Data.RequestedOperation = DataPackageOperation.Copy;
		}

		private void AvailableToolbarItemsTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
			=> draggedAvailableItem = null;

		private void AddedToolbarItemsList_DragOver(object sender, DragEventArgs e)
			=> e.AcceptedOperation = DataPackageOperation.Copy;

		private void AddedToolbarItemsList_Drop(object sender, DragEventArgs e)
		{
			if (draggedAvailableItem is null || sender is not ListView listView)
				return;

			var insertIndex = ResolveDropInsertIndex(listView, e);
			ViewModel.InsertAvailableToolbarItemAt(draggedAvailableItem, insertIndex);
			draggedAvailableItem = null;
		}

		private static ToolbarItemDescriptor? GetDraggedAvailableItem(TreeView sender, TreeViewDragItemsStartingEventArgs e)
			=> (e.Items.OfType<ToolbarAvailableTreeItem>().FirstOrDefault() ?? sender.SelectedItem as ToolbarAvailableTreeItem)?.ToolbarItem;

		private static int ResolveDropInsertIndex(ListView listView, DragEventArgs e)
		{
			for (int index = 0; index < listView.Items.Count; index++)
			{
				if (listView.ContainerFromIndex(index) is not ListViewItem container)
					continue;

				var posInContainer = e.GetPosition(container);
				if (posInContainer.Y < container.ActualHeight / 2)
					return index;
			}

			return listView.Items.Count;
		}

		private void ViewModel_CloseRequested(object? sender, EventArgs e)
		{
			skipSessionRestoreOnUnload = true;
			hostWindow?.Close();
		}
	}
}
