// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;
using Files.App.Converters;
using static Files.App.Data.Items.ToolbarDefaultsTemplate;

namespace Files.App.Data.Items
{
	/// <summary>
	/// Represents a toolbar item that can be added or removed from the toolbar.
	/// </summary>
	public sealed partial class ToolbarItemDescriptor : ObservableObject
	{
		private const string GroupPrefix = "Group:";

		// CommandCode for separator items
		public const string SeparatorCommandCode = "Separator";

		// For commands: the CommandCodes name. For groups: "Group:GroupName".
		public string CommandCode { get; }
		public string ContextId { get; }
		public string DisplayName { get; }
		public string ExtendedDisplayName { get; }
		public string ExtendedDisplayNameWithGroupSuffix => IsGroup ? $"{ExtendedDisplayName} ..." : ExtendedDisplayName;
		public string Description { get; }
		public string CategoryPath { get; }
		public RichGlyph Glyph { get; }
		public bool IsGroup { get; }
		public bool IsSeparator { get; }

		[ObservableProperty]
		public partial bool ShowLabel { get; set; }

		[ObservableProperty]
		public partial bool ShowIcon { get; set; } = true;

		public bool HasIcon => !IsSeparator && (!string.IsNullOrEmpty(Glyph.ThemedIconStyle) || !string.IsNullOrEmpty(Glyph.BaseGlyph));
		public Style? ThemedIconStyle => Glyph.ToThemedIconStyle();

		public override string ToString()
			=> DisplayName;

		private ToolbarItemDescriptor(string commandCode, string contextId, string displayName, string extendedDisplayName, string description, string categoryPath, RichGlyph glyph, bool isGroup, bool isSeparator = false, bool showIcon = true, bool showLabel = false)
		{
			CommandCode = commandCode;
			ContextId = contextId;
			DisplayName = displayName;
			ExtendedDisplayName = extendedDisplayName;
			Description = description;
			CategoryPath = categoryPath;
			Glyph = glyph;
			IsGroup = isGroup;
			IsSeparator = isSeparator;
			ShowIcon = showIcon && HasIcon;
			ShowLabel = showLabel;
		}

		public static ToolbarItemDescriptor FromCommand(IRichCommand command, bool showIcon = true, bool showLabel = false, string? contextId = null)
			=> new(command.Code.ToString(), contextId ?? GetToolbarSectionId(command), command.Label, command.ExtendedLabel, command.Description, GetCategoryPath(command), command.Glyph, isGroup: false, showIcon: showIcon, showLabel: showLabel);

		public static ToolbarItemDescriptor FromGroup(CommandGroup group, bool showIcon = true, bool showLabel = false, string? contextId = null)
			=> new(CreateGroupIdentifier(group.Name), contextId ?? GetToolbarSectionId(group), group.DisplayName, group.DisplayName, group.Description, GetCategoryPath(group), group.Glyph, isGroup: true, showIcon: showIcon, showLabel: showLabel);

		public static ToolbarItemDescriptor CreateSeparator(string contextId)
			=> new(SeparatorCommandCode, contextId, Strings.Separator.GetLocalizedResource(), Strings.Separator.GetLocalizedResource(), Strings.Separator.GetLocalizedResource(), "", RichGlyph.None, isGroup: false, isSeparator: true, showIcon: false, showLabel: false);

		// Resolves a settings entry into a descriptor, or null if unrecognized.
		public static ToolbarItemDescriptor? Resolve(ToolbarItemSettingsEntry settingsEntry, ICommandManager commandManager, string contextId)
		{
			// Handle group
			if (!string.IsNullOrEmpty(settingsEntry.CommandGroup))
			{
				var group = commandManager.Groups.All.FirstOrDefault(g => g.Name == settingsEntry.CommandGroup);
				return group is not null
					? FromGroup(group, settingsEntry.ShowIcon, settingsEntry.ShowLabel, contextId)
					: null;
			}

			// Handle command or separator
			var commandCode = settingsEntry.CommandCode;
			if (string.IsNullOrEmpty(commandCode))
				return null;

			if (IsSeparatorCommandCode(commandCode))
				return CreateSeparator(contextId);

			return Enum.TryParse<CommandCodes>(commandCode, out var code) && code != CommandCodes.None
				? FromCommand(commandManager[code], settingsEntry.ShowIcon, settingsEntry.ShowLabel, contextId)
				: null;
		}

		public static IEnumerable<ToolbarItemDescriptor> GetAvailableItemsForContext(string contextId, ICommandManager commandManager)
		{
			yield return CreateSeparator(contextId);

			foreach (var group in commandManager.Groups.All)
			{
				if (!string.IsNullOrEmpty(group.Name)
					&& group.Commands.Any(code => code is not CommandCodes.None && commandManager[code].IsAccessibleGlobally))
					yield return FromGroup(group, contextId: contextId);
			}

			foreach (var command in commandManager)
			{
				if (command.Code is CommandCodes.None || !command.IsAccessibleGlobally)
					continue;

				yield return FromCommand(command, contextId: contextId);
			}
		}

		public static bool IsKnownContextId(string contextId)
			=> ToolbarDefaultsTemplate.IsKnownContextId(contextId);

		public static bool IsOtherContextsId(string contextId)
			=> string.Equals(contextId, OtherContextsContextId, StringComparison.Ordinal);

		public static bool IsSpecificContextId(string contextId)
			=> contextId is not AlwaysVisibleContextId
				and not OtherContextsContextId;

		public static string GetContextDisplayName(string contextId)
			=> contextId switch
			{
				AlwaysVisibleContextId => Strings.AlwaysVisible.GetLocalizedResource(),
				ArchiveFilesContextId => Strings.ArchiveFiles.GetLocalizedResource(),
				ScriptFilesContextId => Strings.ScriptFiles.GetLocalizedResource(),
				ImageFilesContextId => Strings.ImageFiles.GetLocalizedResource(),
				MediaFilesContextId => Strings.MediaFiles.GetLocalizedResource(),
				FontFilesContextId => Strings.FontFiles.GetLocalizedResource(),
				DriverFilesContextId => Strings.DriverFiles.GetLocalizedResource(),
				CertificateFilesContextId => Strings.CertificateFiles.GetLocalizedResource(),
				RecycleBinContextId => Strings.RecycleBin.GetLocalizedResource(),
				_ => Strings.OtherContexts.GetLocalizedResource(),
			};

		// Resolves a command code string to the toolbar section it belongs to.
		// Called by GetActiveToolbarContexts() to determine which toolbar sections are visible based on currently executable commands.
		public static string ResolveToolbarSectionId(string commandCode, ICommandManager commandManager)
		{
			if (IsGroupCommandCode(commandCode))
			{
				var group = commandManager.Groups.All.FirstOrDefault(candidate => candidate.Name == GetGroupName(commandCode));
				return group is not null ? GetToolbarSectionId(group) : OtherContextsContextId;
			}

			if (Enum.TryParse<CommandCodes>(commandCode, out var code) && code != CommandCodes.None)
				return GetToolbarSectionId(commandManager[code]);

			return OtherContextsContextId;
		}

		public ToolbarItemSettingsEntry ToSettingsEntry()
			=> IsGroup
				? new(commandGroup: GetGroupName(CommandCode), showIcon: HasIcon && ShowIcon, showLabel: ShowLabel)
				: new(commandCode: IsSeparator ? SeparatorCommandCode : CommandCode, showIcon: HasIcon && ShowIcon, showLabel: ShowLabel);

		public static bool IsGroupCommandCode(string commandCode) => commandCode.StartsWith(GroupPrefix, StringComparison.Ordinal);
		public static string CreateGroupIdentifier(string groupName) => GroupPrefix + groupName;
		public static bool IsSeparatorCommandCode(string commandCode) => string.Equals(commandCode, SeparatorCommandCode, StringComparison.Ordinal);
		public static string GetGroupName(string commandCode) => commandCode[GroupPrefix.Length..];

		// Returns which toolbar section this group belongs to — used both to place it
		// in the customization picker and to activate the right section at runtime.
		private static string GetToolbarSectionId(CommandGroup group)
			=> group.Name switch
			{
				nameof(CommandGroups.NewItem) => AlwaysVisibleContextId,
				nameof(CommandGroups.Extract) => ArchiveFilesContextId,
				nameof(CommandGroups.SetAs) => ImageFilesContextId,
				_ => AlwaysVisibleContextId,
			};

		private static string GetCategoryPath(IRichCommand command)
		{
			if (command is ActionCommand { Action: var action })
				return action.Category is not ActionCategory.Unspecified
					? ActionCategoryConverter.ToLocalizedCategoryPath(action.Category)
					: GetCategoryPathFromNamespace(action.GetType().Namespace)
						?? Strings.General.GetLocalizedResource();

			return Strings.General.GetLocalizedResource();
		}

		private static string GetCategoryPath(CommandGroup group)
			=> group.Name switch
			{
				nameof(CommandGroups.SetAs) => Strings.PropertySectionImage.GetLocalizedResource(),
				nameof(CommandGroups.Extract) => Strings.Archive.GetLocalizedResource(),
				nameof(CommandGroups.NewItem) => Strings.Create.GetLocalizedResource(),
				_ => Strings.Groups.GetLocalizedResource(),
			};

		// Derives a localized category path from a namespace, stripping known action prefixes
		// and humanizing the remaining segments (e.g. "Files.App.Actions.Content.ImageManipulation" → "Image / Manipulation").
		private static string? GetCategoryPathFromNamespace(string? namespaceName)
		{
			if (string.IsNullOrEmpty(namespaceName))
				return null;

			const string contentPrefix = "Files.App.Actions.Content.";
			if (namespaceName.StartsWith(contentPrefix, StringComparison.Ordinal))
				return HumanizeCategoryPath(namespaceName[contentPrefix.Length..]);

			const string actionsPrefix = "Files.App.Actions.";
			if (namespaceName.StartsWith(actionsPrefix, StringComparison.Ordinal))
				return HumanizeCategoryPath(namespaceName[actionsPrefix.Length..]);

			return null;
		}

		private static string HumanizeCategoryPath(string path)
			=> string.Join(" / ", path
				.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.Select(HumanizeCategorySegment));

		private static string HumanizeCategorySegment(string segment)
			=> segment switch
			{
				"FileSystem" => Strings.FileSystem.GetLocalizedResource(),
				"ImageManipulation" => Strings.PropertySectionImage.GetLocalizedResource(),
				"PreviewPopup" => Strings.Preview.GetLocalizedResource(),
				"OpenInNewPane" => Strings.OpenInNewPane.GetLocalizedResource(),
				"OpenInNewTab" => Strings.OpenInNewTab.GetLocalizedResource(),
				"OpenInNewWindow" => Strings.OpenInNewWindow.GetLocalizedResource(),
				_ => CamelCaseSplitRegex().Replace(segment, "$1 $2"),
			};

		[GeneratedRegex("([a-z])([A-Z])", RegexOptions.CultureInvariant)]
		private static partial Regex CamelCaseSplitRegex();

		// Returns which toolbar section this command belongs to — used both to place it
		// in the customization picker and to activate the right section at runtime.
		// A section becomes active when at least one of its commands is executable,
		// which implicitly reflects the current file selection without duplicating extension checks here.
		private static string GetToolbarSectionId(IRichCommand command)
			=> command.Code switch
			{
				CommandCodes.DecompressArchiveToChildFolder
					=> ArchiveFilesContextId,

				CommandCodes.RunWithPowershell
					or CommandCodes.EditInNotepad
					=> ScriptFilesContextId,

				CommandCodes.SetAsWallpaperBackground
					or CommandCodes.SetAsLockscreenBackground
					or CommandCodes.SetAsAppBackground
					or CommandCodes.SetAsSlideshowBackground
					or CommandCodes.RotateLeft
					or CommandCodes.RotateRight
					=> ImageFilesContextId,

				CommandCodes.PlayAll
					=> MediaFilesContextId,

				CommandCodes.InstallFont
					=> FontFilesContextId,

				CommandCodes.InstallInfDriver
					=> DriverFilesContextId,

				CommandCodes.InstallCertificate
					=> CertificateFilesContextId,

				CommandCodes.EmptyRecycleBin
					or CommandCodes.RestoreAllRecycleBin
					or CommandCodes.RestoreRecycleBin
					=> RecycleBinContextId,

				_ => OtherContextsContextId,
			};

	}

}
