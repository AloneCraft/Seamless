using Seamless.Collections.Persisted;
using CommonStore=Seamless.Execution.Store;
namespace Test.Persisted.Collections.Work;
public class SortedList{
    [Fact]public void DuplicateRowsAndQueryExamples(){
        using var owner=CommonStore.CreateTemporary();
        var comparer=Comparer<(int Key,int Amount)>.Create(static (left,right)=>left.Key.CompareTo(right.Key));
        using var rows=owner.CreatePersistedSortedList<(int Key,int Amount)>("rows",comparer,sortMemoryBytes:64);
        rows.Add((2,5));rows.Add((1,10));rows.Add((1,20));rows.Add((2,7));
        Assert.Equal((2,5),rows[0]);Assert.Throws<InvalidOperationException>(()=>rows.IndexOf((1,0)));
        rows.CompleteAdding();
        Assert.Equal(new[]{(1,10),(1,20),(2,5),(2,7)},System.Linq.Enumerable.ToArray(rows));
        Assert.Equal(4,rows.Count);Assert.Equal(0,rows.IndexOf((1,999)));
        // GROUP BY は比較順を逐次集計し、全グループをメモリへ保持しない。
        using var totals=owner.CreatePersistedHashDictionary<int,int>("totals");
        int key=-1,sum=0;
        foreach(var row in rows){
            if(key!=row.Key&&key>=0){totals.Add(key,sum);sum=0;}
            key=row.Key;sum+=row.Amount;
        }
        if(key>=0){totals.Add(key,sum);}
        Assert.Equal(30,totals[1]);Assert.Equal(12,totals[2]);
        // JOIN の同一キー行を失わず、範囲検索から全一致行を逐次消費する。
        var matches=0;
        foreach(var row in rows.Range((1,0),(2,0))){Assert.Equal(1,row.Key);matches++;}
        Assert.Equal(2,matches);
    }
    [Fact]public void EnumerationRejectsChanges(){
        using var owner=CommonStore.CreateTemporary();
        using var rows=owner.CreatePersistedSortedList<int>("rows");
        rows.Add(2);rows.Add(1);
        using var enumerator=rows.GetEnumerator();Assert.True(enumerator.MoveNext());
        rows.CompleteAdding();Assert.Throws<InvalidOperationException>(()=>enumerator.MoveNext());
    }
}
