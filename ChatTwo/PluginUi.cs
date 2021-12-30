using System.Numerics;
using System.Runtime.InteropServices;
using ChatTwo.Ui;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;

namespace ChatTwo;

internal sealed class PluginUi : IDisposable {
    internal Plugin Plugin { get; }
    internal ImFontPtr? RegularFont { get; private set; }
    internal ImFontPtr? ItalicFont { get; private set; }
    internal Vector4 DefaultText { get; private set; }

    private List<IUiComponent> Components { get; }
    private ImFontConfigPtr _fontCfg;
    private ImFontConfigPtr _fontCfgMerge;
    private (GCHandle, int) _regularFont;
    private (GCHandle, int) _italicFont;
    private (GCHandle, int) _jpFont;
    private (GCHandle, int) _gameSymFont;

    private ImVector _ranges;

    private GCHandle _jpRange = GCHandle.Alloc(
        GlyphRangesJapanese.GlyphRanges,
        GCHandleType.Pinned
    );

    private GCHandle _symRange = GCHandle.Alloc(
        new ushort[] {
            0xE020,
            0xE0DB,
            0,
        },
        GCHandleType.Pinned
    );

    internal unsafe PluginUi(Plugin plugin) {
        this.Plugin = plugin;
        this.Components = new List<IUiComponent> {
            new Settings(this),
            new ChatLog(this),
        };

        this._fontCfg = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig()) {
            FontDataOwnedByAtlas = false,
        };

        this._fontCfgMerge = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig()) {
            FontDataOwnedByAtlas = false,
            MergeMode = true,
        };

        var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
        builder.AddRanges(ImGui.GetIO().Fonts.GetGlyphRangesDefault());
        builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─");
        builder.BuildRanges(out this._ranges);

        var regular = this.GetResource("ChatTwo.fonts.NotoSans-Regular.ttf");
        this._regularFont = (
            GCHandle.Alloc(regular, GCHandleType.Pinned),
            regular.Length
        );

        var italic = this.GetResource("ChatTwo.fonts.NotoSans-Italic.ttf");
        this._italicFont = (
            GCHandle.Alloc(italic, GCHandleType.Pinned),
            italic.Length
        );

        var jp = this.GetResource("ChatTwo.fonts.NotoSansJP-Regular.otf");
        this._jpFont = (
            GCHandle.Alloc(jp, GCHandleType.Pinned),
            jp.Length
        );

        var gameSym = File.ReadAllBytes(Path.Combine(this.Plugin.Interface.DalamudAssetDirectory.FullName, "UIRes", "gamesym.ttf"));
        this._gameSymFont = (
            GCHandle.Alloc(gameSym, GCHandleType.Pinned),
            gameSym.Length
        );

        this.Plugin.Interface.UiBuilder.BuildFonts += this.BuildFonts;
        this.Plugin.Interface.UiBuilder.Draw += this.Draw;

        this.Plugin.Interface.UiBuilder.RebuildFonts();
    }

    public void Dispose() {
        this.Plugin.Interface.UiBuilder.Draw -= this.Draw;
        this.Plugin.Interface.UiBuilder.BuildFonts -= this.BuildFonts;

        foreach (var component in this.Components) {
            component.Dispose();
        }

        this._regularFont.Item1.Free();
        this._italicFont.Item1.Free();
        this._gameSymFont.Item1.Free();
        this._symRange.Free();
        this._jpRange.Free();
        this._fontCfg.Destroy();
        this._fontCfgMerge.Destroy();
    }

    private void Draw() {
        this.DefaultText = ImGui.GetStyle().Colors[(int) ImGuiCol.Text];

        var font = this.RegularFont.HasValue;

        if (font) {
            ImGui.PushFont(this.RegularFont!.Value);
        }

        foreach (var component in this.Components) {
            try {
                component.Draw();
            } catch (Exception ex) {
                PluginLog.LogError(ex, "Error drawing component");
            }
        }

        if (font) {
            ImGui.PopFont();
        }
    }

    private byte[] GetResource(string name) {
        var stream = this.GetType().Assembly.GetManifestResourceStream(name)!;
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private void BuildFonts() {
        this.RegularFont = null;
        this.ItalicFont = null;

        // load regular noto sans and merge in jp + game icons
        this.RegularFont = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._regularFont.Item1.AddrOfPinnedObject(),
            this._regularFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfg,
            this._ranges.Data
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._jpFont.Item1.AddrOfPinnedObject(),
            this._jpFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfgMerge,
            this._jpRange.AddrOfPinnedObject()
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._gameSymFont.Item1.AddrOfPinnedObject(),
            this._gameSymFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfgMerge,
            this._symRange.AddrOfPinnedObject()
        );

        // load italic noto sans and merge in jp + game icons
        this.ItalicFont = ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._italicFont.Item1.AddrOfPinnedObject(),
            this._italicFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfg,
            this._ranges.Data
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._jpFont.Item1.AddrOfPinnedObject(),
            this._jpFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfgMerge,
            this._jpRange.AddrOfPinnedObject()
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._gameSymFont.Item1.AddrOfPinnedObject(),
            this._gameSymFont.Item2,
            this.Plugin.Config.FontSize,
            this._fontCfgMerge,
            this._symRange.AddrOfPinnedObject()
        );
    }
}
