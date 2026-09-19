using Seamless.Execution;
namespace Seamless.Linq;
public partial class ScopedQuery<T>{
    public ScopedOrderedQuery<T,TKey> OrderBy<TKey>(Func<T,TKey> selector,IComparer<TKey>? comparer=null){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,this.Items,selector,comparer??Comparer<TKey>.Default);
    }
    public ScopedOrderedQuery<T,TKey> OrderByDescending<TKey>(Func<T,TKey> selector,IComparer<TKey>? comparer=null){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,this.Items,selector,new ReverseComparer<TKey>(comparer??Comparer<TKey>.Default));
    }
}
public sealed class ScopedOrderedQuery<T,TKey>:ScopedQuery<T>,IOrderedEnumerable<T>{
    private readonly Func<IEnumerable<T>> source;
    private readonly Func<T,TKey> selector;
    private readonly IComparer<TKey> comparer;
    internal ScopedOrderedQuery(ExecutionScope scope,Func<IEnumerable<T>> source,Func<T,TKey> selector,IComparer<TKey> comparer)
        :base(scope,()=>ExternalSort<T,TKey>.Sort(scope,source(),selector,comparer)){
        this.source=source;this.selector=selector;this.comparer=comparer;
    }
    public ScopedOrderedQuery<T,(TKey,TNext)> ThenBy<TNext>(Func<T,TNext> selector,IComparer<TNext>? comparer=null){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,this.source,x=>(this.selector(x),selector(x)),new CompositeComparer<TKey,TNext>(this.comparer,comparer??Comparer<TNext>.Default));
    }
    public ScopedOrderedQuery<T,(TKey,TNext)> ThenByDescending<TNext>(Func<T,TNext> selector,IComparer<TNext>? comparer=null){
        ArgumentNullException.ThrowIfNull(selector);
        return this.ThenBy(selector,new ReverseComparer<TNext>(comparer??Comparer<TNext>.Default));
    }
    IOrderedEnumerable<T> IOrderedEnumerable<T>.CreateOrderedEnumerable<TNext>(Func<T,TNext> keySelector,IComparer<TNext>? comparer,bool descending){
        if(descending){return this.ThenByDescending(keySelector,comparer);}
        return this.ThenBy(keySelector,comparer);
    }
}
file sealed class ReverseComparer<T>(IComparer<T> comparer):IComparer<T>{
    private readonly IComparer<T> comparer=comparer;
    public int Compare(T? x,T? y)=>this.comparer.Compare(y!,x!);
}
file sealed class CompositeComparer<TFirst,TSecond>(IComparer<TFirst> first,IComparer<TSecond> second):IComparer<(TFirst,TSecond)>{
    private readonly IComparer<TFirst> first=first;
    private readonly IComparer<TSecond> second=second;
    public int Compare((TFirst,TSecond) x,(TFirst,TSecond) y){
        var result=this.first.Compare(x.Item1,y.Item1);
        if(result!=0){return result;}
        return this.second.Compare(x.Item2,y.Item2);
    }
}
