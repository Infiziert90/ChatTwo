using ChatTwo.Code;
using Dalamud.Plugin.Ipc;

namespace ChatTwo.Ipc;

using ChatInputState = (bool InputVisible, bool InputFocused, bool HasText, bool IsTyping, int TextLength, ChatType ChannelType);

internal sealed class TypingIpc : IDisposable
{
    private Plugin Plugin { get; }

    private ICallGateProvider<ChatInputState> StateQueryGate { get; }
    private ICallGateProvider<ChatInputState, object?> StateChangedGate { get; }

    private ChatInputState LastState;
    private bool HasState;

    internal TypingIpc(Plugin plugin)
    {
        Plugin = plugin;

        StateQueryGate = Plugin.Interface.GetIpcProvider<ChatInputState>("ChatTwo.GetChatInputState");
        StateChangedGate = Plugin.Interface.GetIpcProvider<ChatInputState, object?>("ChatTwo.ChatInputStateChanged");

        StateQueryGate.RegisterFunc(GetState);
    }

    private ChatInputState BuildState()
    {
        var log = Plugin.ChatLogWindow;
        var chat = log.Chat ?? string.Empty;
        var hasText = !string.IsNullOrWhiteSpace(chat);
        var usedChannel = Plugin.CurrentTab?.CurrentChannel;
        var inputChannel = usedChannel is not null
            ? (usedChannel.UseTempChannel ? usedChannel.TempChannel : usedChannel.Channel)
            : InputChannel.Invalid;
        var channelType = inputChannel.ToChatType();

        return (InputVisible: !log.IsHidden,
            InputFocused: log.InputFocused,
            HasText: hasText,
            IsTyping: log.InputFocused && hasText,
            TextLength: chat.Length,
            ChannelType: channelType);
    }

    private ChatInputState GetState()
        => BuildState();

    internal void Update()
    {
        var state = BuildState();
        if (HasState && state.Equals(LastState))
            return;

        HasState = true;
        LastState = state;
        StateChangedGate.SendMessage(state);
    }

    public void Dispose()
    {
        StateQueryGate.UnregisterFunc();
    }
}
