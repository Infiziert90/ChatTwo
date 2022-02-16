using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

internal sealed class IpcManager : IDisposable {
    private DalamudPluginInterface Interface { get; }
    private ICallGateProvider<string> RegisterGate { get; }
    private ICallGateProvider<string, object?> UnregisterGate { get; }
    private ICallGateProvider<object?> AvailableGate { get; }
    private ICallGateProvider<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> InvokeGate { get; }

    internal List<string> Registered { get; } = new();

    public IpcManager(DalamudPluginInterface pluginInterface) {
        this.Interface = pluginInterface;

        this.RegisterGate = this.Interface.GetIpcProvider<string>("ChatTwo.Register");
        this.RegisterGate.RegisterFunc(this.Register);

        this.AvailableGate = this.Interface.GetIpcProvider<object?>("ChatTwo.Available");

        this.UnregisterGate = this.Interface.GetIpcProvider<string, object?>("ChatTwo.Unregister");
        this.UnregisterGate.RegisterAction(this.Unregister);

        this.InvokeGate = this.Interface.GetIpcProvider<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");

        this.AvailableGate.SendMessage();
    }

    internal void Invoke(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content) {
        this.InvokeGate.SendMessage(id, sender, contentId, payload, senderString, content);
    }

    private string Register() {
        var id = Guid.NewGuid().ToString();
        this.Registered.Add(id);
        return id;
    }

    private void Unregister(string id) {
        this.Registered.Remove(id);
    }

    public void Dispose() {
        this.UnregisterGate.UnregisterFunc();
        this.RegisterGate.UnregisterFunc();
        this.Registered.Clear();
    }
}
