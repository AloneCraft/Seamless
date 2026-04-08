using Seamless.Collections;

using Tsavorite.core;

namespace Test.Seamless.Collections;

/// <summary>
/// テスト用ストア操作ラッパー。
/// 具体的なストア型を隠蔽し、FlushAndEvict / Save / Load / Address / NewDict を提供する。
/// </summary>
public sealed class StoreHandle<TKey, TValue> : IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue>
{
    private readonly IDisposable _store;
    private readonly Func<ISeamlessDictionary<TKey, TValue>> _createDict;
    private readonly Action<bool> _flushAndEvict;
    private readonly Func<Guid> _save;
    private readonly Action<Guid> _load;
    private readonly Func<(long, long, long, long)> _getAddress;

    internal StoreHandle(
        IDisposable store,
        Func<ISeamlessDictionary<TKey, TValue>> createDict,
        Action<bool> flushAndEvict,
        Func<Guid> save,
        Action<Guid> load,
        Func<(long, long, long, long)> getAddress)
    {
        _store = store;
        _createDict = createDict;
        _flushAndEvict = flushAndEvict;
        _save = save;
        _load = load;
        _getAddress = getAddress;
    }

    public ISeamlessDictionary<TKey, TValue> NewDict() => _createDict();
    public void FlushAndEvict(bool wait = true) => _flushAndEvict(wait);
    public Guid Save() => _save();
    public void Load(Guid token) => _load(token);
    public (long Begin, long Head, long Ro, long Tail) Address => _getAddress();
    public void Dispose() => _store.Dispose();
}
