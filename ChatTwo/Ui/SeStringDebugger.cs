using System.Numerics;
using System.Text;
using ChatTwo.Util;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using DalamudPartyFinderPayload = Dalamud.Game.Text.SeStringHandling.Payloads.PartyFinderPayload;

namespace ChatTwo.Ui;

public class SeStringDebugger : Window
{
    private readonly Plugin Plugin;

    public SeStringDebugger(Plugin plugin) : base($"SeString Debugger###chat2-sestringdebugger")
    {
        Plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(475, 600),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        #if DEBUG
        Plugin.Commands.Register("/chat2SeString", showInHelp: false).Execute += Toggle;
        #endif
    }

    public void Dispose()
    {
        #if DEBUG
        Plugin.Commands.Register("/chat2SeString", showInHelp: false).Execute -= Toggle;
        #endif
    }

    private void Toggle(string _, string __) => Toggle();

    public override void Draw()
    {
        if (Plugin.MessageManager.LastMessage.Sender == null)
        {
            ImGui.TextUnformatted("Nothing to show");
            return;
        }

        // TODO: Make SeString freely selectable through chat
        ImGui.TextUnformatted("Sender Content");
        ImGui.Spacing();
        if (Plugin.MessageManager.LastMessage.Sender != null)
            ProcessPayloads(Plugin.MessageManager.LastMessage.Sender.Payloads);
        else
            ImGui.TextUnformatted("Nothing to show");

        ImGui.TextUnformatted("Message Content");
        ImGui.Spacing();
        if (Plugin.MessageManager.LastMessage.Message != null)
            ProcessPayloads(Plugin.MessageManager.LastMessage.Message.Payloads);
        else
            ImGui.TextUnformatted("Nothing to show");
    }

    private void ProcessPayloads(List<Payload> payloads)
    {
        foreach (var payload in payloads)
        {
            switch (payload)
            {
                case UIForegroundPayload color:
                {
                    RenderMetadataDictionary("Link ForegroundColor", new Dictionary<string, string?>
                    {
                        { "Enabled?", color.IsEnabled.ToString() },
                        { "ColorKey", color.IsEnabled ? color.ColorKey.ToString() : "Color Ended" },
                    });
                    break;
                }
                case MapLinkPayload map:
                {
                    RenderMetadataDictionary("Link MapLinkPayload", new Dictionary<string, string?>
                    {
                        { "Map.RowId", map.Map.RowId.ToString() },
                        { "Map.PlaceName", map.Map.Value.PlaceName.Value.Name.ToString() },
                        { "Map.PlaceNameRegion", map.Map.Value.PlaceNameRegion.Value.Name.ToString() },
                        { "Map.PlaceNameSub", map.Map.Value.PlaceNameSub.Value.Name.ToString() },
                        { "TerritoryType.RowId", map.TerritoryType.RowId.ToString() },
                        { "RawX", map.RawX.ToString() },
                        { "RawY", map.RawY.ToString() },
                        { "XCoord", map.XCoord.ToString() },
                        { "YCoord", map.YCoord.ToString() },
                        { "CoordinateString", map.CoordinateString },
                        { "DataString", map.DataString },
                    });
                    break;
                }
                case QuestPayload quest:
                {
                    RenderMetadataDictionary("Link QuestPayload", new Dictionary<string, string?>
                    {
                        { "Quest.RowId", quest.Quest.RowId.ToString() },
                        { "Quest.Name", quest.Quest.Value.Name.ToString() },
                    });
                    break;
                }
                case DalamudLinkPayload link:
                {
                    RenderMetadataDictionary("Link DalamudLinkPayload", new Dictionary<string, string?>
                    {
                        { "CommandId", link.CommandId.ToString() },
                        { "Plugin", link.Plugin },
                    });
                    break;
                }
                case DalamudPartyFinderPayload pf:
                {
                    RenderMetadataDictionary("Link PartyFinderPayload", new Dictionary<string, string?>
                    {
                        { "ListingId", pf.ListingId.ToString() },
                        { "LinkType", EnumName(pf.LinkType) },
                    });
                    break;
                }
                case PlayerPayload player:
                {
                    RenderMetadataDictionary("Link PlayerPayload", new Dictionary<string, string?>
                    {
                        { "Displayed", player.DisplayedName },
                        { "Player Name", player.PlayerName },
                        { "World Name", player.World.Value.Name.ExtractText() },
                        { "Data", string.Join(" ", player.Encode().Select(b => b.ToString("X2"))) },
                    });
                    break;
                }
                case ItemPayload item:
                {
                    RenderMetadataDictionary("Link ItemPayload", new Dictionary<string, string?>
                    {
                        { "ItemId", item.ItemId.ToString() },
                        { "RawItemId", item.RawItemId.ToString() },
                        { "Kind", EnumName(item.Kind) },
                        { "IsHQ", item.IsHQ.ToString() },
                        { "Item.Name", item.Kind == ItemPayload.ItemKind.EventItem ? Sheets.EventItemSheet.GetRow(item.ItemId).Name.ExtractText() : Sheets.ItemSheet.GetRow(item.ItemId).Name.ExtractText() },
                    });
                    break;
                }
                case AutoTranslatePayload at:
                {
                    RenderMetadataDictionary("Link AutoTranslatePayload", new Dictionary<string, string?>
                    {
                        { "Text", at.Text },
                        { "Data", string.Join(" ", at.Encode().Select(b => b.ToString("X2"))) },
                    });
                    break;
                }
                case IconPayload icon:
                {
                    var found = IconUtil.GfdFileView.TryGetEntry((uint) icon.Icon, out var entry);
                    RenderMetadataDictionary("Link IconPayload", new Dictionary<string, string?>
                    {
                        { "Found", found.ToString() },
                        { "Icon ID", ((uint) icon.Icon).ToString() },
                    });
                    break;
                }
                case RawPayload raw:
                {
                    var colorPayload = ColorPayload.From(raw.Data);
                    if (colorPayload != null)
                    {
                        var push = colorPayload.Enabled && colorPayload.Color != 0;
                        // if (push) ImGui.PushStyleColor(ImGuiCol.Text, ColourUtil.RgbaToAbgr(colorPayload.U));
                        RenderMetadataDictionary("Link ColorPayload", new Dictionary<string, string?>
                        {
                            { "Unshifted", colorPayload.UnshiftedColor.ToString("X8") },
                            { "Color", colorPayload.Color.ToString("X8") },
                            { "Enabled?", colorPayload.Enabled.ToString() },
                        });
                        // if (push) ImGui.PopStyleColor();
                    }
                    else
                    {
                        RenderMetadataDictionary("Link RawPayload", new Dictionary<string, string?>
                        {
                            { "Data", string.Join(" ", raw.Data.Select(b => b.ToString("X2"))) },
                            { "Type", EnumName(raw.Type) },
                        });
                    }
                    break;
                }
                case StatusPayload status:
                {
                    RenderMetadataDictionary("Link StatusPayload", new Dictionary<string, string?>
                    {
                        { "Status.RowId", status.Status.RowId.ToString() },
                        { "Status.Name", status.Status.Value.Name.ExtractText() },
                        { "Status.Icon", status.Status.Value.Icon.ToString() }
                    });
                    break;
                }

                case Util.PartyFinderPayload pf:
                {
                    RenderMetadataDictionary("Link PartyFinderPayload", new Dictionary<string, string?>
                    {
                        { "Id", pf.Id.ToString() }
                    });
                    break;
                }
                case AchievementPayload achievement:
                {
                    RenderMetadataDictionary("Link AchievementPayload", new Dictionary<string, string?>
                    {
                        { "Id", achievement.Id.ToString() }
                    });
                    break;
                }
                default:
                    var payloadData = payload.Encode();

                    var initialByte = payloadData.First();
                    if (initialByte != 0x02)
                    {
                        RenderMetadataDictionary("Text Payload", new Dictionary<string, string?>
                        {
                            { "Content", Encoding.UTF8.GetString(payloadData) },
                        });
                    }
                    else
                    {
                        var unknown = new RawPayload(payloadData);
                        RenderMetadataDictionary("Link Unknown", new Dictionary<string, string?>
                        {
                            { "Unknown", string.Join(" ", unknown.Data.Select(b => b.ToString("X2"))) },
                        });
                    }
                    break;
            }
        }
    }

    private static string? EnumName<T>(T? value) where T : Enum
    {
        if (value == null)
        {
            return null;
        }
        var rawValue = Convert.ChangeType(value, value.GetTypeCode());
        return (Enum.GetName(value.GetType(), value) ?? "Unknown") + $" ({rawValue})";
    }

    private static void RenderMetadataDictionary(string name, Dictionary<string, string?> metadata)
    {
        var style = ImGui.GetStyle();

        ImGui.Text($"{name}:");
        ImGui.Indent(style.IndentSpacing);
        if (!ImGui.BeginTable($"##chat3-{name}", 2, 0))
        {
            ImGui.EndTable();
            ImGui.Unindent(style.IndentSpacing);
            return;
        }
        ImGui.TableSetupColumn($"##chat3-{name}-key", 0, 0.4f);
        ImGui.TableSetupColumn($"##chat3-{name}-value");
        for (var i = 0; i < metadata.Count; i++)
        {
            var (key, value) = metadata.ElementAt(i);
            ImGui.PushID(i);
            ImGui.TableNextColumn();
            ImGui.Text(key);
            ImGui.TableNextColumn();
            ImGuiTextVisibleWhitespace(value);
            ImGui.PopID();
        }
        ImGui.EndTable();
        ImGui.Unindent(style.IndentSpacing);
        ImGui.NewLine();
    }

    // ImGuiTextVisibleWhitespace replaces leading and trailing whitespace with
    // visible characters. The extra characters are rendered with a muted font.
    private static void ImGuiTextVisibleWhitespace(string? original, bool wrap = true)
    {
        if (string.IsNullOrEmpty(original))
        {
            var str = original == null ? "(null)" : "(empty)";
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.5f));
            ImGui.TextUnformatted(str);
            ImGui.PopStyleColor();
            return;
        }

        var text = original;
        var start = 0;
        var end = text.Length;

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0));

        void WriteText(string text)
        {
            if (wrap)
            {
                ImGui.TextWrapped(text);
            }
            else
            {
                ImGui.TextUnformatted(text);
            }
        }

        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }
        if (start > 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.5f));
            WriteText(new string('_', start));
            ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        WriteText(text[start..end]);
        if (end < text.Length)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 1, 0.5f));
            WriteText(new string('_', text.Length - end));
            ImGui.PopStyleColor();
        }

        ImGui.PopStyleVar();
    }
}
