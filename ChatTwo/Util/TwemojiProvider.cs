using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using SkiaSharp;
using Svg.Skia;

namespace ChatTwo.Util;

public static class TwemojiProvider
{
    private const string EmojiSeparator = ":";
    private static readonly Regex EmojiRegex = new($"{EmojiSeparator}[a-zA-Z0-9_+-]+{EmojiSeparator}", RegexOptions.Compiled);

    private static readonly string TwemojiBasePath = Path.Combine(Plugin.Interface.AssemblyLocation.DirectoryName!, @"images\twemoji\assets\");
    private static readonly string EmojibasePath = Path.Combine(Plugin.Interface.AssemblyLocation.DirectoryName!, @"images\twemoji\emojibase.raw.en.json");

    // Map of shortcodes to their corresponding Twemoji filenames
    private static Dictionary<string, string> TwemojiMap = new();

    // Set of all available Twemoji filenames (without extensions)
    private static HashSet<string> AvailableTwemojiFiles = new();

    // Map of filenames to their corresponding cached TwemojiEmote objects
    private static Dictionary<string, TwemojiEmote> TwemojiEmotes = new();

    // Load a list of available Twemoji and the Emojibase data from the filesystem
    static TwemojiProvider()
    {
        // Load the list of available Twemoji from the filesystem
        try
        {
            AvailableTwemojiFiles = Directory.GetFiles(TwemojiBasePath, "*.svg")
                .Select(v => Path.GetFileNameWithoutExtension(v) ?? "")
                .Where(v => !string.IsNullOrEmpty(v))
                .ToHashSet();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to load Twemoji files from {TwemojiBasePath}: {ex.Message}");
            return;
        }

        // Load the Emojibase data from the JSON file
        try
        {
            var emojibaseJson = File.ReadAllText(EmojibasePath);
            var emojibaseData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(emojibaseJson);
            if (emojibaseData == null)
            {
                throw new Exception("emojibaseData is null");
            }

            // Iterate through the emojibaseData and populate the emojibaseMap
            foreach (var (key, element) in emojibaseData)
            {
                // Check to see that we have a matching Twemoji file for this shortcode
                var lowerKey = key.ToLowerInvariant();
                if (!AvailableTwemojiFiles.Contains(lowerKey))
                {
                    continue;
                }

                // Parse the shortcodes from the JsonElement, which can be either a string or an array of strings
                List<string> shortcodes;
                switch (element.ValueKind)
                {
                    case JsonValueKind.Array:
                        shortcodes = new List<string>(element.GetArrayLength());
                        for (int i = 0; i < element.GetArrayLength(); i++)
                        {
                            var item = element[i];
                            if (item.ValueKind != JsonValueKind.String)
                            {
                                throw new Exception($"Unexpected JsonValueKind: {item.ValueKind} for key: {key} at index: {i}");
                            }
                            var shortcode = item.GetString();
                            if (string.IsNullOrEmpty(shortcode))
                            {
                                throw new Exception($"Unexpected empty string for key: {key} at index: {i}");
                            }
                            shortcodes.Add(shortcode);
                        }
                        break;
                    case JsonValueKind.String:
                        var shortcodeStr = element.GetString();
                        if (string.IsNullOrEmpty(shortcodeStr))
                        {
                            throw new Exception($"Unexpected empty string for key: {key}");
                        }
                        shortcodes = [shortcodeStr];
                        break;
                    default:
                        throw new Exception($"Unexpected JsonValueKind: {element.ValueKind} for key: {key}");
                }

                // For each shortcode, add it to the emojibaseMap
                foreach (var shortcode in shortcodes)
                {
                    TwemojiMap[shortcode] = lowerKey;
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to load Emojibase data from {EmojibasePath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve a shortcode to its corresponding Twemoji filename.
    /// </summary>
    /// <param name="shortcode">The emoji shortcode with or without ':' delimiters.</param>
    /// <returns>The corresponding Twemoji filename, or null.</returns>
    public static string? ResolveShortcode(string shortcode)
    {
        var trimmedShortcode = shortcode.Trim(EmojiSeparator.ToCharArray());
        return TwemojiMap.GetValueOrDefault(trimmedShortcode);
    }

    public static TwemojiEmote? GetTwemoji(string unicode)
    {
        // Look in the cache
        if (TwemojiEmotes.TryGetValue(unicode, out var emote))
        {
            return emote;
        }

        // Check that the file exists
        if (!AvailableTwemojiFiles.Contains(unicode))
        {
            return null;
        }

        // Otherwise, make a new one and add it to the cache
        var filePath = Path.Join(TwemojiBasePath, unicode + ".svg");
        var newEmote = new TwemojiEmote().Prepare(filePath);
        TwemojiEmotes[unicode] = newEmote;
        return newEmote;
    }

    public class TwemojiEmote : ImGuiDrawable
    {
        public byte[] RawData = [];
        private IDalamudTextureWrap? Texture;

        public TwemojiEmote Prepare(string filePath)
        {
            Task.Run(() => LoadAsync(filePath));
            return this;
        }

        public override void Draw(Vector2 size)
        {
            ImGui.Image(Texture!.Handle, size);
        }

        private async Task LoadAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length <= 0)
                    {
                        throw new Exception($"Twemoji file is empty: {filePath}");
                    }

                    // Convert the SVG data to PNG using Svg.Skia
                    var svgPicture = new SKSvg().Load(stream);
                    if (svgPicture is null)
                    {
                        throw new Exception($"Failed to load SVG from {filePath}");
                    }

                    using var bitmap = SKImage.FromPicture(svgPicture, svgPicture.CullRect.Size.ToSizeI());
                    using var bitmapData = bitmap.Encode(SKEncodedImageFormat.Png, 100);

                    RawData = bitmapData.ToArray();
                    Texture = await Plugin.TextureProvider.CreateFromImageAsync(RawData);
                    IsLoaded = true;
                    Failed = false;
                }
                else
                {
                    throw new FileNotFoundException($"Twemoji file not found: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Failed = true;
                Plugin.Log.Error(ex, $"Unable to load Twemoji from {filePath}");
            }
        }
    }
}