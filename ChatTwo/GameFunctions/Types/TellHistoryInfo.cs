namespace ChatTwo.GameFunctions.Types;

internal sealed class TellHistoryInfo {
    internal string Name { get; }
    internal uint World { get; }
    internal ulong ContentId { get; }

    internal TellHistoryInfo(string name, uint world, ulong contentId) {
        Name = name;
        World = world;
        ContentId = contentId;
    }
}
