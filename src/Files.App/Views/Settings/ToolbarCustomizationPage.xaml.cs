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
		private ToolbarItemDescriptor? draggedAvailableToolbarItem;
		private ObservableCollection<ToolbarItemDescriptor>? observedContextToolbarItems;
		private WindowEx? ownerWindow;
		private bool isSessionComplete;
		private static readonly Thickness AddedItemsDividerThickness = new(0, 0, 0, 1);
		private static readonly Thickness NoDividerThickness = new(0);
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
			ownerWindow = e.Parameter as WindowEx;
		}

		private void ToolbarCustomizationPage_Loaded(object sender, RoutedEventArgs e)
		{
			ViewModel.BeginToolbarCustomizationSession();
			isSessionComplete = false;
			ViewModel.CloseRequested += ViewModel_CloseRequested;

			AttachPreviewSubscriptions();
			RebuildPreviewCommandBar();
		}

		private void ToolbarCustomizationPage_Unloaded(object sender, RoutedEventArgs e)
		{
			ViewModel.CloseRequested -= ViewModel_CloseRequested;
			DetachPreviewSubscriptions();

			if (!isSessionComplete)
				ViewModel.CancelToolbarCustomizationSession();
		}

		private void AttachPreviewSubscriptions()
		{
			ViewModel.PropertyChanged += ViewModel_PropertyChanged;
			ViewModel.AlwaysVisibleToolbarItems.CollectionChanged += PreviewItems_CollectionChanged;
			SubscribeItemProperties(ViewModel.AlwaysVisibleToolbarItems);

			SubscribeContextToolbarItems(ViewModel.ToolbarItems);
			if (!ReferenceEquals(ViewModel.ToolbarItems, ViewModel.AlwaysVisibleToolbarItems))
				SubscribeItemProperties(ViewModel.ToolbarItems);
		}

		private void DetachPreviewSubscriptions()
		{
			ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
			ViewModel.AlwaysVisibleToolbarItems.CollectionChanged -= PreviewItems_CollectionChanged;
			UnsubscribeItemProperties(ViewModel.AlwaysVisibleToolbarItems);
			UnsubscribeContextToolbarItems();
		}

		private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(ToolbarCustomizationViewModel.SelectedToolbarContextId))
			{
				UnsubscribeContextToolbarItems();
				SubscribeContextToolbarItems(ViewModel.ToolbarItems);

				if (!ReferenceEquals(ViewModel.ToolbarItems, ViewModel.AlwaysVisibleToolbarItems))
					SubscribeItemProperties(ViewModel.ToolbarItems);

				RebuildPreviewCommandBar();
			}
		}

		private void SubscribeContextToolbarItems(ObservableCollection<ToolbarItemDescriptor> items)
		{
			observedContextToolbarItems = items;
			observedContextToolbarItems.CollectionChanged += PreviewItems_CollectionChanged;
		}

		private void UnsubscribeContextToolbarItems()
		{
			if (observedContextToolbarItems is null)
				return;

			observedContextToolbarItems.CollectionChanged -= PreviewItems_CollectionChanged;
			if (!ReferenceEquals(observedContextToolbarItems, ViewModel.AlwaysVisibleToolbarItems))
				UnsubscribeItemProperties(observedContextToolbarItems);

			observedContextToolbarItems = null;
		}

		private void PreviewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems is not null)
			{
				foreach (var item in e.OldItems.OfType<ToolbarItemDescriptor>())
					item.PropertyChanged -= ToolbarItem_PropertyChanged;
			}

			if (e.NewItems is not null)
			{
				foreach (var item in e.NewItems.OfType<ToolbarItemDescriptor>())
					item.PropertyChanged += ToolbarItem_PropertyChanged;
			}

			RebuildPreviewCommandBar();
		}

		private void SubscribeItemProperties(IEnumerable<ToolbarItemDescriptor> items)
		{
			foreach (var item in items)
				item.PropertyChanged += ToolbarItem_PropertyChanged;
		}

		private void UnsubscribeItemProperties(IEnumerable<ToolbarItemDescriptor> items)
		{
			foreach (var item in items)
				item.PropertyChanged -= ToolbarItem_PropertyChanged;
		}

		private void ToolbarItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(ToolbarItemDescriptor.ShowIcon)
				or nameof(ToolbarItemDescriptor.ShowLabel))
				RebuildPreviewCommandBar();
		}

		private void RebuildPreviewCommandBar()
		{
			if (PreviewCommandBar is null)
				return;

			PreviewCommandBar.PrimaryCommands.Clear();

			if (ViewModel.IsSelectedContextAlwaysVisible)
				AddPreviewItems(ViewModel.AlwaysVisibleToolbarItems, false);
			else
				AddPreviewItems(ViewModel.ToolbarItems, false);

			UpdatePreviewSeparatorVisibility();
		}

		private void AddPreviewItems(IEnumerable<ToolbarItemDescriptor> items, bool dimmed)
		{
			foreach (var item in items)
			{
				if (item.IsSeparator)
				{
					PreviewCommandBar.PrimaryCommands.Add(new AppBarSeparator { Opacity = dimmed ? 0.5 : 1.0 });
					continue;
				}

				var showIcon = item.ShowIcon && (!string.IsNullOrEmpty(item.Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(item.Glyph.BaseGlyph));

				if (!showIcon && !item.ShowLabel)
					continue;

				var button = new AppBarButton
				{
					Width = double.NaN,
					MinWidth = showIcon ? 40 : 0,
					Label = item.IsGroup ? item.DisplayName : item.ExtendedDisplayName,
					LabelPosition = item.ShowLabel ? CommandBarLabelPosition.Default : CommandBarLabelPosition.Collapsed,
					IsEnabled = true,
					IsHitTestVisible = false,
					Opacity = dimmed ? 0.5 : 1.0,
				};

				var useStyledTemplate = item.IsGroup || (showIcon && !string.IsNullOrEmpty(item.Glyph.ThemedIconStyle));

				if (useStyledTemplate)
					button.Style = (Style)Resources["ToolBarAppBarButtonFlyoutStyle"];

				if (item.IsGroup)
				{
					button.Flyout = new MenuFlyout();
				}

				if (showIcon)
					ToolbarButtonGlyphHelper.Apply(button, item.Glyph, useStyledTemplate);
				else
				{
					button.Loaded += CollapseButtonIconViewbox;
				}

				PreviewCommandBar.PrimaryCommands.Add(button);
			}
		}

		private static void CollapseButtonIconViewbox(object sender, RoutedEventArgs e)
		{
			var button = (AppBarButton)sender;
			button.Loaded -= CollapseButtonIconViewbox;
			if (button.FindDescendant("ContentViewbox") is Viewbox viewbox)
				viewbox.Visibility = Visibility.Collapsed;
		}

		private void ToolbarItemsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
		{
			if (args.ItemContainer is not ListViewItem container)
				return;

			var isLastItem = args.ItemIndex == sender.Items.Count - 1;
			container.BorderThickness = isLastItem ? NoDividerThickness : AddedItemsDividerThickness;
		}

		private void UpdatePreviewSeparatorVisibility()
		{
			if (PreviewCommandBar is null)
				return;

			bool previousWasSeparator = true;
			AppBarSeparator? lastSeparator = null;

			for (int index = 0; index < PreviewCommandBar.PrimaryCommands.Count; index++)
			{
				if (PreviewCommandBar.PrimaryCommands[index] is AppBarSeparator separator)
				{
					separator.Visibility = previousWasSeparator ? Visibility.Collapsed : Visibility.Visible;
					if (!previousWasSeparator)
						lastSeparator = separator;

					previousWasSeparator = true;
				}
				else if (PreviewCommandBar.PrimaryCommands[index] is UIElement { Visibility: Visibility.Visible })
				{
					previousWasSeparator = false;
				}
			}

			if (lastSeparator is not null && previousWasSeparator)
				lastSeparator.Visibility = Visibility.Collapsed;
		}

		private void AvailableToolbarItemsTree_DragItemsStarting(TreeView sender, TreeViewDragItemsStartingEventArgs e)
		{
			var draggedTreeItem = e.Items.OfType<ToolbarAvailableTreeItem>().FirstOrDefault() ?? sender.SelectedItem as ToolbarAvailableTreeItem;
			if (draggedTreeItem?.ToolbarItem is not ToolbarItemDescriptor draggedItem)
			{
				draggedAvailableToolbarItem = null;
				e.Cancel = true;
				e.Data.RequestedOperation = DataPackageOperation.None;
				return;
			}

			draggedAvailableToolbarItem = draggedItem;
			e.Data.RequestedOperation = DataPackageOperation.Copy;
		}

		private void AvailableToolbarItemsTree_DragItemsCompleted(TreeView sender, TreeViewDragItemsCompletedEventArgs args)
			=> draggedAvailableToolbarItem = null;

		private void AddedToolbarItemsList_DragOver(object sender, DragEventArgs e)
			=> e.AcceptedOperation = DataPackageOperation.Copy;

		private void AddedToolbarItemsList_Drop(object sender, DragEventArgs e)
		{
			if (draggedAvailableToolbarItem is null || sender is not ListView listView)
				return;

			var insertIndex = GetDropInsertIndex(listView, e);
			ViewModel.InsertAvailableToolbarItemAt(draggedAvailableToolbarItem, insertIndex);
			draggedAvailableToolbarItem = null;
		}

		private static int GetDropInsertIndex(ListView listView, DragEventArgs e)
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
			isSessionComplete = true;
			ownerWindow?.Close();
		}
	}
}
