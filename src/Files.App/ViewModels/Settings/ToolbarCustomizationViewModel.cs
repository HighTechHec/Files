// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.ViewModels.Settings
{
	public sealed partial class ToolbarCustomizationViewModel : ObservableObject
	{
		private readonly IUserSettingsService UserSettingsService;
		private readonly ICommandManager CommandManager;

		private readonly Dictionary<string, ObservableCollection<ToolbarItemDescriptor>> toolbarItemsByContext = new(StringComparer.Ordinal);
		private IEnumerable<ToolbarItemDescriptor> ToolbarItemsAcrossContexts => toolbarItemsByContext.Values.SelectMany(static items => items);
		private IEnumerable<string> ToolbarContextKeys => ToolbarContexts.Select(static context => context.Key);
		private IEnumerable<ToolbarItemDescriptor> CurrentAvailableToolbarItems
			=> ToolbarItemDescriptor.GetAvailableItemsForContext(ResolveCurrentToolbarContextId(), CommandManager);

		public ObservableCollection<KeyValuePair<string, string>> ToolbarContexts { get; } = [];

		/// <summary>
		/// The current items in the selected toolbar context, displayed in the settings UI.
		/// </summary>
		public ObservableCollection<ToolbarItemDescriptor> ToolbarItems
			=> GetToolbarItems(ResolveCurrentToolbarContextId());

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
			=> ToolbarItemDescriptor.GetContextDisplayName(ResolveCurrentToolbarContextId());

		public bool IsSelectedContextAlwaysVisible
			=> ResolveCurrentToolbarContextId() == ToolbarDefaultsTemplate.AlwaysVisibleContextId;

		[ObservableProperty]
		public partial string? SelectedToolbarContextId { get; set; }

		[ObservableProperty]
		public partial bool HasToolbarChanges { get; set; }

		private bool isApplyingToolbarItems;
		private bool isCustomizationSessionActive;

		public event EventHandler? CloseRequested;
		public event EventHandler? PreviewChanged;

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
			if (isCustomizationSessionActive)
				return;

			SelectedToolbarContextId = ToolbarDefaultsTemplate.AlwaysVisibleContextId;

			isCustomizationSessionActive = true;
			HasToolbarChanges = false;
		}

		public void SaveToolbarCustomizationSession()
			=> FinishCustomizationSession(persistChanges: true);

		public void CancelToolbarCustomizationSession()
			=> FinishCustomizationSession(persistChanges: false);

		private void FinishCustomizationSession(bool persistChanges)
		{
			if (!isCustomizationSessionActive)
				return;

			if (persistChanges)
				SaveToolbarItems();
			else
				LoadToolbarItems();

			ResetCustomizationSessionState();
		}

		private void ResetCustomizationSessionState()
		{
			isCustomizationSessionActive = false;
			HasToolbarChanges = false;
		}

		partial void OnSelectedToolbarContextIdChanged(string? value)
		{
			RefreshAvailableItems();
			OnPropertyChanged(nameof(ToolbarItems));
			OnPropertyChanged(nameof(SelectedToolbarContextName));
			OnPropertyChanged(nameof(IsSelectedContextAlwaysVisible));
			PreviewChanged?.Invoke(this, EventArgs.Empty);
		}

		private void InitializeToolbarContexts()
		{
			ToolbarContexts.Clear();

			foreach (var contextId in BuildKnownToolbarContextIds())
			{
				ToolbarContexts.Add(new(contextId, ToolbarItemDescriptor.GetContextDisplayName(contextId)));
				_ = GetToolbarItems(contextId);
			}
		}

		private IEnumerable<string> BuildKnownToolbarContextIds()
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
				? NormalizeToolbarSettingsByContext(savedContextItems)
				: ToolbarDefaultsTemplate.CreateDefaultItemsByContext();

			ApplyToolbarItems(itemsByContext, saveChanges: false);
		}

		private void ApplyToolbarItems(IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext, bool saveChanges)
		{
			isApplyingToolbarItems = true;
			UpdateToolbarItemSubscriptions(ToolbarItemsAcrossContexts, subscribe: false);

			try
			{
				foreach (var contextId in ToolbarContextKeys)
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
			}
			finally
			{
				UpdateToolbarItemSubscriptions(ToolbarItemsAcrossContexts, subscribe: true);
				isApplyingToolbarItems = false;
			}

			RefreshAvailableItems();
			OnPropertyChanged(nameof(ToolbarItems));
			OnPropertyChanged(nameof(SelectedToolbarContextName));

			if (saveChanges)
				SaveToolbarItems();
		}

		private Dictionary<string, List<ToolbarItemSettingsEntry>> NormalizeToolbarSettingsByContext(IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext)
		{
			var normalized = ToolbarContextKeys.ToDictionary(static contextId => contextId, static _ => new List<ToolbarItemSettingsEntry>(), StringComparer.Ordinal);

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
			if (isApplyingToolbarItems)
				return;

			UpdateToolbarItemSubscriptions(e.OldItems, subscribe: false);
			UpdateToolbarItemSubscriptions(e.NewItems, subscribe: true);

			PersistOrTrackToolbarChange();
			PreviewChanged?.Invoke(this, EventArgs.Empty);
		}

		private void ToolbarItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(ToolbarItemDescriptor.ShowIcon)
				and not nameof(ToolbarItemDescriptor.ShowLabel))
				return;

			PersistOrTrackToolbarChange();
			PreviewChanged?.Invoke(this, EventArgs.Empty);
		}

		private void PersistOrTrackToolbarChange()
		{
			if (isCustomizationSessionActive)
			{
				HasToolbarChanges = true;
				return;
			}

			SaveToolbarItems();
		}

		private void UpdateToolbarItemSubscriptions(System.Collections.IEnumerable? items, bool subscribe)
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

			// Build category nodes on demand so the tree only includes paths used by the current context.
			foreach (var item in CurrentAvailableToolbarItems
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
			=> UserSettingsService.AppearanceSettingsService.CustomToolbarItems =
				ToolbarContextKeys.ToDictionary(
					contextId => contextId,
					contextId => GetToolbarItems(contextId).Select(static item => item.ToSettingsEntry()).ToList(),
					StringComparer.Ordinal);

		public void InsertAvailableToolbarItemAt(ToolbarItemDescriptor sourceItem, int index)
		{
			var contextId = ResolveCurrentToolbarContextId();
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
			ApplyToolbarItems(ToolbarDefaultsTemplate.CreateDefaultItemsByContext(), saveChanges: false);
			PersistOrTrackToolbarChange();
			PreviewChanged?.Invoke(this, EventArgs.Empty);
		}

		[RelayCommand]
		private void SaveToolbar()
		{
			SaveToolbarCustomizationSession();
			RaiseCloseRequested();
		}

		[RelayCommand]
		private void CancelToolbar()
		{
			CancelToolbarCustomizationSession();
			RaiseCloseRequested();
		}

		private void RaiseCloseRequested()
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

		private string ResolveCurrentToolbarContextId()
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