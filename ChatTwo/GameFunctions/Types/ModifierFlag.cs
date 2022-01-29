namespace ChatTwo.GameFunctions.Types;

[Flags]
internal enum ModifierFlag {
    Shift = 1 << 0,
    Ctrl = 1 << 1,
    Alt = 1 << 2,
}
