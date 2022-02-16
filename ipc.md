# Context Menu IPC Integration

If you want to display custom menu items in the chat context menu, you can use
Chat 2's IPC.

Here's an example.

```c#
public class ContextMenuIntegration {
    // This is used to register your plugin with the IPC. It will return an ID
    // that you should save for later.
    private ICallGateSubscriber<string> Register { get; }
    // This is used to unregister your plugin from the IPC. You should call this
    // when your plugin is unloaded.
    private ICallGateSubscriber<string, object?> Unregister { get; }
    // You should subscribe to this event in order to receive a notification
    // when Chat 2 is loaded or updated, so you can re-register.
    private ICallGateSubscriber<object?> Available { get; }
    // Subscribe to this to draw your custom context menu items.
    private ICallGateSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?> Invoke { get; }

    // The registration ID.
    private string? _id;

    public ChatTwoIpc(DalamudPluginInterface @interface) {
        this.Register = @interface.GetIpcSubscriber<string>("ChatTwo.Register");
        this.Unregister = @interface.GetIpcSubscriber<string, object?>("ChatTwo.Unregister");
        this.Invoke = @interface.GetIpcSubscriber<string, PlayerPayload?, ulong, Payload?, SeString?, SeString?, object?>("ChatTwo.Invoke");
        this.Available = @interface.GetIpcSubscriber<object?>("ChatTwo.Available");
    }

    public void Enable() {
        // When Chat 2 becomes available (if it loads after this plugin) or when
        // Chat 2 is updated, register automatically.
        this.Available.Subscribe(() => this.Register());
        // Register if Chat 2 is already loaded.
        this.Register();
        
        // Listen for context menu events.
        this.Invoke.Subscribe(this.Integration);
    }
    
    private void Register() {
        // Register and save the registration ID.
        this._id = this.Register.InvokeFunc();
    }

    public void Disable() {
        if (this._id != null) {
            this.Unregister.InvokeAction(this._id);
            this._id = null;
        }

        this.Invoke.Unsubscribe(this.Integration);
    }

    private void Integration(string id, PlayerPayload? sender, ulong contentId, Payload? payload, SeString? senderString, SeString? content) {
        // Make sure the ID is the same as the saved registration ID.
        if (id != this._id) {
            return;
        }

        // Draw your custom menu items here.
        // sender is the first PlayerPayload contained in the sender SeString
        // contentId is the content ID of the message sender or 0 if not known
        // payload is the payload that was right-clicked, if any (excluding text)
        // senderString is the message sender SeString
        // content is the message content SeString
        if (ImGui.Selectable("Test plugin")) {
            PluginLog.Log($"hi!\nsender: {sender}\ncontent id: {contentId:X}\npayload: {payload}\nsender string: {senderString}\ncontent string: {content}");
        }
    }
}
```

All integrations are called inside of an ImGui `BeginMenu`.
