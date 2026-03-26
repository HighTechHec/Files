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
		private Style PreviewButtonStyle => (Style)Resources["ToolBarAppBarButtonFlyoutStyle"];
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

			SetPreviewSubscriptions(subscribe: true);
			RebuildPreviewCommandBar();
		}

		private void ToolbarCustomizationPage_Unloaded(object sender, RoutedEventArgs e)
		{
			ViewModel.CloseRequested -= ViewModel_CloseRequested;
			SetPreviewSubscriptions(subscribe: false);

			if (!isSessionComplete)
				ViewModel.CancelToolbarCustomizationSession();
		}

		private void SetPreviewSubscriptions(bool subscribe)
		{
			if (subscribe)
				ViewModel.PropertyChanged += ViewModel_PropertyChanged;
			else
				ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

			SetToolbarSubscription(ViewModel.AlwaysVisibleToolbarItems, subscribe);
			SetObservedContextToolbarItems(subscribe ? ViewModel.ToolbarItems : null);
		}

		private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ToolbarCustomizationViewModel.SelectedToolbarContextId))
				return;

			SetObservedContextToolbarItems(ViewModel.ToolbarItems);
			RebuildPreviewCommandBar();
		}

		private void SetObservedContextToolbarItems(ObservableCollection<ToolbarItemDescriptor>? items)
		{
			var nextItems = ReferenceEquals(items, ViewModel.AlwaysVisibleToolbarItems) ? null : items;
			if (ReferenceEquals(nextItems, observedContextToolbarItems))
				return;

			SetToolbarSubscription(observedContextToolbarItems, subscribe: false);
			observedContextToolbarItems = nextItems;
			SetToolbarSubscription(observedContextToolbarItems, subscribe: true);
		}

		private void SetToolbarSubscription(ObservableCollection<ToolbarItemDescriptor>? items, bool subscribe)
		{
			if (items is null)
				return;

			if (subscribe)
				items.CollectionChanged += PreviewItems_CollectionChanged;
			else
				items.CollectionChanged -= PreviewItems_CollectionChanged;

			UpdateItemPropertySubscriptions(items, subscribe);
		}

		private void PreviewItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			UpdateItemPropertySubscriptions(e.OldItems, subscribe: false);
			UpdateItemPropertySubscriptions(e.NewItems, subscribe: true);
			RebuildPreviewCommandBar();
		}

		private void UpdateItemPropertySubscriptions(System.Collections.IEnumerable? items, bool subscribe)
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
				if (CreatePreviewCommand(item) is { } command)
					PreviewCommandBar.PrimaryCommands.Add(command);
			}

			UpdatePreviewSeparatorVisibility();
		}

		private IEnumerable<ToolbarItemDescriptor> GetPreviewItems()
			=> ViewModel.IsSelectedContextAlwaysVisible ? ViewModel.AlwaysVisibleToolbarItems : ViewModel.ToolbarItems;

		private ICommandBarElement? CreatePreviewCommand(ToolbarItemDescriptor item)
		{
			if (item.IsSeparator)
				return new AppBarSeparator();

			var showIcon = item.ShowIcon && HasVisibleIcon(item);
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
				Style = useStyledTemplate ? PreviewButtonStyle : null,
				Flyout = item.IsGroup ? new MenuFlyout() : null,
			};

			if (showIcon)
				ToolbarButtonGlyphHelper.Apply(button, item.Glyph, useStyledTemplate);
			else
				button.Loaded += CollapseButtonIconViewbox;

			return button;
		}

		private static bool HasVisibleIcon(ToolbarItemDescriptor item)
			=> !string.IsNullOrEmpty(item.Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(item.Glyph.BaseGlyph);

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
			if (GetDraggedAvailableToolbarItem(sender, e) is not { } draggedItem)
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

		private static ToolbarItemDescriptor? GetDraggedAvailableToolbarItem(TreeView sender, TreeViewDragItemsStartingEventArgs e)
			=> (e.Items.OfType<ToolbarAvailableTreeItem>().FirstOrDefault() ?? sender.SelectedItem as ToolbarAvailableTreeItem)?.ToolbarItem;

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
