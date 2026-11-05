using System.ComponentModel;
using System.Runtime.InteropServices;
using Seamless.Execution;

namespace Seamless.Linq;

/// <summary>生成されたGroupJoinで、既知範囲のキーごとに一致件数を保持する。</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class DenseGroupCounts<T>(ExecutionScope? scope,int minimum,int maximum):ICollection<T>,IDisposable{
    private OwnedBuffer? storage;
    private long[]? counts;
    private long selectedCount;

    public DenseGroupCounts(int minimum,int maximum):this(null,minimum,maximum){}

    public void AddKey(int key){
        Span<long> values;
        if(scope is null){
            values=this.counts??=new long[checked(maximum-minimum+1)];
        }else{
            this.storage??=scope.Rent(checked((maximum-minimum+1)*sizeof(long)));
            values=MemoryMarshal.Cast<byte,long>(this.storage.Memory.Span);
        }
        var index=key-minimum;
        values[index]=checked(values[index]+1);
    }

    // Count()だけを呼ぶことが証明済みの同期selectorへ一時的に渡す。
    public IEnumerable<T> SelectCount(int key){
        this.selectedCount=this.GetLongCount(key);
        return this;
    }

    public long GetLongCount(int key){
        if(key<minimum||key>maximum){return 0;}
        var index=key-minimum;
        if(this.counts is{} values){return values[index];}
        return this.storage is{} buffer?MemoryMarshal.Cast<byte,long>(buffer.Memory.Span)[index]:0;
    }

    int ICollection<T>.Count=>checked((int)this.selectedCount);
    bool ICollection<T>.IsReadOnly=>true;
    void ICollection<T>.Add(T value)=>throw new NotSupportedException();
    bool ICollection<T>.Remove(T value)=>throw new NotSupportedException();
    void ICollection<T>.Clear()=>throw new NotSupportedException();
    bool ICollection<T>.Contains(T value)=>throw new NotSupportedException();
    void ICollection<T>.CopyTo(T[] array,int index)=>throw new NotSupportedException();
    IEnumerator<T> IEnumerable<T>.GetEnumerator()=>throw new NotSupportedException();
    IEnumerator IEnumerable.GetEnumerator()=>throw new NotSupportedException();
    public void Dispose()=>this.storage?.Dispose();
}
