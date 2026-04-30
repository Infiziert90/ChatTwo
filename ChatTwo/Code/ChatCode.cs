using Dalamud.Game.Text;

namespace ChatTwo.Code;

public class ChatCode
{
    public ChatType Type { get; }
    public XivChatRelationKind Source { get; }
    public XivChatRelationKind Target { get; }

    public ChatCode(XivChatType type, XivChatRelationKind source, XivChatRelationKind target)
    {
        Type = (ChatType)type;
        Source = source;
        Target = target;
    }

    public ChatCode(byte type, byte source, byte target)
        : this((XivChatType)type, (XivChatRelationKind)source, (XivChatRelationKind)target) {}

    public bool IsBattle()
    {
        switch (Type)
        {
            // Error isn't a battle message, but it can be just as spammy if you
            // use macros with unavailable actions.
            case ChatType.Error:
            case ChatType.Damage:
            case ChatType.Miss:
            case ChatType.Action:
            case ChatType.Item:
            case ChatType.Healing:
            case ChatType.GainBuff:
            case ChatType.LoseBuff:
            case ChatType.GainDebuff:
            case ChatType.LoseDebuff:
            case ChatType.BattleSystem:
                return true;
            default:
                return false;
        }
    }

    public bool IsPlayerMessage()
    {
        switch (Type)
        {
            case ChatType.Say:
            case ChatType.Shout:
            case ChatType.TellOutgoing:
            case ChatType.TellIncoming:
            case ChatType.Party:
            case ChatType.CrossParty:
            case ChatType.Linkshell1:
            case ChatType.Linkshell2:
            case ChatType.Linkshell3:
            case ChatType.Linkshell4:
            case ChatType.Linkshell5:
            case ChatType.Linkshell6:
            case ChatType.Linkshell7:
            case ChatType.Linkshell8:
            case ChatType.CrossLinkshell1:
            case ChatType.CrossLinkshell2:
            case ChatType.CrossLinkshell3:
            case ChatType.CrossLinkshell4:
            case ChatType.CrossLinkshell5:
            case ChatType.CrossLinkshell6:
            case ChatType.CrossLinkshell7:
            case ChatType.CrossLinkshell8:
            case ChatType.FreeCompany:
            case ChatType.NoviceNetwork:
            case ChatType.Yell:
            case ChatType.ExtraChatLinkshell1:
            case ChatType.ExtraChatLinkshell2:
            case ChatType.ExtraChatLinkshell3:
            case ChatType.ExtraChatLinkshell4:
            case ChatType.ExtraChatLinkshell5:
            case ChatType.ExtraChatLinkshell6:
            case ChatType.ExtraChatLinkshell7:
            case ChatType.ExtraChatLinkshell8:
                return true;
            default:
                return false;
        }
    }

    public int ToSortCodeV2()
    {
        return (byte)Type << 16 | (byte)Source << 8 | (byte)Target;
    }

    public override bool Equals(object? obj)
    {
        if (obj == null)
            return false;

        if (obj is not ChatCode code)
            return false;

        return GetHashCode() == code.GetHashCode();
    }

    public override int GetHashCode()
    {
        return (byte)Type << 16 | (byte)Source << 8 | (byte)Target;
    }
}
