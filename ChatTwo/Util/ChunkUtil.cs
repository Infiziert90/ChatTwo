using ChatTwo.Code;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text;
using Lumina.Text.Payloads;

using PayloadType = Dalamud.Game.Text.SeStringHandling.PayloadType;

namespace ChatTwo.Util;

internal static class ChunkUtil
{
    // internal static IEnumerable<Chunk> ToChunks(ReadOnlySeString msg, ChunkSource source, ChatType? defaultColour)
    // {
    //     var chunks = new List<Chunk>();
    //
    //     var italic = false;
    //     var foreground = new Stack<uint>();
    //     var glow = new Stack<uint>();
    //     Payload? link = null;
    //
    //     void Append(string text)
    //     {
    //         chunks.Add(new TextChunk(source, link, text)
    //         {
    //             FallbackColour = defaultColour,
    //             Foreground = foreground.Count > 0 ? foreground.Peek() : null,
    //             Glow = glow.Count > 0 ? glow.Peek() : null,
    //             Italic = italic,
    //         });
    //     }
    //
    //     foreach (var payload in msg)
    //     {
    //         if (payload.Type == ReadOnlySePayloadType.Text)
    //         {
    //             // We don't want to parse any null string
    //             var str = payload.ToString();
    //             var nulIndex = str.IndexOf('\0');
    //             if (nulIndex > 0)
    //                 str = str[..nulIndex];
    //             if (string.IsNullOrEmpty(str))
    //                 continue;
    //
    //             Append(str);
    //             continue;
    //         }
    //
    //         switch (payload.MacroCode)
    //         {
    //             case MacroCode.Italic:
    //                 var newStatus = payload.TryGetExpression(out var expression) && expression.TryGetUInt(out var value) && value == 1;
    //                 italic = newStatus;
    //                 break;
    //             case MacroCode.Color:
    //                 if (payload.TryGetExpression(out var eColor))
    //                 {
    //                     if (eColor.TryGetPlaceholderExpression(out var ph) && ph == (int)ExpressionType.StackColor)
    //                     {
    //                         if (foreground.Count > 0)
    //                             foreground.Pop();
    //                     }
    //                     else if (TryResolveUInt(eColor, out var eColorVal))
    //                     {
    //                         var color = ColourUtil.ArgbToRgba(eColorVal);
    //
    //                         if (color > 0)
    //                             foreground.Push(color);
    //                         else if (foreground.Count > 0) // Push the previous color as we don't want invisible text
    //                             foreground.Push(foreground.Peek());
    //                     }
    //                 }
    //                 break;
    //             case MacroCode.EdgeColor:
    //                 if (payload.TryGetExpression(out eColor))
    //                 {
    //                     if (eColor.TryGetPlaceholderExpression(out var ph) && ph == (int)ExpressionType.StackColor)
    //                     {
    //                         if (glow.Count > 0)
    //                             glow.Pop();
    //                     }
    //                     else if (TryResolveUInt(eColor, out var eColorVal))
    //                     {
    //                         glow.Push(ColourUtil.ArgbToRgba(eColorVal));
    //                     }
    //                 }
    //                 break;
    //             case MacroCode.ColorType:
    //                 if (!payload.TryGetExpression(out var eColorType) || !eColorType.TryGetUInt(out var eColorTypeVal))
    //                 {
    //                     if (foreground.Count > 0)
    //                         foreground.Pop();
    //                     break;
    //                 }
    //
    //                 if (eColorTypeVal == 0)
    //                 {
    //                     if (foreground.Count > 0)
    //                         foreground.Pop();
    //                 }
    //                 else if (Sheets.UIColorSheet.TryGetRow(eColorTypeVal, out var row))
    //                 {
    //                     foreground.Push(row.Dark);
    //                 }
    //                 break;
    //             case MacroCode.EdgeColorType:
    //                 if (!payload.TryGetExpression(out var eEdgeColor) || !eEdgeColor.TryGetUInt(out var eEdgeColorVal))
    //                 {
    //                     if (glow.Count > 0)
    //                         glow.Pop();
    //                     break;
    //                 }
    //
    //                 if (eEdgeColorVal == 0)
    //                 {
    //                     if (glow.Count > 0)
    //                         glow.Pop();
    //                 }
    //                 else if (Sheets.UIColorSheet.TryGetRow(eEdgeColorVal, out var row))
    //                 {
    //                     glow.Push(row.Dark);
    //                 }
    //                 break;
    //             case MacroCode.Fixed:
    //                 if (!payload.TryGetExpression(out var expr1, out var expr2))
    //                     break;
    //
    //                 if (expr1.TryGetUInt(out var group) && expr2.TryGetUInt(out var key))
    //                 {
    //                     chunks.Add(new IconChunk(source, null, BitmapFontIcon.AutoTranslateBegin));
    //                     using var rssb = new RentedSeStringBuilder();
    //                     var translatePayload = rssb.Builder
    //                         .BeginMacro(MacroCode.Fixed)
    //                         .AppendUIntExpression(group - 1)
    //                         .AppendUIntExpression(key)
    //                         .EndMacro()
    //                         .ToReadOnlySeString();
    //
    //                     Append(Plugin.Evaluator.Evaluate(translatePayload).ToString());
    //                     chunks.Add(new IconChunk(source, null, BitmapFontIcon.AutoTranslateEnd));
    //                 }
    //                 break;
    //             case MacroCode.Icon:
    //                 if (payload.TryGetExpression(out var eIcon) && TryResolveInt(eIcon, out var iconVal))
    //                     chunks.Add(new IconChunk(source, link, (BitmapFontIcon)iconVal));
    //                 break;
    //             case MacroCode.Link:
    //                 if (!payload.TryGetExpression(
    //                         out var linkTypeExpr1,
    //                         out var uintExpr2,
    //                         out var intExpr3,
    //                         out var intExpr4,
    //                         out var strExpr5))
    //                     break;
    //
    //                 if (!linkTypeExpr1.TryGetUInt(out var linkType))
    //                     break;
    //
    //                 switch ((LinkMacroPayloadType)linkType)
    //                 {
    //                     case LinkMacroPayloadType.Terminator:
    //                         link = null;
    //                         break;
    //                     case LinkMacroPayloadType.MapPosition:
    //                         if (!uintExpr2.TryGetUInt(out var ids))
    //                             break;
    //
    //                         if (!intExpr3.TryGetInt(out var rawX))
    //                             break;
    //
    //                         if (!intExpr4.TryGetInt(out var rawY))
    //                             break;
    //
    //                         var mapId = ids & 0xFF;
    //                         var territoryId = (ids >> 16) & 0xFF;
    //                         break;
    //                     case (LinkMacroPayloadType)Payload.EmbeddedInfoType.DalamudLink - 1:
    //                         if (!uintExpr2.TryGetUInt(out var commandId))
    //                             break;
    //
    //                         if (!intExpr3.TryGetInt(out var extra1))
    //                             break;
    //
    //                         if (!intExpr4.TryGetInt(out var extra2))
    //                             break;
    //
    //                         if (!strExpr5.TryGetString(out var extraStr))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.Quest:
    //                         if (!uintExpr2.TryGetUInt(out var questId))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.Status:
    //                         if (!uintExpr2.TryGetUInt(out var statusId))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.Item:
    //                         if (!uintExpr2.TryGetUInt(out var itemId))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.Character:
    //                         if (!uintExpr2.TryGetUInt(out var flags))
    //                             break;
    //
    //                         if (!intExpr3.TryGetUInt(out var worldId))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.PartyFinder:
    //                         if (!uintExpr2.TryGetUInt(out var listingId))
    //                             break;
    //
    //                         // intExpr3 is unused
    //
    //                         if (!intExpr4.TryGetUInt(out worldId))
    //                             break;
    //                         break;
    //                     case LinkMacroPayloadType.PartyFinderNotification:
    //                         // no expr used
    //                         break;
    //                     case LinkMacroPayloadType.Achievement:
    //                         if (!uintExpr2.TryGetUInt(out var achievementId))
    //                             break;
    //                         break;
    //                 }
    //                 break;
    //             case MacroCode.NonBreakingSpace:
    //                 Append(" ");
    //                 break;
    //             case PayloadType.Unknown:
    //                 var rawPayload = (RawPayload)payload;
    //                 else if (rawPayload.Data.Length > 1 && rawPayload.Data[1] == 0x14)
    //                 {
    //                     if (glow.Count > 0)
    //                     {
    //                         glow.Pop();
    //                     }
    //                     else if (rawPayload.Data.Length > 6 && rawPayload.Data[2] == 0x05 && rawPayload.Data[3] == 0xF6)
    //                     {
    //                         var (r, g, b) = (rawPayload.Data[4], rawPayload.Data[5], rawPayload.Data[6]);
    //                         glow.Push(ColourUtil.ComponentsToRgba(r, g, b));
    //                     }
    //                 }
    //                 break;
    //         }
    //     }
    //
    //     return chunks;
    // }

    internal static IEnumerable<Chunk> ToChunks(SeString msg, ChunkSource source, ChatType? defaultColour)
    {
        var chunks = new List<Chunk>();

        var italic = false;
        var foreground = new Stack<uint>();
        var glow = new Stack<uint>();
        Payload? link = null;

        void Append(string text)
        {
            chunks.Add(new TextChunk(source, link, text)
            {
                FallbackColour = defaultColour,
                Foreground = foreground.Count > 0 ? foreground.Peek() : null,
                Glow = glow.Count > 0 ? glow.Peek() : null,
                Italic = italic,
            });
        }

        foreach (var payload in msg.Payloads)
        {
            switch (payload.Type)
            {
                case PayloadType.EmphasisItalic:
                    var newStatus = ((EmphasisItalicPayload) payload).IsEnabled;
                    italic = newStatus;
                    break;
                case PayloadType.UIForeground:
                    var foregroundPayload = (UIForegroundPayload) payload;
                    if (foregroundPayload.IsEnabled)
                        foreground.Push(foregroundPayload.UIColor.Value.Dark);
                    else if (foreground.Count > 0)
                        foreground.Pop();
                    break;
                case PayloadType.UIGlow:
                    var glowPayload = (UIGlowPayload) payload;
                    if (glowPayload.IsEnabled)
                        glow.Push(glowPayload.UIColor.Value.Light);
                    else if (glow.Count > 0)
                        glow.Pop();
                    break;
                case PayloadType.AutoTranslateText:
                    chunks.Add(new IconChunk(source, payload, BitmapFontIcon.AutoTranslateBegin));
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
                    var colorPayload = ColorPayload.From(rawPayload.Data);
                    if (colorPayload != null)
                    {
                        if (colorPayload.Enabled)
                        {
                            if (colorPayload.Color > 0)
                                foreground.Push(colorPayload.Color);
                            else if (foreground.Count > 0) // Push the previous color as we don't want invisible text
                                foreground.Push(foreground.Peek());
                        }
                        else if (foreground.Count > 0)
                        {
                            foreground.Pop();
                        }
                    }
                    else if (rawPayload.Data.Length > 1 && rawPayload.Data[1] == 0x14)
                    {
                        if (glow.Count > 0)
                        {
                            glow.Pop();
                        }
                        else if (rawPayload.Data.Length > 6 && rawPayload.Data[2] == 0x05 && rawPayload.Data[3] == 0xF6)
                        {
                            var (r, g, b) = (rawPayload.Data[4], rawPayload.Data[5], rawPayload.Data[6]);
                            glow.Push(ColourUtil.ComponentsToRgba(r, g, b));
                        }
                    }
                    else if (rawPayload.Data.Length > 7 && rawPayload.Data[1] == 0x27 && rawPayload.Data[3] == 0x0A)
                    {
                        // pf payload
                        var reader = new BinaryReader(new MemoryStream(rawPayload.Data[4..]));
                        var id = GetInteger(reader);
                        link = new PartyFinderPayload(id);
                    }
                    else if (rawPayload.Data.Length > 5 && rawPayload.Data[1] == 0x27 && rawPayload.Data[3] == 0x06)
                    {
                        // achievement payload
                        var reader = new BinaryReader(new MemoryStream(rawPayload.Data[4..]));
                        var id = GetInteger(reader);
                        link = new AchievementPayload(id);
                    }
                    else if (rawPayload.Data is [_, (byte)MacroCode.NonBreakingSpace, _, _])
                    {
                        // NonBreakingSpace payload
                        Append(" ");
                    }
                    // NOTE: no URIPayload because it originates solely from
                    // new Message(). The game doesn't have a URI payload type.
                    else if (Equals(rawPayload, RawPayload.LinkTerminator))
                    {
                        link = null;
                    }
                    break;
                default:
                    if (payload is ITextProvider textProvider)
                    {
                        // We don't want to parse any null string
                        var str = textProvider.Text;
                        var nulIndex = str.IndexOf('\0');
                        if (nulIndex > 0)
                            str = str[..nulIndex];
                        if (string.IsNullOrEmpty(str))
                            break;

                        Append(str);
                    }
                    break;
            }
        }

        return chunks;
    }

    internal static string ToRawString(List<Chunk> chunks)
    {
        if (chunks.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var chunk in chunks)
            if (chunk is TextChunk text)
                builder.Append(text.Content);

        return builder.ToString();
    }

    internal static readonly RawPayload PeriodicRecruitmentLink = new([0x02, 0x27, 0x07, 0x08, 0x01, 0x01, 0x01, 0xFF, 0x01, 0x03]);

    private static uint GetInteger(BinaryReader input)
    {
        var num1 = (uint) input.ReadByte();
        if (num1 < 208U)
            return num1 - 1U;

        var num2 = (uint) ((int) num1 + 1 & 15);
        var numArray = new byte[4];
        for (var index = 3; index >= 0; --index)
            numArray[index] = (num2 & 1 << index) == 0L ? (byte) 0 : input.ReadByte();

        return BitConverter.ToUInt32(numArray, 0);
    }

    // private static bool TryResolveUInt(in ReadOnlySeExpressionSpan expression, out uint value)
    // {
    //     if (expression.TryGetUInt(out value))
    //         return true;
    //
    //     if (expression.TryGetParameterExpression(out var exprType, out var operand1))
    //     {
    //         if (!TryResolveUInt(operand1, out var paramIndex))
    //             return false;
    //
    //         if (paramIndex == 0)
    //             return false;
    //
    //         paramIndex--;
    //         if ((ExpressionType)exprType == ExpressionType.GlobalNumber)
    //         {
    //             value = (uint) GlobalParametersCache.GetValue((int)paramIndex);
    //             return true;
    //         }
    //         // return (ExpressionType)exprType switch
    //         // {
    //         //     // ExpressionType.LocalNumber => context.TryGetLNum((int)paramIndex, out value), // lnum
    //         //     ExpressionType.GlobalNumber => (uint) GlobalParametersCache.GetValue((int)paramIndex), // gnum
    //         //     _ => false, // gstr, lstr
    //         // };
    //     }
    //
    //     return false;
    // }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // private static bool TryResolveInt(in ReadOnlySeExpressionSpan expression, out int value)
    // {
    //     if (TryResolveUInt(expression, out var u32))
    //     {
    //         value = (int)u32;
    //         return true;
    //     }
    //
    //     value = 0;
    //     return false;
    // }
}
