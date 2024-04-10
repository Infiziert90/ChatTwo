namespace ChatTwo.Util;

internal class Lender<T> {
    private readonly Func<T> _ctor;
    private readonly List<T> _items = new();
    private int _counter;

    internal Lender(Func<T> ctor) {
        _ctor = ctor;
    }

    internal void ResetCounter() {
        _counter = 0;
    }

    internal T Borrow() {
        if (_items.Count <= _counter) {
            _items.Add(_ctor());
        }

        return _items[_counter++];
    }
}
