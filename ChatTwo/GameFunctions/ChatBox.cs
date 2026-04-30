using System.Text;
using ChatTwo.Resources;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace ChatTwo.GameFunctions;

public unsafe class ChatBox
{
    public static void SendMessageUnsafe(byte[] message)
    {
        var mes = Utf8String.FromSequence(message.NullTerminate());
        UIModule.Instance()->ProcessChatBoxEntry(mes);
        mes->Dtor(true);
    }

    public static void SendMessage(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        if (bytes.Length == 0)
            throw new ArgumentException(Language.ChatBox_Error_Empty, nameof(message));

        if (bytes.Length > 500)
            throw new ArgumentException(Language.ChatBox_Error_Too_Long, nameof(message));

        if (message.Length != SanitiseText(message).Length)
            throw new ArgumentException(Language.ChatBox_Error_Invalid, nameof(message));

        SendMessageUnsafe(bytes);
    }

    private static string SanitiseText(string text)
    {
        var uText = Utf8String.FromString(text);

        uText->SanitizeString((AllowedEntities) 0x27F);
        var sanitised = uText->ToString();
        uText->Dtor(true);

        return sanitised;
    }
}