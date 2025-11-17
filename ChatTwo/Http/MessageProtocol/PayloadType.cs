namespace ChatTwo.Http.MessageProtocol;

/// <summary>
/// Baseline: <see cref="Dalamud.Game.Text.SeStringHandling.PayloadType"/>
/// </summary>
public enum WebPayloadType
{
    // Dalamud
    Unknown,
    Player,
    Item,
    Status,
    RawText,
    UIForeground,
    UIGlow,
    MapLink,
    AutoTranslateText,
    EmphasisItalic,
    Icon,
    Quest,
    DalamudLink,
    NewLine,
    SeHyphen,
    PartyFinder,

    // Custom
    CustomPartyFinder = 0x50,
    CustomAchievement = 0x51,
    CustomUri = 0x52,
    CustomEmote = 0x53,
}