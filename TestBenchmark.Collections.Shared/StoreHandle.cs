namespace Test.Seamless.Collections;

/// <summary>
/// �e�X�g�p�X�g�A���색�b�p�[�B
/// ��̓I�ȃX�g�A�^��B�����AFlushAndEvict / Save / Load / Address / NewDict ��񋟂���B
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
        _store=store;
        _createDict=createDict;
        _flushAndEvict=flushAndEvict;
        _save=save;
        _load=load;
        _getAddress=getAddress;
    }

    public global::Seamless.Tsavorite.Collections.IDictionary<TKey,TValue> NewDict() =>
        _createDict();
    public void FlushAndEvict(bool wait = true) =>
        _flushAndEvict(wait);
    public Guid Save() =>
        _save();
    public void Load(Guid token) =>
        _load(token);
    public (long Begin,long Head,long Ro,long Tail) Address =>
        _getAddress();
    public void Dispose() {
        _store.Dispose();
    }
}
