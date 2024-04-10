namespace ChatTwo.Ui;

internal class AutoCompleteInfo {
    internal string ToComplete;
    internal int StartPos { get; }
    internal int EndPos { get; }

    internal AutoCompleteInfo(string toComplete, int startPos, int endPos) {
        ToComplete = toComplete;
        StartPos = startPos;
        EndPos = endPos;
    }
}
