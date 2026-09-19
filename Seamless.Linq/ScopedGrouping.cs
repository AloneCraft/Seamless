using Seamless.Execution;
namespace Seamless.Linq;
public partial class ScopedQuery<T>{
    public ScopedQuery<IGrouping<TKey,T>> GroupBy<TKey>(Func<T,TKey> selector,IEqualityComparer<TKey>? comparer=null)=>this.GroupBy(selector,static x=>x,comparer);
    public ScopedQuery<IGrouping<TKey,TElement>> GroupBy<TKey,TElement>(Func<T,TKey> keySelector,Func<T,TElement> elementSelector,IEqualityComparer<TKey>? comparer=null){
        ArgumentNullException.ThrowIfNull(keySelector);ArgumentNullException.ThrowIfNull(elementSelector);
        return new(this.Scope,Build);
        IEnumerable<IGrouping<TKey,TElement>> Build(){
            var lookup=new ScopedLookup<TKey,TElement>(this.Scope,comparer);
            var complete=false;
            try{
                foreach(var value in this.Items()){lookup.Add(keySelector(value),elementSelector(value));}
                complete=true;
                foreach(var group in lookup.Groups()){yield return group;}
            }finally{if(!complete){lookup.Dispose();}}
        }
    }
    public ScopedQuery<TResult> Join<TInner,TKey,TResult>(IEnumerable<TInner> inner,Func<T,TKey> outerKeySelector,Func<TInner,TKey> innerKeySelector,Func<T,TInner,TResult> resultSelector,IEqualityComparer<TKey>? comparer=null){
        ArgumentNullException.ThrowIfNull(inner);ArgumentNullException.ThrowIfNull(outerKeySelector);
        ArgumentNullException.ThrowIfNull(innerKeySelector);ArgumentNullException.ThrowIfNull(resultSelector);
        return new(this.Scope,Build);
        IEnumerable<TResult> Build(){
            using var outer=this.Items().GetEnumerator();
            if(!outer.MoveNext()){yield break;}
            using var lookup=new ScopedLookup<TKey,TInner>(this.Scope,comparer);
            foreach(var value in this.Source(inner)){this.Scope.ThrowIfUnavailable();lookup.Add(innerKeySelector(value),value);}
            do{
                foreach(var match in lookup.Match(outerKeySelector(outer.Current))){yield return resultSelector(outer.Current,match);}
            }while(outer.MoveNext());
        }
    }
    public ScopedQuery<T> Distinct(IEqualityComparer<T>? comparer=null)=>this.DistinctBy(static x=>x,comparer);
    public ScopedQuery<T> DistinctBy<TKey>(Func<T,TKey> selector,IEqualityComparer<TKey>? comparer=null){
        ArgumentNullException.ThrowIfNull(selector);
        return new(this.Scope,Build);
        IEnumerable<T> Build(){
            using var lookup=new ScopedLookup<TKey,byte>(this.Scope,comparer);
            foreach(var value in this.Items()){
                var key=selector(value);
                if(!System.Linq.Enumerable.Any(lookup.Match(key))){lookup.Add(key,0);yield return value;}
            }
        }
    }
    public ScopedQuery<T> Union(IEnumerable<T> second,IEqualityComparer<T>? comparer=null)=>this.Concat(second).Distinct(comparer);
    public ScopedQuery<T> Intersect(IEnumerable<T> second,IEqualityComparer<T>? comparer=null)=>this.SetFilter(second,comparer,true);
    public ScopedQuery<T> Except(IEnumerable<T> second,IEqualityComparer<T>? comparer=null)=>this.SetFilter(second,comparer,false);
    private ScopedQuery<T> SetFilter(IEnumerable<T> second,IEqualityComparer<T>? comparer,bool present){
        ArgumentNullException.ThrowIfNull(second);
        return new(this.Scope,Build);
        IEnumerable<T> Build(){
            using var lookup=new ScopedLookup<T,byte>(this.Scope,comparer);
            using var emitted=new ScopedLookup<T,byte>(this.Scope,comparer);
            foreach(var value in this.Source(second)){
                this.Scope.ThrowIfUnavailable();
                if(!System.Linq.Enumerable.Any(lookup.Match(value))){lookup.Add(value,0);}
            }
            foreach(var value in this.Items()){
                if(System.Linq.Enumerable.Any(lookup.Match(value))==present&&!System.Linq.Enumerable.Any(emitted.Match(value))){
                    emitted.Add(value,0);yield return value;
                }
            }
        }
    }
}
