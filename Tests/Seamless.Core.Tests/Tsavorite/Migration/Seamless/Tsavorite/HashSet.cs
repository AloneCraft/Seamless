using CommonStore=global::Seamless.Execution.Store;
namespace Test.Seamless.Tsavorite;
/// <summary>Tsavorite の HashSet が System.Collections.Generic.ISet&lt;T&gt; として BCL の HashSet と同じ答えを返すことの仕様。</summary>
public class HashSet{
    private static readonly long[][] Selves=[[],[1],[1,2],[1,2,3],[5,6,7,8]];
    private static readonly long[][] Others=[[],[1],[1,2],[1,2,3],[1,2,3,4],[3,3,2,1],[9],[5,6,7,8,9]];
    private static void Check(Func<global::System.Collections.Generic.ISet<long>,IEnumerable<long>,bool> relation){
        using var store=CommonStore.CreateMemory();
        var round=0;
        foreach(var self in Selves){
            using var data=store.CreateTsavoriteHashSet<long>("s"+round++);
            using var set=data.Create();
            foreach(var item in self){
                set.Add(item);
            }
            global::System.Collections.Generic.ISet<long> standard=set;
            var reference=new global::System.Collections.Generic.HashSet<long>(self);
            foreach(var other in Others){
                var label=$"self=[{string.Join(",",self)}] other=[{string.Join(",",other)}]";
                Assert.True(relation(reference,other)==relation(standard,other),label+" (array)");
                Assert.True(relation(reference,other)==relation(standard,new global::System.Collections.Generic.HashSet<long>(other)),label+" (BCL set)");
            }
        }
    }
    [Fact,Trait("Category","Unit")]public void IntersectWith(){
        var root=Path.Combine(Path.GetTempPath(),"seamless-tsavorite-hashset-tests",Guid.NewGuid().ToString("N"));
        try{
            //共通ストア(一時)から作った集合の作業リストはストアのフォルダの中に作られ、終われば消える。結果は BCL と同じ
            using var store=CommonStore.CreateTemporary(temporaryDirectory:root);
            using var data=store.CreateTsavoriteHashSet<long>("ids");
            using var set=data.Create();
            for(long i=0;i<1000;i++){
                set.Add(i);
            }
            var workLists=Path.Combine(store.GetComponentDirectory("ids"),"worklists");
            var probe=new ProbeSet(()=>Assert.True(Directory.Exists(Path.Combine(workLists,"hashset-intersect-ienumerable"))));
            for(long i=0;i<1000;i+=2){
                probe.Add(i);
            }
            set.IntersectWith((global::System.Collections.Generic.ISet<long>)probe);
            Assert.True(probe.Probed);
            Assert.Equal(500,set.GetLongCount());
            Assert.True(set.Contains(998));
            Assert.False(set.Contains(999));
            Assert.Empty(Directory.EnumerateDirectories(Path.Combine(workLists,"hashset-intersect-ienumerable")));
        }finally{
            if(Directory.Exists(root)){
                Directory.Delete(root,true);
            }
        }
    }
    // 最初の Contains 呼び出しのときに、作業リストの置き場所を確かめるための集合。
    private sealed class ProbeSet(Action onFirstContains):global::System.Collections.Generic.HashSet<long>,global::System.Collections.Generic.ISet<long>{
        public bool Probed{get;private set;}
        bool ICollection<long>.Contains(long item){
            if(!this.Probed){
                this.Probed=true;
                onFirstContains();
            }
            return base.Contains(item);
        }
    }
    [Fact,Trait("Category","Unit")]public void IsProperSubsetOf()=>Check(static (set,other)=>set.IsProperSubsetOf(other));
    [Fact,Trait("Category","Unit")]public void IsProperSupersetOf()=>Check(static (set,other)=>set.IsProperSupersetOf(other));
    [Fact,Trait("Category","Unit")]public void IsSubsetOf()=>Check(static (set,other)=>set.IsSubsetOf(other));
    [Fact,Trait("Category","Unit")]public void IsSupersetOf()=>Check(static (set,other)=>set.IsSupersetOf(other));
    [Fact,Trait("Category","Unit")]public void Overlaps()=>Check(static (set,other)=>set.Overlaps(other));
    [Fact,Trait("Category","Unit")]public void SetEquals()=>Check(static (set,other)=>set.SetEquals(other));
}
