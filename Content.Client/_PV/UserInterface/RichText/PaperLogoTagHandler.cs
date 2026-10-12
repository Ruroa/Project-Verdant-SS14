using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._PV.UserInterface.RichText;

/// <summary>
/// Displays one of the approved organization logos in player-written paper markup.
/// </summary>
public sealed class PaperLogoTagHandler : IMarkupTagHandler
{
    private static readonly Dictionary<string, ResPath> Logos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nt"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_nanotrasen_logo.png"),
        ["nanotrasen"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_nanotrasen_logo.png"),
        ["cc"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_centcomm_logo.png"),
        ["centcomm"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_centcomm_logo.png"),
        ["vsc"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_VSC_logo.png"),
        ["vts"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_verdant_logo.png"),
        ["verdant"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_verdant_logo.png"),
        ["tsf"] = new("/Textures/_Starlight/Interface/Paper/paper_heading_tsf_logo.png"),
    };

    [Dependency] private IResourceCache _resourceCache = default!;

    public string Name => "logo";

    public PaperLogoTagHandler()
    {
        IoCManager.InjectDependencies(this);
    }

    /// <summary>
    /// Converts the convenient unquoted player syntax into a quoted markup parameter.
    /// Robust's markup parser otherwise interprets unquoted words as color values.
    /// </summary>
    public static string NormalizeMarkup(string text)
    {
        foreach (var logoName in Logos.Keys)
        {
            var normalizedTag = $"[logo=\"{logoName}\"]";
            text = text.Replace(
                $"[logo={logoName}]",
                normalizedTag,
                StringComparison.OrdinalIgnoreCase);
            text = text.Replace(
                $"[logo=\"{logoName}\"]",
                normalizedTag,
                StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context) { }
    public string TextBefore(MarkupNode node) => string.Empty;
    public string TextAfter(MarkupNode node) => string.Empty;

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        var logoName = node.Value.StringValue?.Trim();
        if (logoName == null || !Logos.TryGetValue(logoName, out var path))
        {
            control = null;
            return false;
        }

        var texture = _resourceCache.GetResource<TextureResource>(path);
        var size = new Vector2(texture.Texture.Width, texture.Texture.Height);

        control = new TextureRect
        {
            Texture = texture,
            MinSize = size,
            MaxSize = size,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = Control.MouseFilterMode.Ignore,
            Margin = new Thickness(2),
        };
        return true;
    }
}
