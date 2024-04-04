using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ChatTwo.Util;

internal static class ChunkUtil {
    internal static IEnumerable<Chunk> ToChunks(SeString msg, ChunkSource source, ChatType? defaultColour) {
        var chunks = new List<Chunk>();

        var italic = false;
        var foreground = new Stack<uint>();
        var glow = new Stack<uint>();
        Payload? link = null;

        void Append(string text) {
            chunks.Add(new TextChunk(source, link, text) {
                FallbackColour = defaultColour,
                Foreground = foreground.Count > 0 ? foreground.Peek() : null,
                Glow = glow.Count > 0 ? glow.Peek() : null,
                Italic = italic,
            });
        }

        foreach (var payload in msg.Payloads) {
            switch (payload.Type) {
                case PayloadType.EmphasisItalic:
                    var newStatus = ((EmphasisItalicPayload) payload).IsEnabled;
                    italic = newStatus;
                    break;
                case PayloadType.UIForeground:
                    var foregroundPayload = (UIForegroundPayload) payload;
                    if (foregroundPayload.IsEnabled) {
                        foreground.Push(foregroundPayload.UIColor.UIForeground);
                    } else if (foreground.Count > 0) {
                        foreground.Pop();
                    }

                    break;
                case PayloadType.UIGlow:
                    var glowPayload = (UIGlowPayload) payload;
                    if (glowPayload.IsEnabled) {
                        glow.Push(glowPayload.UIColor.UIGlow);
                    } else if (glow.Count > 0) {
                        glow.Pop();
                    }

                    break;
                case PayloadType.AutoTranslateText:
                    chunks.Add(new IconChunk(source, link, BitmapFontIcon.AutoTranslateBegin));
                    var autoText = ((AutoTranslatePayload) payload).Text;
                    Append(autoText.Substring(2, autoText.Length - 4));
                    chunks.Add(new IconChunk(source, link, BitmapFontIcon.AutoTranslateEnd));
                    break;
                case PayloadType.Icon:
                    chunks.Add(new IconChunk(source, link, ((IconPayload) payload).Icon));
                    break;
                case PayloadType.MapLink:
                case PayloadType.Quest:
                case PayloadType.DalamudLink:
                case PayloadType.Status:
                case PayloadType.Item:
                case PayloadType.Player:
                    link = payload;
                    break;
                case PayloadType.PartyFinder:
                    link = payload;
                    break;
                case PayloadType.Unknown:
                    var rawPayload = (RawPayload) payload;
                    if (rawPayload.Data.Length > 1 && rawPayload.Data[1] == 0x13)
                    {
                        if (foreground.Count > 0) {
                            foreground.Pop();
                        }
                        else if (rawPayload.Data.Length > 6 && rawPayload.Data[2] == 0x05 && rawPayload.Data[3] == 0xF6)
                        {
                            var (r, g, b) = (rawPayload.Data[4], rawPayload.Data[5], rawPayload.Data[6]);
                            foreground.Push(ColourUtil.ComponentsToRgba(r, g, b));
                        }
                    }
                    else if (rawPayload.Data.Length > 1 && rawPayload.Data[1] == 0x14)
                    {
                        if (glow.Count > 0) {
                            glow.Pop();
                        }
                        else if (rawPayload.Data.Length > 6 && rawPayload.Data[2] == 0x05 && rawPayload.Data[3] == 0xF6)
                        {
                            var (r, g, b) = (rawPayload.Data[4], rawPayload.Data[5], rawPayload.Data[6]);
                            glow.Push(ColourUtil.ComponentsToRgba(r, g, b));
                        }
                    }
                    else if (rawPayload.Data.Length > 7 && rawPayload.Data[1] == 0x27 && rawPayload.Data[3] == 0x0A) {
                        // pf payload
                        var reader = new BinaryReader(new MemoryStream(rawPayload.Data[4..]));
                        var id = GetInteger(reader);
                        link = new PartyFinderPayload(id);
                    } else if (rawPayload.Data.Length > 5 && rawPayload.Data[1] == 0x27 && rawPayload.Data[3] == 0x06) {
                        // achievement payload
                        var reader = new BinaryReader(new MemoryStream(rawPayload.Data[4..]));
                        var id = GetInteger(reader);
                        link = new AchievementPayload(id);
                    } else if (Equals(rawPayload, RawPayload.LinkTerminator)) {
                        link = null;
                    }

                    break;
                default:
                    if (payload is ITextProvider textProvider) {
                        Append(textProvider.Text);
                    }

                    break;
            }
        }

        return chunks;
    }

    internal static readonly RawPayload PeriodicRecruitmentLink = new(new byte[] {
        0x02, 0x27, 0x07, 0x08, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03,
    });

    private static uint GetInteger(BinaryReader input) {
        var num1 = (uint) input.ReadByte();
        if (num1 < 208U) {
            return num1 - 1U;
        }

        var num2 = (uint) ((int) num1 + 1 & 15);
        var numArray = new byte[4];
        for (var index = 3; index >= 0; --index) {
            numArray[index] = (num2 & 1 << index) == 0L ? (byte) 0 : input.ReadByte();
        }

        return BitConverter.ToUInt32(numArray, 0);
    }
}
