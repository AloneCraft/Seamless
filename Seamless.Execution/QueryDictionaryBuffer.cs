using System.Runtime.CompilerServices;
namespace Seamless.Execution;

/// <summary>辞書と既知の値の構築費用を予約し、例外時の予約を戻す。確定した辞書はcaller所有。</summary>
public sealed class QueryDictionaryBuffer<TKey,TValue>:IDisposable where TKey:notnull{
    private readonly ExecutionScope scope;
    private readonly Action? validate;
    private readonly Codecs.IAdapter<TKey>? keyCodec;
    private readonly Codecs.IAdapter<TValue>? valueCodec;
    private readonly ResourceReservation memory;
    private Dictionary<TKey,TValue>? values;
    private long payload;
    private bool disposed;
    public QueryDictionaryBuffer(ExecutionScope scope,IEqualityComparer<TKey>? comparer=null,Action? validate=null){
        ArgumentNullException.ThrowIfNull(scope);this.scope=scope;this.validate=validate;scope.ThrowIfUnavailable();validate?.Invoke();
        var codecs=scope.Codecs.Fork();if(codecs.CanEncode<TKey>()){this.keyCodec=codecs.Get<TKey>();}if(codecs.CanEncode<TValue>()){this.valueCodec=codecs.Get<TValue>();}
        this.memory=scope.Budget.ReserveMemory(128);
        try{this.values=new(comparer);scope.Own(this);}catch{this.memory.Dispose();throw;}
    }
    public int Count=>this.values?.Count??0;
    public void Add(TKey key,TValue value){
        this.Check();var payload=checked(this.payload+128L);
        if(this.keyCodec is not null){payload=checked(payload+4L*this.keyCodec.GetEncodedLength(key));}if(this.valueCodec is not null){payload=checked(payload+4L*this.valueCodec.GetEncodedLength(value));}
        // entry/bucketの余剰容量と再確保中の旧領域を保守的に予約。件数だけで全件配列を作らない。
        var bytes=checked(128L+(this.values!.Count+1L)*(Unsafe.SizeOf<TKey>()+Unsafe.SizeOf<TValue>()+32L)*6+payload);var previous=this.memory.Bytes;
        this.memory.Resize(bytes);
        try{this.values.Add(key,value);}catch{this.memory.Resize(previous);throw;}
        this.payload=payload;
    }
    public Dictionary<TKey,TValue> ToDictionary(){this.Check();var result=this.values!;this.values=null;return result;}
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.values is null){throw new InvalidOperationException("The result has already been transferred.");}this.scope.ThrowIfUnavailable();this.validate?.Invoke();}
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.values=null;try{this.memory.Dispose();}finally{this.scope.Release(this);}}
}
