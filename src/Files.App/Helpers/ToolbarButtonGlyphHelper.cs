// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.App.Controls;
using Files.App.Data.Commands;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Files.App.Helpers
{
	internal static class ToolbarButtonGlyphHelper
	{
		public static void Apply(AppBarButton button, RichGlyph glyph, bool useStyledTemplate)
		{
			if (!string.IsNullOrEmpty(glyph.ThemedIconStyle))
			{
				if (useStyledTemplate && glyph.ToThemedIcon() is FrameworkElement themedIcon)
					button.Content = themedIcon;

				if (ToOverflowIcon(glyph) is IconElement overflowIcon)
					button.Icon = overflowIcon;

				return;
			}

			if (glyph.ToFontIcon() is FontIcon fontIcon)
			{
				button.Icon = fontIcon;
				if (useStyledTemplate)
					button.Content = fontIcon;
			}
		}

		private static IconElement? ToOverflowIcon(RichGlyph glyph)
		{
			if (glyph.ToThemedIconStyle() is not Style style)
				return null;

			var pathData = GetStyleStringValue(style, ThemedIcon.OutlineIconDataProperty)
				?? GetStyleStringValue(style, ThemedIcon.FilledIconDataProperty);

			if (string.IsNullOrWhiteSpace(pathData))
				return null;

			return new PathIcon
			{
				Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData)
			};
		}

		private static string? GetStyleStringValue(Style style, DependencyProperty property)
		{
			for (var currentStyle = style; currentStyle is not null; currentStyle = currentStyle.BasedOn)
			{
				foreach (var setterBase in currentStyle.Setters)
				{
					if (setterBase is Setter { Property: var setterProperty, Value: string value }
						&& setterProperty == property
						&& !string.IsNullOrWhiteSpace(value))
					{
						return value;
					}
				}
			}

			return null;
		}
	}
}