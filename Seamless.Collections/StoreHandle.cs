namespace Seamless.Collections;

/// <summary>
/// ・ｽe・ｽX・ｽg・ｽp・ｽX・ｽg・ｽA・ｽ・ｽ・ｽ・会ｿｽb・ｽp・ｽ[・ｽB
/// ・ｽ・ｽﾌ的・ｽﾈス・ｽg・ｽA・ｽ^・ｽ・ｽB・ｽ・ｽ・ｽ・ｽ・ｽAFlushAndEvict / Save / Load / Address / NewDict ・ｽ・ｽ氓・ｽ・ｽ・ｽB
/// </summary>
public sealed class StoreHandle<TKey, TValue>:IDisposable
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private readonly IDisposable _store;
    private readonly Func<global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue>> _createDict;
    private readonly Action<bool> _flushAndEvict;
    private readonly Func<Guid> _save;
    private readonly Action<Guid> _load;
    private readonly Func<(long,long,long,long)> _getAddress;

    internal StoreHandle(
        IDisposable store,
        Func<global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue>> createDict,
        Action<bool> flushAndEvict,
        Func<Guid> save,
        Action<Guid> load,
        Func<(long,long,long,long)> getAddress) {
        this._store=store;
        this._createDict=createDict;
        this._flushAndEvict=flushAndEvict;
        this._save=save;
        this._load=load;
        this._getAddress=getAddress;
    }

    public global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> NewDict() =>
        this._createDict();
    public void FlushAndEvict(bool wait = true) =>
        this._flushAndEvict(wait);
    public Guid Save() =>
        this._save();
    public void Load(Guid token) =>
        this._load(token);
    public (long Begin,long Head,long Ro,long Tail) Address =>
        this._getAddress();
    public void Dispose() {
        this._store.Dispose();
    }
}
