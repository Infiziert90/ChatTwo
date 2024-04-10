using Dalamud.Game.Command;

namespace ChatTwo;

internal sealed class Commands : IDisposable {
    private Plugin Plugin { get; }
    private Dictionary<string, CommandWrapper> Registered { get; } = new();

    internal Commands(Plugin plugin) {
        Plugin = plugin;
    }

    public void Dispose() {
        foreach (var name in Registered.Keys) {
            Plugin.CommandManager.RemoveHandler(name);
        }
    }

    internal void Initialise() {
        foreach (var wrapper in Registered.Values) {
            Plugin.CommandManager.AddHandler(wrapper.Name, new CommandInfo(Invoke) {
                HelpMessage = wrapper.Description ?? string.Empty,
                ShowInHelp = wrapper.ShowInHelp,
            });
        }
    }

    internal CommandWrapper Register(string name, string? description = null, bool? showInHelp = null) {
        if (Registered.TryGetValue(name, out var wrapper)) {
            if (description != null) {
                wrapper.Description = description;
            }

            if (showInHelp != null) {
                wrapper.ShowInHelp = showInHelp.Value;
            }

            return wrapper;
        }

        Registered[name] = new CommandWrapper(name, description, showInHelp ?? true);
        return Registered[name];
    }

    private void Invoke(string command, string arguments) {
        if (!Registered.TryGetValue(command, out var wrapper)) {
            Plugin.Log.Warning($"Missing registration for command {command}");
            return;
        }

        try {
            wrapper.Invoke(command, arguments);
        } catch (Exception ex) {
            Plugin.Log.Error(ex, $"Error while executing command {command}");
        }
    }
}

internal sealed class CommandWrapper {
    internal string Name { get; }
    internal string? Description { get; set; }
    internal bool ShowInHelp { get; set; }

    internal event Action<string, string>? Execute;

    internal CommandWrapper(string name, string? description, bool showInHelp) {
        Name = name;
        Description = description;
        ShowInHelp = showInHelp;
    }

    internal void Invoke(string command, string arguments) {
        Execute?.Invoke(command, arguments);
    }
}
