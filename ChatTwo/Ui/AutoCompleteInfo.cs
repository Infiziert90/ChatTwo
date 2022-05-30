namespace ChatTwo.Ui;

internal class AutoCompleteInfo {
    internal string ToComplete;
    internal int StartPos { get; }
    internal int EndPos { get; }

    internal AutoCompleteInfo(string toComplete, int startPos, int endPos) {
        this.ToComplete = toComplete;
        this.StartPos = startPos;
        this.EndPos = endPos;
    }
}
