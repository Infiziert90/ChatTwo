using System.Numerics;
using System.Runtime.InteropServices;
using ChatTwo.Ui;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;

namespace ChatTwo;

internal sealed class PluginUi : IDisposable {
    internal Plugin Plugin { get; }

    internal bool SettingsVisible;
    internal bool ScreenshotMode;
    internal string Salt { get; }

    internal ImFontPtr? RegularFont { get; private set; }
    internal ImFontPtr? ItalicFont { get; private set; }
    internal Vector4 DefaultText { get; private set; }

    internal Tab? CurrentTab {
        get {
            var i = this._chatLog.LastTab;
            if (i > -1 && i < this.Plugin.Config.Tabs.Count) {
                return this.Plugin.Config.Tabs[i];
            }

            return null;
        }
    }

    private List<IUiComponent> Components { get; }
    private ImFontConfigPtr _fontCfg;
    private ImFontConfigPtr _fontCfgMerge;
    private (GCHandle, int, float) _regularFont;
    private (GCHandle, int, float) _italicFont;
    private (GCHandle, int, float) _jpFont;
    private (GCHandle, int) _gameSymFont;

    private readonly ImVector _ranges;

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

    private readonly ChatLog _chatLog;

    internal unsafe PluginUi(Plugin plugin) {
        this.Plugin = plugin;
        this.Salt = new Random().Next().ToString();

        this._chatLog = new ChatLog(this);
        this.Components = new List<IUiComponent> {
            new Settings(this),
            this._chatLog,
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
        builder.AddText("←→↑↓《》■※☀★★☆♥♡ヅツッシ☀☁☂℃℉°♀♂♠♣♦♣♧®©™€$£♯♭♪✓√◎◆◇♦■□〇●△▽▼▲‹›≤≥<«“”─＼～Œœ");
        builder.BuildRanges(out this._ranges);

        this.SetUpUserFonts();

        var gameSym = File.ReadAllBytes(Path.Combine(this.Plugin.Interface.DalamudAssetDirectory.FullName, "UIRes", "gamesym.ttf"));
        this._gameSymFont = (
            GCHandle.Alloc(gameSym, GCHandleType.Pinned),
            gameSym.Length
        );

        var uiBuilder = this.Plugin.Interface.UiBuilder;
        uiBuilder.DisableAutomaticUiHide = true;
        uiBuilder.DisableCutsceneUiHide = true;
        uiBuilder.DisableGposeUiHide = true;
        uiBuilder.DisableUserUiHide = true;

        uiBuilder.BuildFonts += this.BuildFonts;
        uiBuilder.Draw += this.Draw;

        uiBuilder.RebuildFonts();
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

    private void SetUpUserFonts() {
        FontData? fontData = null;
        if (this.Plugin.Config.GlobalFont.StartsWith(Fonts.IncludedIndicator)) {
            var globalFont = Fonts.GlobalFonts.FirstOrDefault(font => font.Name == this.Plugin.Config.GlobalFont);
            if (globalFont != null) {
                var regular = new FaceData(this.GetResource(globalFont.ResourcePath), 1f);
                var italic = new FaceData(this.GetResource(globalFont.ResourcePathItalic), 1f);
                fontData = new FontData(regular, italic);
            }
        } else {
            fontData = Fonts.GetFont(this.Plugin.Config.GlobalFont, true);
        }

        if (fontData == null) {
            this.Plugin.Config.GlobalFont = Fonts.GlobalFonts[0].Name;
            this.Plugin.SaveConfig();

            var globalFont = Fonts.GlobalFonts[0];
            var regular = new FaceData(this.GetResource(globalFont.ResourcePath), 1f);
            var italic = new FaceData(this.GetResource(globalFont.ResourcePathItalic), 1f);
            fontData = new FontData(regular, italic);
        }

        if (this._regularFont.Item1.IsAllocated) {
            this._regularFont.Item1.Free();
        }

        if (this._italicFont.Item1.IsAllocated) {
            this._italicFont.Item1.Free();
        }

        this._regularFont = (
            GCHandle.Alloc(fontData.Regular.Data, GCHandleType.Pinned),
            fontData.Regular.Data.Length,
            fontData.Regular.Ratio
        );

        this._italicFont = (
            GCHandle.Alloc(fontData.Italic!.Data, GCHandleType.Pinned),
            fontData.Italic.Data.Length,
            fontData.Italic.Ratio
        );

        FontData? jpFontData = null;
        if (this.Plugin.Config.JapaneseFont.StartsWith(Fonts.IncludedIndicator)) {
            var jpFont = Fonts.JapaneseFonts.FirstOrDefault(item => item.Item1 == this.Plugin.Config.JapaneseFont);
            if (jpFont != default) {
                jpFontData = new FontData(
                    new FaceData(this.GetResource(jpFont.Item2), 1f),
                    null
                );
            }
        }
        // else {
        //     jpFontData = Fonts.GetFont(this.Plugin.Config.JapaneseFont, false, CharacterSet.SHIFTJIS_CHARSET);
        //     PluginLog.Log($"data.Regular.Length: {jpFontData?.Regular.Length}");
        // }

        if (jpFontData == null) {
            this.Plugin.Config.JapaneseFont = Fonts.JapaneseFonts[0].Item1;
            this.Plugin.SaveConfig();

            var jpFont = Fonts.JapaneseFonts[0];
            jpFontData = new FontData(
                new FaceData(this.GetResource(jpFont.Item2), 1f),
                null
            );
        }

        if (this._jpFont.Item1.IsAllocated) {
            this._jpFont.Item1.Free();
        }

        this._jpFont = (
            GCHandle.Alloc(jpFontData.Regular.Data, GCHandleType.Pinned),
            jpFontData.Regular.Data.Length,
            jpFontData.Regular.Ratio
        );
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

        this.SetUpUserFonts();

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
            this.Plugin.Config.JapaneseFontSize,
            this._fontCfgMerge,
            this._jpRange.AddrOfPinnedObject()
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._gameSymFont.Item1.AddrOfPinnedObject(),
            this._gameSymFont.Item2,
            this.Plugin.Config.SymbolsFontSize,
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
            this.Plugin.Config.JapaneseFontSize,
            this._fontCfgMerge,
            this._jpRange.AddrOfPinnedObject()
        );

        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(
            this._gameSymFont.Item1.AddrOfPinnedObject(),
            this._gameSymFont.Item2,
            this.Plugin.Config.SymbolsFontSize,
            this._fontCfgMerge,
            this._symRange.AddrOfPinnedObject()
        );
    }
}
