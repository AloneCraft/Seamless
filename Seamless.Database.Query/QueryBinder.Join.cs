using Seamless.Collections.Persisted;
namespace Seamless.Database.Query;
internal sealed partial class QueryBinder{
    private static IEnumerable<object?[]>MergeJoin(QueryExecution execution,object?[]?outer,BoundSource left,BoundSource right,BoundScalar[]leftKeys,BoundScalar[]rightKeys,Func<EvalFrame,bool?>condition,bool leftOuter){
        var comparer=new SqlRowComparer(types:leftKeys.Select(x=>x.Type).ToArray());
        using var first=SqlBoundedOperators.Sort(execution,left.Rows(execution,outer),row=>Keys(row,leftKeys),comparer).GetEnumerator();
        using var second=SqlBoundedOperators.Sort(execution,right.Rows(execution,outer),row=>Keys(row,rightKeys),comparer).GetEnumerator();
        var hasRight=second.MoveNext();object?[]?groupKey=null;ScopedStoredSequence<object?[]>?group=null;
        try{
            while(first.MoveNext()){
                execution.Scope.ThrowIfUnavailable();var key=Keys(first.Current,leftKeys);var matched=false;
                if(key.All(x=>x is not null)){
                    if(group is null||comparer.Compare(groupKey,key)!=0){
                        group?.Dispose();group=null;groupKey=null;
                        while(hasRight&&comparer.Compare(Keys(second.Current,rightKeys),key)<0){hasRight=second.MoveNext();}
                        if(hasRight&&comparer.Compare(Keys(second.Current,rightKeys),key)==0){
                            group=new(execution.Scope);groupKey=key;
                            do{group.Append(second.Current);hasRight=second.MoveNext();}while(hasRight&&comparer.Compare(Keys(second.Current,rightKeys),key)==0);
                            group.Seal();
                        }
                    }
                    if(group is not null&&comparer.Compare(groupKey,key)==0){foreach(var candidate in group){var row=first.Current.Concat(candidate).ToArray();if(condition(new(row,outer,execution))==true){matched=true;yield return row;}}}
                }
                if(!matched&&leftOuter){yield return first.Current.Concat(new object?[right.Columns.Count]).ToArray();}
            }
        }finally{group?.Dispose();}
        object?[]Keys(object?[]row,BoundScalar[]keys){var frame=new EvalFrame(row,outer,execution);return keys.Select(x=>x.Value(frame)).ToArray();}
    }
}
