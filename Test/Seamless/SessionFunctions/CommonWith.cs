using Seamless.Tsavorite.Collections;

namespace Test.Seamless.SessionFunctions;

public abstract class CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>,IDisposable {
    protected abstract TKey CreateKey(int seed);
    protected abstract (TSet Set,IDisposable Store) CreateSet();
    protected abstract void With(TSet set,TSet other);
    protected abstract void ExpectedWith(global::System.Collections.Generic.HashSet<TKey> set,global::System.Collections.Generic.HashSet<TKey> other);

    protected void RunWithAB(int iterations = 10) {
        var (setA,storeA)=CreateSet();
        var (setB,storeB)=CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        var hashSetA = new global::System.Collections.Generic.HashSet<TKey>();
        var hashSetB = new global::System.Collections.Generic.HashSet<TKey>();
        for(var b = 0; b<iterations; b++) {
            ResetWithRange(hashSetA,setA,0);
            ResetWithRange(hashSetB,setB,b);
            With(setA,setB);
            ExpectedWith(hashSetA,hashSetB);
            Assert.Equal(hashSetA.Count,setA.Count);
            Assert.Equal(hashSetB.Count,setB.Count);
        }
    }

    protected void RunWithAA(int iterations = 8) {
        var (setA,storeA)=CreateSet();
        using var _storeA = storeA;
        using var _setA = setA;
        var hashSetA = new global::System.Collections.Generic.HashSet<TKey>();
        for(var b = 0; b<iterations; b++) {
            ResetWithRange(hashSetA,setA,b);
            With(setA,setA);
            ExpectedWith(hashSetA,hashSetA);
            Assert.Equal(hashSetA.Count,setA.Count);
        }
    }

    private void ResetWithRange(global::System.Collections.Generic.HashSet<TKey> hashSet,TSet set,int offset) {
        hashSet.Clear();
        set.Clear();
        for(var a = 0; a<8; a++) {
            var key = CreateKey(a+offset);
            hashSet.Add(key);
            set.Add(key);
        }
    }
}
