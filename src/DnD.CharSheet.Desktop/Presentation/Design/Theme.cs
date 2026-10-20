using System.Windows;
using System.Windows.Media;

namespace DnD.CharSheet.Desktop.Presentation.Design;

internal static class Theme
{
    public static void Apply(ResourceDictionary resources)
    {
        foreach (var name in new[] { "Canvas", "Surface", "SurfaceMuted", "Text", "TextSecondary", "Border", "Divider", "Accent", "AccentHover", "AccentPressed", "Selection", "DangerFill", "OnAccent", "OnSelection", "Focus", "Success", "Warning", "Danger", "Brass" })
        {
            var color = (Color)resources["Color." + name];
            if (SystemParameters.HighContrast)
                color = name switch
                {
                    "Canvas" or "Surface" or "SurfaceMuted" => SystemColors.WindowColor,
                    "Accent" or "AccentHover" or "AccentPressed" or "Selection" or "DangerFill" => SystemColors.HighlightColor,
                    "OnAccent" or "OnSelection" => SystemColors.HighlightTextColor,
                    _ => SystemColors.WindowTextColor
                };
            resources["Brush." + name] = new SolidColorBrush(color);
        }
    }
}

