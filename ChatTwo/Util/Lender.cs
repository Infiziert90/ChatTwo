namespace ChatTwo.Util;

internal class Lender<T>
{
    private readonly Func<T> Ctor;
    private readonly List<T> Items = [];
    private int Counter;

    internal Lender(Func<T> ctor)
    {
        Ctor = ctor;
    }

    internal void ResetCounter()
    {
        Counter = 0;
    }

    internal T Borrow()
    {
        if (Items.Count <= Counter)
            Items.Add(Ctor());

        return Items[Counter++];
    }
}
