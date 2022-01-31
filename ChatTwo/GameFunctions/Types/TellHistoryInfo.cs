namespace ChatTwo.GameFunctions.Types;

internal sealed class TellHistoryInfo {
    internal string Name { get; }
    internal uint World { get; }
    internal ulong ContentId { get; }

    internal TellHistoryInfo(string name, uint world, ulong contentId) {
        this.Name = name;
        this.World = world;
        this.ContentId = contentId;
    }
}
