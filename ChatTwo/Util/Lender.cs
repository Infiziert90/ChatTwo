namespace ChatTwo.Util;

internal class Lender<T> {
    private readonly Func<T> _ctor;
    private readonly List<T> _items = new();
    private int _counter;

    internal Lender(Func<T> ctor) {
        this._ctor = ctor;
    }

    internal void ResetCounter() {
        this._counter = 0;
    }

    internal T Borrow() {
        if (this._items.Count <= this._counter) {
            this._items.Add(this._ctor());
        }

        return this._items[this._counter++];
    }
}
