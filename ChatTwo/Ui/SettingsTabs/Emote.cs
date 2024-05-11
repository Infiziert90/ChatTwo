using System.Numerics;
using ChatTwo.Resources;
using ChatTwo.Util;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace ChatTwo.Ui.SettingsTabs;

internal sealed class Emote : ISettingsTab
{
    private readonly Plugin Plugin;
    private Configuration Mutable { get; }

    public string Name => Language.Options_Emote_Tab + "###tabs-emote";

    private static SearchSelector.SelectorPopupOptions WordPopupOptions = null!;

    internal Emote(Plugin plugin, Configuration mutable)
    {
        Plugin = plugin;
        Mutable = mutable;

        WordPopupOptions = new SearchSelector.SelectorPopupOptions
        {
            FilteredSheet = EmoteCache.EmoteCodeArray.Where(w => !Mutable.BlockedEmotes.Contains(w))
        };
    }

    public void Draw(bool changed)
    {
        ImGui.PushTextWrapPos();

        ImGuiUtil.OptionCheckbox(ref Mutable.ShowEmotes, Language.Options_ShowEmotes_Name, Language.Options_ShowEmotes_Desc);
        ImGui.Spacing();

        ImGui.TextUnformatted(Language.Options_Emote_BlockedEmotes);
        ImGui.Spacing();

        var buttonWidth = ImGui.GetContentRegionAvail().X / 3;
        using (Plugin.FontManager.FontAwesome.Push())
            ImGui.Button(FontAwesomeIcon.Plus.ToIconString(), new Vector2(buttonWidth, 0));

        if (SearchSelector.SelectorPopup("WordAddPopup", out var newWord, WordPopupOptions))
            Mutable.BlockedEmotes.Add(newWord);

        using(var table = ImRaii.Table("##BlockedWords", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInner))
        {
            if (table)
            {
                ImGui.TableSetupColumn(Language.Options_Emote_EmoteTable);
                ImGui.TableSetupColumn("##Del", 0, 0.07f);

                ImGui.TableHeadersRow();

                var copiedList = Mutable.BlockedEmotes.ToArray();
                foreach (var word in copiedList)
                {
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(word);

                    ImGui.TableNextColumn();
                    if (ImGuiUtil.Button($"##{word}Del", FontAwesomeIcon.Trash, !ImGui.GetIO().KeyCtrl))
                        Mutable.BlockedEmotes.Remove(word);
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (EmoteCache.State is EmoteCache.LoadingState.Done)
            ImGui.TextColored(ImGuiColors.HealerGreen, "Ready");
        else
            ImGui.TextColored(ImGuiColors.DPSRed, "Not Ready");

        ImGui.TextUnformatted($"Emotes loaded: {EmoteCache.EmoteCodeArray.Length}");
        foreach (var word in EmoteCache.EmoteCodeArray)
            ImGui.TextUnformatted(word);

        ImGui.PopTextWrapPos();
    }
}
