// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.App.Data.Items
{
	public static class ToolbarDefaultsTemplate
	{
		public const string AlwaysVisibleContextId = "AlwaysVisible";
		public const string ArchiveFilesContextId = "ArchiveFiles";
		public const string ScriptFilesContextId = "ScriptFiles";
		public const string ImageFilesContextId = "ImageFiles";
		public const string MediaFilesContextId = "MediaFiles";
		public const string FontFilesContextId = "FontFiles";
		public const string DriverFilesContextId = "DriverFiles";
		public const string CertificateFilesContextId = "CertificateFiles";
		public const string RecycleBinContextId = "RecycleBin";
		public const string OtherContextsContextId = "OtherContexts";

		public static readonly IReadOnlyDictionary<string, ToolbarItemSettingsEntry[]> DefaultItemsByContext =
			new Dictionary<string, ToolbarItemSettingsEntry[]>(StringComparer.Ordinal)
			{
				[AlwaysVisibleContextId] =
				[
					CreateGroup(nameof(CommandGroups.NewItem), showLabel: true),
					CreateSeparator(),
					CreateCommand(nameof(CommandCodes.CutItem)),
					CreateCommand(nameof(CommandCodes.CopyItem)),
					CreateCommand(nameof(CommandCodes.PasteItem)),
					CreateCommand(nameof(CommandCodes.Rename)),
					CreateCommand(nameof(CommandCodes.ShareItem)),
					CreateCommand(nameof(CommandCodes.DeleteItem)),
					CreateCommand(nameof(CommandCodes.OpenProperties)),
				],
				[ArchiveFilesContextId] =
				[
					CreateGroup(nameof(CommandGroups.Extract), showLabel: true),
				],
				[ScriptFilesContextId] =
				[
					CreateCommand(nameof(CommandCodes.RunWithPowershell), showLabel: true),
					CreateCommand(nameof(CommandCodes.EditInNotepad), showLabel: true),
				],
				[ImageFilesContextId] =
				[
					CreateGroup(nameof(CommandGroups.SetAs), showLabel: true),
					CreateCommand(nameof(CommandCodes.SetAsSlideshowBackground), showLabel: true),
					CreateCommand(nameof(CommandCodes.RotateLeft), showLabel: true),
					CreateCommand(nameof(CommandCodes.RotateRight), showLabel: true),
				],
				[MediaFilesContextId] =
				[
					CreateCommand(nameof(CommandCodes.PlayAll), showLabel: true),
				],
				[FontFilesContextId] =
				[
					CreateCommand(nameof(CommandCodes.InstallFont), showLabel: true),
				],
				[DriverFilesContextId] =
				[
					CreateCommand(nameof(CommandCodes.InstallInfDriver), showLabel: true),
				],
				[CertificateFilesContextId] =
				[
					CreateCommand(nameof(CommandCodes.InstallCertificate), showLabel: true),
				],
				[RecycleBinContextId] =
				[
					CreateCommand(nameof(CommandCodes.EmptyRecycleBin), showLabel: true),
					CreateCommand(nameof(CommandCodes.RestoreAllRecycleBin), showLabel: true),
					CreateCommand(nameof(CommandCodes.RestoreRecycleBin), showLabel: true),
				],
			};

		public static readonly string[] ContextOrder =
		[
			AlwaysVisibleContextId,
			.. DefaultItemsByContext.Keys.Where(static k => k != AlwaysVisibleContextId),
			OtherContextsContextId,
		];

		private static readonly HashSet<string> KnownContextSet = new(ContextOrder, StringComparer.Ordinal);

		public static Dictionary<string, List<ToolbarItemSettingsEntry>> CreateDefaultItemsByContext()
			=> ContextOrder
				.ToDictionary(
					static contextId => contextId,
					static contextId => new List<ToolbarItemSettingsEntry>(
						Array.ConvertAll(DefaultItemsByContext.GetValueOrDefault(contextId) ?? [], Clone)),
					StringComparer.Ordinal);

		public static Dictionary<string, List<string>> CreateDefaultIdentifiersByContext()
			=> ContextOrder
				.ToDictionary(
					static contextId => contextId,
					static contextId =>
					{
						var src = DefaultItemsByContext.GetValueOrDefault(contextId) ?? [];
						var ids = new List<string>(src.Length);
						foreach (var item in src)
							if ((item.CommandCode ?? item.CommandGroup) is { } id)
								ids.Add(id);
						return ids;
					},
					StringComparer.Ordinal);

		public static bool AreTemplatesEqual(
			IReadOnlyDictionary<string, List<string>>? left,
			IReadOnlyDictionary<string, List<string>> right)
			=> left is not null
				&& left.Count == right.Count
				&& right.All(kv => left.TryGetValue(kv.Key, out var leftIds) && leftIds.SequenceEqual(kv.Value, StringComparer.Ordinal));

		public static bool TryMergeNewDefaults(
			Dictionary<string, List<ToolbarItemSettingsEntry>> itemsByContext,
			IReadOnlyDictionary<string, List<string>> previousDefaultTemplate,
			IReadOnlyDictionary<string, List<ToolbarItemSettingsEntry>> currentDefaultTemplate)
		{
			var hasChanges = false;

			foreach (var contextId in ContextOrder)
			{
				var defaultItems = currentDefaultTemplate.GetValueOrDefault(contextId) ?? [];
				var previousDefaultCommandCodes = new HashSet<string>(
					previousDefaultTemplate.GetValueOrDefault(contextId) ?? [],
					StringComparer.Ordinal);

				if (!itemsByContext.TryGetValue(contextId, out var contextItems))
					itemsByContext[contextId] = contextItems = [];

				var existingCommandCodes = GetItemIdentifiers(contextItems);

				foreach (var defaultItem in defaultItems)
				{
					var identifier = defaultItem.CommandCode ?? defaultItem.CommandGroup;
					if (string.IsNullOrEmpty(identifier) || ToolbarItemDescriptor.IsSeparatorCommandCode(identifier))
						continue;

					if (previousDefaultCommandCodes.Contains(identifier))
						continue;

					if (!existingCommandCodes.Add(identifier))
						continue;

					contextItems.Add(Clone(defaultItem));
					hasChanges = true;
				}
			}

			return hasChanges;
		}

		public static bool IsKnownContextId(string contextId)
			=> KnownContextSet.Contains(contextId);

		public static string NormalizeContextId(string? contextId, string nullFallbackContextId = OtherContextsContextId, string unknownFallbackContextId = OtherContextsContextId)
			=> string.IsNullOrEmpty(contextId)
				? nullFallbackContextId
				: IsKnownContextId(contextId)
					? contextId
					: unknownFallbackContextId;

		private static ToolbarItemSettingsEntry CreateCommand(string commandCode, bool showIcon = true, bool showLabel = false)
			=> new(commandCode: commandCode, showIcon: showIcon, showLabel: showLabel);

		private static ToolbarItemSettingsEntry CreateGroup(string groupName, bool showIcon = true, bool showLabel = false)
			=> new(commandGroup: groupName, showIcon: showIcon, showLabel: showLabel);

		private static ToolbarItemSettingsEntry CreateSeparator()
			=> new(commandCode: ToolbarItemDescriptor.SeparatorCommandCode, showIcon: false, showLabel: false);

		private static HashSet<string> GetItemIdentifiers(IEnumerable<ToolbarItemSettingsEntry> items)
			=> items.Select(static item => item.CommandCode ?? item.CommandGroup ?? "").ToHashSet(StringComparer.Ordinal);

		private static ToolbarItemSettingsEntry Clone(ToolbarItemSettingsEntry entry)
			=> new(commandCode: entry.CommandCode, commandGroup: entry.CommandGroup, showIcon: entry.ShowIcon, showLabel: entry.ShowLabel);
	}
}
