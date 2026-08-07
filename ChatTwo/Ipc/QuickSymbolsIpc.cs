using ChatTwo.Ui.Handler;
using Dalamud.Plugin.Ipc;

namespace ChatTwo;

public sealed class QuickSymbolsIpc : IDisposable
{
    private const string Owner = "ChatTwo";
    private const string WatchInput = "QuickSymbols.WatchInput";
    private const string ClosePicker = "QuickSymbols.ClosePicker";
    private const string SymbolSelected = "QuickSymbols.SymbolSelected";

    private ICallGateSubscriber<string, bool>? watch;
    private ICallGateSubscriber<string, bool>? close;
    private ICallGateSubscriber<string, string, object?>? selected;
    private Action<string, string>? onSelected;

    // This is for tracking the input that most recently advertised itself as active.
    // QuickSymbols will send the selected symbol back ONLY through IPC; ChatTwo will still own the buffer and caret.
    private InputHandler? input;

    public QuickSymbolsIpc(Plugin _)
    {
        Register();
    }

    public void Watch(InputHandler handler, bool state)
    {
        // A false state means that this handler should no longer receive symbols.
        // This will preserve any other handler that became operational post this one.
        if (!state)
        {
            if (ReferenceEquals(input, handler))
                input = null;

            return;
        }

        input = handler;

        try
        {
            // The active input tells QuickSymbols that it is safe to react
            // to the user QuickSymbols keybind but only for this owner and while the input is active.
            watch?.InvokeFunc(Owner);
        }
        catch
        {
            // QuickSymbols is optional. Won't do anything unless called.
        }
    }

    private void Register()
    {
        try
        {
            // These subscribers will only be utilized if QuickSymbols is loaded
            // and the IPC available. Otherwise, ChatTwo will carry on functioning as it normally does.
            watch = Plugin.Interface.GetIpcSubscriber<string, bool>(WatchInput);
            close = Plugin.Interface.GetIpcSubscriber<string, bool>(ClosePicker);
            selected = Plugin.Interface.GetIpcSubscriber<string, string, object?>(SymbolSelected);

            // QuickSymbols doesnt modify ChatTwo input itself. It simply displays
            // the chosen symbol in this location and then ChatTwo should handle its insertion via its own InputHandler.
            onSelected = OnSelected;
            selected.Subscribe(onSelected);
        }
        catch (Exception ex)
        {
            Plugin.Log.Debug(ex, "QuickSymbols IPC could not be registered.");
        }
    }

    private void Close()
    {
        try
        {
            // This will terminate any picker session that was initiated for ChatTwo.
            // It should prevent outdated QuickSymbols target from remaining once ChatTwo has been unloaded.
            close?.InvokeFunc(Owner);
        }
        catch
        {

        }
    }

    private void OnSelected(string owner, string symbol)
    {
        // Multiple plugins may pick up on the same occurrence,
        // therefore the owner key is to ensure that each setup stays separate.
        if (owner != Owner || string.IsNullOrEmpty(symbol))
            return;

        // This approach is to ensure that the text buffer, blinking cursor and the
        // restoration of focus are all managed by ChatTwo itself.
        // For this to work, it channels the input directly through ChatTwo internal mechanisms.
        input?.InsertText(symbol);
    }

    public void Dispose()
    {
        // Any active picker is closed before the IPC subscriber goes away.
        Close();

        if (selected != null && onSelected != null)
        {
            selected.Unsubscribe(onSelected);
            onSelected = null;
        }
    }
}
