using Seamless.Collections;
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
        this._store = store;
        this._createDict = createDict;
        this._flushAndEvict = flushAndEvict;
        this._save = save;
        this._load = load;
        this._getAddress = getAddress;
    }

    public ISeamlessDictionary<TKey, TValue> NewDict() =>
        this._createDict();
    public void FlushAndEvict(bool wait = true) =>
        this._flushAndEvict(wait);
    public Guid Save() =>
        this._save();
    public void Load(Guid token) =>
        this._load(token);
    public (long Begin, long Head, long Ro, long Tail) Address =>
        this._getAddress();
    public void Dispose() =>
        this._store.Dispose();
}
