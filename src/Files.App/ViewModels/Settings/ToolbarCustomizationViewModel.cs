// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Windows.Input;

namespace Files.App.ViewModels.Settings
{
	public sealed partial class ToolbarCustomizationViewModel : ObservableObject
	{
		private readonly IUserSettingsService UserSettingsService;
		private readonly ICommandManager CommandManager;

		private readonly Dictionary<string, ObservableCollection<ToolbarItemDescriptor>> toolbarItemsByContext = new(StringComparer.Ordinal);

		public ObservableCollection<KeyValuePair<string, string>> ToolbarContexts { get; } = [];

		/// <summary>
		/// The current items in the selected toolbar context, displayed in the settings UI.
		/// </summary>
		public ObservableCollection<ToolbarItemDescriptor> ToolbarItems
			=> GetToolbarItems(GetCurrentToolbarContextId());

		/// <summary>
		/// The always-visible items used in the preview pane.
		/// </summary>
		public ObservableCollection<ToolbarItemDescriptor> AlwaysVisibleToolbarItems
			=> GetToolbarItems(ToolbarDefaultsTemplate.AlwaysVisibleContextId);

		/// <summary>
		/// The available items organized as a hierarchical tree.
		/// </summary>
		public ObservableCollection<ToolbarAvailableTreeItem> AvailableToolbarTreeItems { get; } = [];

		public string SelectedToolbarContextName
			=> ToolbarItemDescriptor.GetContextDisplayName(GetCurrentToolbarContextId());

		public bool IsSelectedContextAlwaysVisible
			=> GetCurrentToolbarContextId() == ToolbarDefaultsTemplate.AlwaysVisibleContextId;

		[ObservableProperty]
		public partial string? SelectedToolbarContextId { get; set; }

		[ObservableProperty]
		public partial bool HasToolbarChanges { get; set; }

		private bool isReplacingToolbarItems;
		private bool isToolbarCustomizationSessionActive;
		private Dictionary<string, List<ToolbarItemSettingsEntry>>? toolbarCustomizationSessionSnapshot;

		public ICommand RemoveToolbarItemCommand { get; }
		public ICommand ResetToolbarCommand { get; }
		public ICommand SaveToolbarCommand { get; }
		public ICommand CancelToolbarCommand { get; }

		public event EventHandler? CloseRequested;

		public ToolbarCustomizationViewModel(IUserSettingsService userSettingsService, ICommandManager commandManager)
		{
			UserSettingsService = userSettingsService;
			CommandManager = commandManager;

			RemoveToolbarItemCommand = new RelayCommand<ToolbarItemDescriptor>(ExecuteRemoveToolbarItem);
			ResetToolbarCommand = new RelayCommand(ExecuteResetToolbar);
			SaveToolbarCommand = new RelayCommand(ExecuteSaveToolbar);
			CancelToolbarCommand = new RelayCommand(ExecuteCancelToolbar);

			InitializeToolbarContexts();
			SelectedToolbarContextId = ToolbarDefaultsTemplate.AlwaysVisibleContextId;
			LoadToolbarItems();
		}

		public void BeginToolbarCustomizationSession()
		{
			if (isToolbarCustomizationSessionActive)
				return;

			SelectedToolbarContextId = ToolbarDefaultsTemplate.AlwaysVisibleContextId;

			toolbarCustomizationSessionSnapshot = CreateCurrentToolbarItemsSnapshot();
			isToolbarCustomizationSessionActive = true;
			HasToolbarChanges = false;
		}

		public void SaveToolbarCustomizationSession()
		{
			if (!isToolbarCustomizationSessionActive)
				return;

			SaveToolbarItems();
			EndToolbarCustomizationSession();
		}

		public void CancelToolbarCustomizationSession()
		{
			if (!isToolbarCustomizationSessionActive)
				return;

			if (toolbarCustomizationSessionSnapshot is not null)
				ReplaceToolbarItems(toolbarCustomizationSessionSnapshot, saveChanges: true);

			EndToolbarCustomizationSession();
		}

		private void EndToolbarCustomizationSession()
		{
			isToolbarCustomizationSessionActive = false;
			toolbarCustomizationSessionSnapshot = null;
			HasToolbarChanges = false;
		}

		partial void OnSelectedToolbarContextIdChanged(string? value)
		{
			RefreshAvailableItems();
			OnPropertyChanged(nameof(ToolbarItems));
			OnPropertyChanged(nameof(SelectedToolbarContextName));
			OnPropertyChanged(nameof(IsSelectedContextAlwaysVisible));
		}

		private void InitializeToolbarCustomization()
		{
			InitializeToolbarContexts();
			SelectedToolbarContextId = ToolbarDefaultsTemplate.AlwaysVisibleContextId;
			LoadToolbarItems();
		}

		private void InitializeToolbarContexts()
		{
			var knownContexts = new HashSet<string>(ToolbarDefaultsTemplate.DefaultItemsByContext.Keys, StringComparer.Ordinal)
			{
				// Always include OtherContexts - it holds items shown for file types without a dedicated context.
				ToolbarDefaultsTemplate.OtherContextsContextId,
			};

			foreach (var group in CommandManager.Groups.All)
			{
				if (string.IsNullOrEmpty(group.Name))
					continue;

				knownContexts.Add(ToolbarItemDescriptor.ResolveToolbarSectionId(ToolbarItemDescriptor.CreateGroupIdentifier(group.Name), CommandManager));
			}

			foreach (var command in CommandManager)
			{
				if (command.Code is CommandCodes.None || !command.IsAccessibleGlobally)
					continue;

				knownContexts.Add(ToolbarItemDescriptor.ResolveToolbarSectionId(command.Code.ToString(), CommandManager));
			}

			ToolbarContexts.Clear();

			foreach (var contextId in ToolbarDefaultsTemplate.ContextOrder)
			{
				if (!knownContexts.Contains(contextId))
					continue;

				ToolbarContexts.Add(new(contextId, ToolbarItemDescriptor.GetContextDisplayName(contextId)));
				_ = GetToolbarItems(contextId);
			}
		}

		private void LoadToolbarItems()
		{
			var itemsByContext = UserSettingsService.AppearanceSettingsService.CustomToolbarItems is { Count: > 0 } savedContextItems
				? NormalizeToolbarItemsByContext(savedContextItems)
				: ToolbarDefaultsTemplate.CreateDefaultItemsByContext();

			ReplaceToolbarItems(itemsByContext, saveChanges: false);
		}

		private void ReplaceToolbarItems(IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext, bool saveChanges)
		{
			isReplacingToolbarItems = true;
			UnsubscribeItemPropertyChanged();

			foreach (var contextId in ToolbarContexts.Select(static context => context.Key))
				GetToolbarItems(contextId).Clear();

			foreach (var pair in itemsByContext)
			{
				var contextId = ToolbarDefaultsTemplate.NormalizeContextId(pair.Key);
				var items = GetToolbarItems(contextId);

				foreach (var settingsEntry in pair.Value)
				{
					if (ToolbarItemDescriptor.Resolve(settingsEntry, CommandManager, contextId) is { } descriptor)
						items.Add(descriptor);
				}
			}

			SubscribeItemPropertyChanged();
			isReplacingToolbarItems = false;

			RefreshAvailableItems();
			OnPropertyChanged(nameof(ToolbarItems));
			OnPropertyChanged(nameof(SelectedToolbarContextName));

			if (saveChanges)
				SaveToolbarItems();
		}

		private Dictionary<string, List<ToolbarItemSettingsEntry>> NormalizeToolbarItemsByContext(IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext)
		{
			var normalized = ToolbarContexts
				.Select(static context => context.Key)
				.ToDictionary(static contextId => contextId, static _ => new List<ToolbarItemSettingsEntry>(), StringComparer.Ordinal);

			foreach (var pair in itemsByContext)
			{
				var contextId = ToolbarDefaultsTemplate.NormalizeContextId(pair.Key);
				if (!normalized.TryGetValue(contextId, out var items))
					continue;

				foreach (var settingsEntry in pair.Value)
						items.Add(new(commandCode: settingsEntry.CommandCode, commandGroup: settingsEntry.CommandGroup, showIcon: settingsEntry.ShowIcon, showLabel: settingsEntry.ShowLabel));
			}

			return normalized;
		}

		private void ToolbarItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (isReplacingToolbarItems)
				return;

			if (e.OldItems is not null)
			{
				foreach (ToolbarItemDescriptor item in e.OldItems)
					item.PropertyChanged -= ToolbarItem_PropertyChanged;
			}

			if (e.NewItems is not null)
			{
				foreach (ToolbarItemDescriptor item in e.NewItems)
					item.PropertyChanged += ToolbarItem_PropertyChanged;
			}

			if (isToolbarCustomizationSessionActive)
				HasToolbarChanges = true;
			else
				SaveToolbarItems();
		}

		private void ToolbarItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(ToolbarItemDescriptor.ShowIcon)
				or nameof(ToolbarItemDescriptor.ShowLabel))
			{
				if (isToolbarCustomizationSessionActive)
					HasToolbarChanges = true;
				else
					SaveToolbarItems();
			}
		}

		private Dictionary<string, List<ToolbarItemSettingsEntry>> CreateCurrentToolbarItemsSnapshot()
			=> ToolbarContexts
				.Select(static context => context.Key)
				.ToDictionary(
					contextId => contextId,
					contextId => GetToolbarItems(contextId).Select(static item => item.ToSettingsEntry()).ToList(),
					StringComparer.Ordinal);

		private void SubscribeItemPropertyChanged()
		{
			foreach (var item in toolbarItemsByContext.Values.SelectMany(static items => items))
				item.PropertyChanged += ToolbarItem_PropertyChanged;
		}

		private void UnsubscribeItemPropertyChanged()
		{
			foreach (var item in toolbarItemsByContext.Values.SelectMany(static items => items))
				item.PropertyChanged -= ToolbarItem_PropertyChanged;
		}

		private void RefreshAvailableItems()
		{
			AvailableToolbarTreeItems.Clear();

			var availableItems = ToolbarItemDescriptor.GetAvailableItemsForContext(GetCurrentToolbarContextId(), CommandManager)
				.OrderBy(static item => item.CategoryPath, StringComparer.OrdinalIgnoreCase)
				.ThenBy(static item => item.ExtendedDisplayName, StringComparer.OrdinalIgnoreCase)
				.ToList();

			var categoryNodesByPath = new Dictionary<string, ToolbarAvailableTreeItem>(StringComparer.OrdinalIgnoreCase);

			foreach (var item in availableItems)
			{
				ToolbarAvailableTreeItem? parentNode = null;
				var pathSegments = item.CategoryPath.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

				if (pathSegments.Length > 0)
				{
					var currentPath = string.Empty;
					foreach (var segment in pathSegments)
					{
						currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

						if (!categoryNodesByPath.TryGetValue(currentPath, out var categoryNode))
						{
							categoryNode = new(segment);

							if (parentNode is null)
								AvailableToolbarTreeItems.Add(categoryNode);
							else
								parentNode.Children.Add(categoryNode);

							categoryNodesByPath[currentPath] = categoryNode;
						}

						parentNode = categoryNode;
					}
				}

				var treeItem = new ToolbarAvailableTreeItem(item.ExtendedDisplayNameWithGroupSuffix, item);
				if (parentNode is null)
					AvailableToolbarTreeItems.Add(treeItem);
				else
					parentNode.Children.Add(treeItem);
			}
		}

		private void SaveToolbarItems()
			=> UserSettingsService.AppearanceSettingsService.CustomToolbarItems = CreateCurrentToolbarItemsSnapshot();

		public void InsertAvailableToolbarItemAt(ToolbarItemDescriptor sourceItem, int index)
		{
			var contextId = GetCurrentToolbarContextId();
			var targetItems = GetToolbarItems(contextId);

			var clonedItem = ToolbarItemDescriptor.Resolve(sourceItem.ToSettingsEntry(), CommandManager, contextId);
			if (clonedItem is null)
				return;

			// When dragging new items to the added actions list, default to show label
			clonedItem.ShowLabel = true;

			var insertIndex = Math.Clamp(index, 0, targetItems.Count);
			targetItems.Insert(insertIndex, clonedItem);
		}

		private void ExecuteRemoveToolbarItem(ToolbarItemDescriptor? item)
		{
			if (item is not null)
				GetToolbarItems(item.ContextId).Remove(item);
		}

		private void ExecuteResetToolbar()
		{
			ReplaceToolbarItems(ToolbarDefaultsTemplate.CreateDefaultItemsByContext(), saveChanges: !isToolbarCustomizationSessionActive);

			if (isToolbarCustomizationSessionActive)
				HasToolbarChanges = true;
		}

		private void ExecuteSaveToolbar()
		{
			SaveToolbarCustomizationSession();
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}

		private void ExecuteCancelToolbar()
		{
			CancelToolbarCustomizationSession();
			CloseRequested?.Invoke(this, EventArgs.Empty);
		}

		private ObservableCollection<ToolbarItemDescriptor> GetToolbarItems(string contextId)
		{
			if (!toolbarItemsByContext.TryGetValue(contextId, out var items))
			{
				items = [];
				items.CollectionChanged += ToolbarItems_CollectionChanged;
				toolbarItemsByContext[contextId] = items;
			}

			return items;
		}

		private string GetCurrentToolbarContextId()
			=> ToolbarDefaultsTemplate.NormalizeContextId(
				SelectedToolbarContextId,
				nullFallbackContextId: ToolbarDefaultsTemplate.AlwaysVisibleContextId,
				unknownFallbackContextId: ToolbarDefaultsTemplate.AlwaysVisibleContextId);
	}

	public sealed class ToolbarAvailableTreeItem
	{
		public string DisplayName { get; }
		public ObservableCollection<ToolbarAvailableTreeItem> Children { get; } = [];
		public ToolbarItemDescriptor? ToolbarItem { get; }

		public ToolbarAvailableTreeItem(string displayName, ToolbarItemDescriptor? toolbarItem = null)
		{
			DisplayName = displayName;
			ToolbarItem = toolbarItem;
		}

		public override string ToString()
			=> DisplayName;
	}
}