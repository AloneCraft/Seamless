using Seamless.Execution;
namespace Seamless.Linq;
public static class ScopedQueryExtensions{
    public static ScopedQuery<T> Query<T>(this ExecutionScope scope,IEnumerable<T> source){
        ArgumentNullException.ThrowIfNull(scope);ArgumentNullException.ThrowIfNull(source);
        scope.ThrowIfUnavailable();
        if(source is ScopedQuery<T> query&&ReferenceEquals(query.Scope,scope)){return query;}
        return new(scope,()=>source);
    }
}
public partial class ScopedQuery<T>:IEnumerable<T>{
    private readonly Func<IEnumerable<T>> factory;
    internal ScopedQuery(ExecutionScope scope,Func<IEnumerable<T>> factory){this.Scope=scope;this.factory=factory;}
    public ExecutionScope Scope{get;}
    internal IEnumerable<T> Items(){
        this.Scope.ThrowIfUnavailable();
        foreach(var value in this.factory()){this.Scope.ThrowIfUnavailable();yield return value;}
    }
    internal IEnumerable<TItem> Source<TItem>(IEnumerable<TItem> source){
        if(source is ScopedQuery<TItem> query&&ReferenceEquals(query.Scope,this.Scope)){return query.Items();}
        return source;
    }
    public IEnumerator<T> GetEnumerator()=>this.Enumerate().GetEnumerator();
    private IEnumerable<T> Enumerate(){
        using var operation=this.Scope.Enter();
        foreach(var value in this.Items()){this.Scope.ThrowIfUnavailable();yield return value;}
    }
    IEnumerator IEnumerable.GetEnumerator()=>this.GetEnumerator();
    public ScopedQuery<T> Where(Func<T,bool> predicate){
        ArgumentNullException.ThrowIfNull(predicate);
        return new(this.Scope,()=>System.Linq.Enumerable.Where(this.Items(),predicate));
    }
    public ScopedQuery<TResult> Select<TResult>(Func<T,TResult> selector){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,()=>System.Linq.Enumerable.Select(this.Items(),selector));
    }
    public ScopedQuery<TResult> SelectMany<TCollection,TResult>(Func<T,IEnumerable<TCollection>> selector,Func<T,TCollection,TResult> resultSelector){
        ArgumentNullException.ThrowIfNull(selector);ArgumentNullException.ThrowIfNull(resultSelector);
        return new(this.Scope,()=>System.Linq.Enumerable.SelectMany(this.Items(),x=>this.Source(selector(x)),resultSelector));
    }
    public ScopedQuery<TResult> SelectMany<TResult>(Func<T,IEnumerable<TResult>> selector){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,()=>System.Linq.Enumerable.SelectMany(this.Items(),x=>this.Source(selector(x))));
    }
    public ScopedQuery<T> Take(int count)=>new(this.Scope,()=>System.Linq.Enumerable.Take(this.Items(),count));
    public ScopedQuery<T> Skip(int count)=>new(this.Scope,()=>System.Linq.Enumerable.Skip(this.Items(),count));
    public ScopedQuery<T> Concat(IEnumerable<T> other){
        ArgumentNullException.ThrowIfNull(other);
        return new(this.Scope,()=>System.Linq.Enumerable.Concat(this.Items(),this.Source(other)));
    }
    public long LongCount(){
        long count=0;
        foreach(var value in this){count=checked(count+1);}
        return count;
    }
    public int Count()=>checked((int)this.LongCount());
    public bool Any(){
        using var enumerator=this.GetEnumerator();
        return enumerator.MoveNext();
    }
    public bool Any(Func<T,bool> predicate)=>this.Where(predicate).Any();
    public T First(){
        using var enumerator=this.GetEnumerator();
        if(!enumerator.MoveNext()){throw new InvalidOperationException("Sequence contains no elements.");}
        return enumerator.Current;
    }
    public List<T> ToList(){
        using var reservation=this.Scope.Budget.ReserveMemory(64);
        return this.Materialize(reservation);
    }
    public T[] ToArray(){
        using var reservation=this.Scope.Budget.ReserveMemory(64);
        var list=this.Materialize(reservation);
        reservation.Resize(checked(reservation.Bytes+32L+list.Count*(long)Unsafe.SizeOf<T>()));
        return list.ToArray();
    }
    private List<T> Materialize(ResourceReservation reservation){
        var result=new List<T>();
        long estimatedValues=0;
        var canEncode=this.Scope.Codecs.CanEncode<T>();
        foreach(var item in this){
            if(canEncode){estimatedValues=checked(estimatedValues+this.Scope.Codecs.Get<T>().GetEncodedLength(item)*4L);}
            var capacity=result.Capacity;
            if(result.Count==capacity){capacity=Math.Max(4,checked(capacity*2));}
            var steady=checked(64L+capacity*(long)(Unsafe.SizeOf<T>()+16)+estimatedValues);
            if(result.Capacity!=capacity){
                reservation.Resize(checked(steady+result.Capacity*(long)Unsafe.SizeOf<T>()));
                result.Capacity=capacity;
            }
            reservation.Resize(steady);
            result.Add(item);
        }
        return result;
    }
    public Seamless.Collections.Persisted.ScopedStoredSequence<T> ToStorage(){
        var storage=new Seamless.Collections.Persisted.ScopedStoredSequence<T>(this.Scope);
        try{
            foreach(var value in this){storage.Append(value);}
            storage.Seal();
            return storage;
        }catch{storage.Dispose();throw;}
    }
}
