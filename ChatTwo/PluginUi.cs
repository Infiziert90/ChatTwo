﻿using System.Numerics;
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
    private (GCHandle, int) _regularFont;
    private (GCHandle, int) _italicFont;
    private (GCHandle, int) _jpFont;
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
                fontData = new FontData(this.GetResource(globalFont.ResourcePath), this.GetResource(globalFont.ResourcePathItalic));
            }
        } else {
            fontData = Fonts.GetFont(this.Plugin.Config.GlobalFont, true);
        }

        if (fontData == null) {
            PluginLog.Warning("global fallback");
            var globalFont = Fonts.GlobalFonts[0];
            fontData = new FontData(this.GetResource(globalFont.ResourcePath), this.GetResource(globalFont.ResourcePathItalic));
        }

        if (this._regularFont.Item1.IsAllocated) {
            this._regularFont.Item1.Free();
        }

        if (this._italicFont.Item1.IsAllocated) {
            this._italicFont.Item1.Free();
        }

        this._regularFont = (
            GCHandle.Alloc(fontData.Regular, GCHandleType.Pinned),
            fontData.Regular.Length
        );

        this._italicFont = (
            GCHandle.Alloc(fontData.Italic, GCHandleType.Pinned),
            fontData.Italic.Length
        );

        FontData? jpFontData = null;
        if (this.Plugin.Config.JapaneseFont.StartsWith(Fonts.IncludedIndicator)) {
            var jpFont = Fonts.JapaneseFonts.FirstOrDefault(item => item.Item1 == this.Plugin.Config.JapaneseFont);
            if (jpFont != default) {
                jpFontData = new FontData(this.GetResource(jpFont.Item2), Array.Empty<byte>());
            }
        }
        // else {
        //     jpFontData = Fonts.GetFont(this.Plugin.Config.JapaneseFont, false, CharacterSet.SHIFTJIS_CHARSET);
        //     PluginLog.Log($"data.Regular.Length: {jpFontData?.Regular.Length}");
        // }

        if (jpFontData == null) {
            PluginLog.Warning("jp fallback");
            var jpFont = Fonts.JapaneseFonts[0];
            jpFontData = new FontData(this.GetResource(jpFont.Item2), Array.Empty<byte>());
        }

        if (this._jpFont.Item1.IsAllocated) {
            this._jpFont.Item1.Free();
        }

        this._jpFont = (
            GCHandle.Alloc(jpFontData.Regular, GCHandleType.Pinned),
            jpFontData.Regular.Length
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
