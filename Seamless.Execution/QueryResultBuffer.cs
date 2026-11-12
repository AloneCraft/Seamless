using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace Seamless.Execution;

/// <summary>生成queryの結果構築中の配列・値を予算へ計上する。返した結果はcallerが所有する。</summary>
public sealed class QueryResultBuffer<T>:IDisposable{
    private readonly ExecutionScope scope;
    private readonly Action? validate;
    private readonly Codecs.IAdapter<T>? codec;
    private readonly ResourceReservation memory;
    private T[] values=[];
    private long payload;
    private bool disposed,finished;
    public QueryResultBuffer(ExecutionScope scope,Action? validate=null){
        ArgumentNullException.ThrowIfNull(scope);this.scope=scope;this.validate=validate;scope.ThrowIfUnavailable();validate?.Invoke();
        var codecs=scope.Codecs.Fork();if(codecs.CanEncode<T>()){this.codec=codecs.Get<T>();}
        this.memory=scope.Budget.ReserveMemory(64);try{scope.Own(this);}catch{this.memory.Dispose();throw;}
    }
    public int Count{get;private set;}
    public void Add(T value){
        this.Check();var payload=checked(this.payload+64L);
        if(this.codec is Codecs.IOwnedAdapter<T> owned){payload=checked(payload+owned.GetRetainedSize(value));}
        else if(this.codec is not null){payload=checked(payload+4L*this.codec.GetEncodedLength(value));}
        var capacity=this.values.Length;if(this.Count==capacity){capacity=Math.Max(4,checked(capacity*2));}
        var steady=checked(64L+capacity*(long)Unsafe.SizeOf<T>()+payload);
        if(capacity!=this.values.Length){
            this.memory.Resize(checked(steady+this.values.Length*(long)Unsafe.SizeOf<T>()));
            var values=new T[capacity];this.values.CopyTo(values,0);this.values=values;
        }
        this.memory.Resize(steady);this.values[this.Count++]=value;this.payload=payload;
    }
    public T[] ToArray(){
        this.Check();T[] result;
        if(this.Count==this.values.Length){result=this.values;}
        else{
            this.memory.Resize(checked(this.memory.Bytes+32L+this.Count*(long)Unsafe.SizeOf<T>()));result=new T[this.Count];this.values.AsSpan(0,this.Count).CopyTo(result);
        }
        this.Check();this.values=[];this.finished=true;this.memory.Resize(0);return result;
    }
    public List<T> ToList(){
        this.Check();this.memory.Resize(checked(this.memory.Bytes+64L+this.Count*(long)Unsafe.SizeOf<T>()));
        var result=new List<T>(this.Count);CollectionsMarshal.SetCount(result,this.Count);this.values.AsSpan(0,this.Count).CopyTo(CollectionsMarshal.AsSpan(result));
        this.Check();this.values=[];this.finished=true;this.memory.Resize(0);return result;
    }
    private void Check(){ObjectDisposedException.ThrowIf(this.disposed,this);if(this.finished){throw new InvalidOperationException("The result has already been transferred.");}this.scope.ThrowIfUnavailable();this.validate?.Invoke();}
    public void Dispose(){if(this.disposed){return;}this.disposed=true;this.values=[];try{this.memory.Dispose();}finally{this.scope.Release(this);}}
}
