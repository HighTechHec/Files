// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.ViewModels.Settings
{
	public sealed partial class ToolbarCustomizationViewModel : ObservableObject
	{
		private readonly IUserSettingsService UserSettingsService;
		private readonly ICommandManager CommandManager;

		private readonly Dictionary<string, ObservableCollection<ToolbarItemDescriptor>> toolbarItemsByContext = new(StringComparer.Ordinal);
		private IEnumerable<ToolbarItemDescriptor> AllToolbarItems => toolbarItemsByContext.Values.SelectMany(static items => items);
		private IEnumerable<string> ToolbarContextIds => ToolbarContexts.Select(static context => context.Key);
		private IEnumerable<ToolbarItemDescriptor> AvailableToolbarItems
			=> ToolbarItemDescriptor.GetAvailableItemsForContext(GetCurrentToolbarContextId(), CommandManager);

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

		public event EventHandler? CloseRequested;

		public ToolbarCustomizationViewModel(IUserSettingsService userSettingsService, ICommandManager commandManager)
		{
			UserSettingsService = userSettingsService;
			CommandManager = commandManager;

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
			=> CompleteToolbarCustomizationSession(saveChanges: true);

		public void CancelToolbarCustomizationSession()
			=> CompleteToolbarCustomizationSession(saveChanges: false);

		private void CompleteToolbarCustomizationSession(bool saveChanges)
		{
			if (!isToolbarCustomizationSessionActive)
				return;

			if (saveChanges)
				SaveToolbarItems();
			else if (toolbarCustomizationSessionSnapshot is not null)
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

		private void InitializeToolbarContexts()
		{
			ToolbarContexts.Clear();

			foreach (var contextId in GetKnownToolbarContextIds())
			{
				ToolbarContexts.Add(new(contextId, ToolbarItemDescriptor.GetContextDisplayName(contextId)));
				_ = GetToolbarItems(contextId);
			}
		}

		private IEnumerable<string> GetKnownToolbarContextIds()
		{
			var knownContexts = new HashSet<string>(ToolbarDefaultsTemplate.DefaultItemsByContext.Keys, StringComparer.Ordinal)
			{
				// Always include OtherContexts - it holds items shown for file types without a dedicated context.
				ToolbarDefaultsTemplate.OtherContextsContextId,
			};

			knownContexts.UnionWith(CommandManager.Groups.All
				.Where(group => !string.IsNullOrEmpty(group.Name))
				.Select(group => ToolbarItemDescriptor.ResolveToolbarSectionId(ToolbarItemDescriptor.CreateGroupIdentifier(group.Name), CommandManager)));

			knownContexts.UnionWith(CommandManager
				.Where(command => command.Code is not CommandCodes.None && command.IsAccessibleGlobally)
				.Select(command => ToolbarItemDescriptor.ResolveToolbarSectionId(command.Code.ToString(), CommandManager)));

			return ToolbarDefaultsTemplate.ContextOrder.Where(knownContexts.Contains);
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
			UpdateItemPropertySubscriptions(AllToolbarItems, subscribe: false);

			foreach (var contextId in ToolbarContextIds)
				GetToolbarItems(contextId).Clear();

			foreach (var pair in itemsByContext)
			{
				var contextId = ToolbarDefaultsTemplate.NormalizeContextId(pair.Key);
				var items = GetToolbarItems(contextId);

				foreach (var descriptor in pair.Value
					.Select(settingsEntry => ToolbarItemDescriptor.Resolve(settingsEntry, CommandManager, contextId))
					.OfType<ToolbarItemDescriptor>())
					items.Add(descriptor);
			}

			UpdateItemPropertySubscriptions(AllToolbarItems, subscribe: true);
			isReplacingToolbarItems = false;

			RefreshAvailableItems();
			OnPropertyChanged(nameof(ToolbarItems));
			OnPropertyChanged(nameof(SelectedToolbarContextName));

			if (saveChanges)
				SaveToolbarItems();
		}

		private Dictionary<string, List<ToolbarItemSettingsEntry>> NormalizeToolbarItemsByContext(IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext)
		{
			var normalized = ToolbarContextIds.ToDictionary(static contextId => contextId, static _ => new List<ToolbarItemSettingsEntry>(), StringComparer.Ordinal);

			foreach (var pair in itemsByContext)
			{
				var contextId = ToolbarDefaultsTemplate.NormalizeContextId(pair.Key);
				if (!normalized.TryGetValue(contextId, out var items))
					continue;

				items.AddRange(pair.Value.Select(static settingsEntry => new ToolbarItemSettingsEntry(
					commandCode: settingsEntry.CommandCode,
					commandGroup: settingsEntry.CommandGroup,
					showIcon: settingsEntry.ShowIcon,
					showLabel: settingsEntry.ShowLabel)));
			}

			return normalized;
		}

		private void ToolbarItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			if (isReplacingToolbarItems)
				return;

			UpdateItemPropertySubscriptions(e.OldItems, subscribe: false);
			UpdateItemPropertySubscriptions(e.NewItems, subscribe: true);

			HandleToolbarChange();
		}

		private void ToolbarItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ToolbarItemDescriptor.ShowIcon)
				and not nameof(ToolbarItemDescriptor.ShowLabel))
				return;

			HandleToolbarChange();
		}

		private Dictionary<string, List<ToolbarItemSettingsEntry>> CreateCurrentToolbarItemsSnapshot()
			=> ToolbarContextIds.ToDictionary(
					contextId => contextId,
					contextId => GetToolbarItems(contextId).Select(static item => item.ToSettingsEntry()).ToList(),
					StringComparer.Ordinal);

		private void HandleToolbarChange()
		{
			if (isToolbarCustomizationSessionActive)
			{
				HasToolbarChanges = true;
				return;
			}

			SaveToolbarItems();
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

		private void RefreshAvailableItems()
		{
			AvailableToolbarTreeItems.Clear();

			var categoryNodesByPath = new Dictionary<string, ToolbarAvailableTreeItem>(StringComparer.OrdinalIgnoreCase);

			foreach (var item in AvailableToolbarItems
				.OrderBy(static item => item.CategoryPath, StringComparer.OrdinalIgnoreCase)
				.ThenBy(static item => item.ExtendedDisplayName, StringComparer.OrdinalIgnoreCase))
			{
				AddTreeItem(GetParentNode(item.CategoryPath), new(item.ExtendedDisplayNameWithGroupSuffix, item));
			}

			ToolbarAvailableTreeItem? GetParentNode(string categoryPath)
			{
				ToolbarAvailableTreeItem? parentNode = null;
				var currentPath = string.Empty;

				foreach (var segment in categoryPath.Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
				{
					currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

					if (!categoryNodesByPath.TryGetValue(currentPath, out var categoryNode))
					{
						categoryNode = new(segment);
						AddTreeItem(parentNode, categoryNode);
						categoryNodesByPath[currentPath] = categoryNode;
					}

					parentNode = categoryNode;
				}

				return parentNode;
			}

			void AddTreeItem(ToolbarAvailableTreeItem? parentNode, ToolbarAvailableTreeItem treeItem)
			{
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

			if (ToolbarItemDescriptor.Resolve(sourceItem.ToSettingsEntry(), CommandManager, contextId) is not { } clonedItem)
				return;

			// When dragging new items to the added actions list, default to show label
			clonedItem.ShowLabel = true;

			var insertIndex = Math.Clamp(index, 0, targetItems.Count);
			targetItems.Insert(insertIndex, clonedItem);
		}

		[RelayCommand]
		private void RemoveToolbarItem(ToolbarItemDescriptor? item)
		{
			if (item is not null)
				GetToolbarItems(item.ContextId).Remove(item);
		}

		[RelayCommand]
		private void ResetToolbar()
		{
			ReplaceToolbarItems(ToolbarDefaultsTemplate.CreateDefaultItemsByContext(), saveChanges: false);
			HandleToolbarChange();
		}

		[RelayCommand]
		private void SaveToolbar()
		{
			SaveToolbarCustomizationSession();
			RequestClose();
		}

		[RelayCommand]
		private void CancelToolbar()
		{
			CancelToolbarCustomizationSession();
			RequestClose();
		}

		private void RequestClose()
			=> CloseRequested?.Invoke(this, EventArgs.Empty);

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