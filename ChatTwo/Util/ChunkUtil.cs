using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace ChatTwo.Util;

internal static class ChunkUtil {
    internal static IEnumerable<Chunk> ToChunks(SeString msg, ChatType? defaultColour) {
        var chunks = new List<Chunk>();

        var italic = false;
        var foreground = new Stack<uint>();
        var glow = new Stack<uint>();
        Payload? link = null;

        void Append(string text) {
            chunks.Add(new TextChunk(msg, link, text) {
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
                    chunks.Add(new IconChunk(msg, link, BitmapFontIcon.AutoTranslateBegin));
                    var autoText = ((AutoTranslatePayload) payload).Text;
                    Append(autoText.Substring(2, autoText.Length - 4));
                    chunks.Add(new IconChunk(msg, link, BitmapFontIcon.AutoTranslateEnd));
                    break;
                case PayloadType.Icon:
                    chunks.Add(new IconChunk(msg, link, ((IconPayload) payload).Icon));
                    break;
                case PayloadType.MapLink:
                case PayloadType.Quest:
                case PayloadType.DalamudLink:
                case PayloadType.Status:
                case PayloadType.Item:
                case PayloadType.Player:
                    link = payload;
                    break;
                case PayloadType.Unknown:
                    var rawPayload = (RawPayload) payload;
                    if (rawPayload.Data[1] == 0x13) {
                        foreground.Pop();
                        glow.Pop();
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
}
