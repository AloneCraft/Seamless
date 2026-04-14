using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

/// <summary>
/// .NET 10 の <see cref="System.Collections.Generic.HashSet{T}"/> をオラクルとするモデルベーステスト。
/// ランダム操作列を両方に適用し、常に同じ状態であることを検証する。
/// </summary>
public class ModelBasedHashSetTests{
    private enum Op{Add,Remove,Contains,Clear,Count,ExceptWith,IntersectWith,UnionWith,SymmetricExceptWith}
    private static readonly Op[] Ops=Enum.GetValues<Op>();

    [Fact,Trait("Category","Unit")]
    public void ManagedHashSet_ランダム操作列がNET10HashSetと一致する(){
        using var store=Factory.Create().Create<int>();
        using var subject=store.Create();
        var oracle=new HashSet<int>();
        RunRandomOps(subject,oracle,seed:42,opCount:500);
    }

    [Fact,Trait("Category","Unit")]
    public void UnmanagedHashSet_ランダム操作列がNET10HashSetと一致する(){
        using var store=Factory.Create().CreateUnmanagedHashSet<int>();
        using var subject=store.Create();
        var oracle=new HashSet<int>();
        RunRandomOpsUnmanaged(subject,oracle,seed:123,opCount:500);
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void ManagedHashSet_複数シードでも一致する(int seed){
        using var store=Factory.Create().Create<int>();
        using var subject=store.Create();
        var oracle=new HashSet<int>();
        RunRandomOps(subject,oracle,seed,opCount:300);
    }

    private static void RunRandomOps(ManagedHashSet<int> subject,HashSet<int> oracle,int seed,int opCount){
        var rng=new Random(seed);
        using var otherStore=Factory.Create().Create<int>();
        using var other=otherStore.Create();
        var otherOracle=new HashSet<int>();

        for(var i=0; i<opCount; i++){
            var op=Ops[rng.Next(Ops.Length)];
            var value=rng.Next(100);

            switch(op){
                case Op.Add:
                    Assert.Equal(oracle.Add(value),subject.Add(value));
                    break;
                case Op.Remove:
                    Assert.Equal(oracle.Remove(value),subject.Remove(value));
                    break;
                case Op.Contains:
                    Assert.Equal(oracle.Contains(value),subject.Contains(value));
                    break;
                case Op.Clear:
                    oracle.Clear();
                    subject.Clear();
                    break;
                case Op.Count:
                    Assert.Equal(oracle.Count,subject.Count);
                    break;
                case Op.ExceptWith:
                    SyncOther(other,otherOracle,rng);
                    oracle.ExceptWith(otherOracle);
                    subject.ExceptWith(other);
                    break;
                case Op.IntersectWith:
                    SyncOther(other,otherOracle,rng);
                    oracle.IntersectWith(otherOracle);
                    subject.IntersectWith(other);
                    break;
                case Op.UnionWith:
                    SyncOther(other,otherOracle,rng);
                    oracle.UnionWith(otherOracle);
                    subject.UnionWith(other);
                    break;
                case Op.SymmetricExceptWith:
                    SyncOther(other,otherOracle,rng);
                    oracle.SymmetricExceptWith(otherOracle);
                    subject.SymmetricExceptWith(other);
                    break;
            }

            Assert.Equal(oracle.Count,subject.Count);
        }

        // 最終状態の全要素一致を検証
        AssertSetEquals(oracle,subject);
    }

    private static void RunRandomOpsUnmanaged(UnmanagedHashSet<int> subject,HashSet<int> oracle,int seed,int opCount){
        var rng=new Random(seed);
        using var otherStore=Factory.Create().CreateUnmanagedHashSet<int>();
        using var other=otherStore.Create();
        var otherOracle=new HashSet<int>();

        for(var i=0; i<opCount; i++){
            var op=Ops[rng.Next(Ops.Length)];
            var value=rng.Next(100);

            switch(op){
                case Op.Add:
                    Assert.Equal(oracle.Add(value),subject.Add(value));
                    break;
                case Op.Remove:
                    Assert.Equal(oracle.Remove(value),subject.Remove(value));
                    break;
                case Op.Contains:
                    Assert.Equal(oracle.Contains(value),subject.Contains(value));
                    break;
                case Op.Clear:
                    oracle.Clear();
                    subject.Clear();
                    break;
                case Op.Count:
                    Assert.Equal(oracle.Count,subject.Count);
                    break;
                case Op.ExceptWith:
                    SyncOtherUnmanaged(other,otherOracle,rng);
                    oracle.ExceptWith(otherOracle);
                    subject.ExceptWith(other);
                    break;
                case Op.IntersectWith:
                    SyncOtherUnmanaged(other,otherOracle,rng);
                    oracle.IntersectWith(otherOracle);
                    subject.IntersectWith(other);
                    break;
                case Op.UnionWith:
                    SyncOtherUnmanaged(other,otherOracle,rng);
                    oracle.UnionWith(otherOracle);
                    subject.UnionWith(other);
                    break;
                case Op.SymmetricExceptWith:
                    SyncOtherUnmanaged(other,otherOracle,rng);
                    oracle.SymmetricExceptWith(otherOracle);
                    subject.SymmetricExceptWith(other);
                    break;
            }

            Assert.Equal(oracle.Count,subject.Count);
        }

        AssertSetEqualsUnmanaged(oracle,subject);
    }

    private static void SyncOther(ManagedHashSet<int> other,HashSet<int> otherOracle,Random rng){
        other.Clear();
        otherOracle.Clear();
        var count=rng.Next(10);
        for(var j=0; j<count; j++){
            var v=rng.Next(100);
            other.Add(v);
            otherOracle.Add(v);
        }
    }

    private static void SyncOtherUnmanaged(UnmanagedHashSet<int> other,HashSet<int> otherOracle,Random rng){
        other.Clear();
        otherOracle.Clear();
        var count=rng.Next(10);
        for(var j=0; j<count; j++){
            var v=rng.Next(100);
            other.Add(v);
            otherOracle.Add(v);
        }
    }

    private static void AssertSetEquals(HashSet<int> oracle,ManagedHashSet<int> subject){
        foreach(var item in oracle){
            Assert.True(subject.Contains(item),$"Subject missing {item}");
        }
        foreach(var item in subject){
            Assert.True(oracle.Contains(item),$"Oracle missing {item}");
        }
    }

    private static void AssertSetEqualsUnmanaged(HashSet<int> oracle,UnmanagedHashSet<int> subject){
        foreach(var item in oracle){
            Assert.True(subject.Contains(item),$"Subject missing {item}");
        }
        foreach(var item in subject){
            Assert.True(oracle.Contains(item),$"Oracle missing {item}");
        }
    }
}
