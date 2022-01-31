using ChatTwo.Code;

namespace ChatTwo.GameFunctions.Types;

internal class ChannelSwitchInfo {
    internal InputChannel? Channel { get; }
    internal bool Permanent { get; }
    internal RotateMode Rotate { get; }
    internal string? Text { get; }

    internal ChannelSwitchInfo(InputChannel? channel, bool permanent = false, RotateMode rotate = RotateMode.None, string? text = null) {
        this.Channel = channel;
        this.Permanent = permanent;
        this.Rotate = rotate;
        this.Text = text;
    }
}
