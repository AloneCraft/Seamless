using Seamless.Collections;

namespace Test.Seamless.Unit.SessionFunctions;

public abstract class CommonWith<TKey,TSet>
    where TKey : IEquatable<TKey>
    where TSet : IHashSet<TKey>,IDisposable {
    protected abstract TKey CreateKey(int seed);
    protected abstract (TSet Set,IDisposable Store) CreateSet();
    protected abstract void With(TSet set,TSet other);
    protected abstract void ExpectedWith(global::System.Collections.Generic.HashSet<TKey> set,global::System.Collections.Generic.HashSet<TKey> other);

    protected void RunWithAB(int iterations = 10) {
        var (setA,storeA)=this.CreateSet();
        var (setB,storeB)=this.CreateSet();
        using var _storeA = storeA;
        using var _storeB = storeB;
        using var _setA = setA;
        using var _setB = setB;
        var hashSetA = new global::System.Collections.Generic.HashSet<TKey>();
        var hashSetB = new global::System.Collections.Generic.HashSet<TKey>();
        for(var b = 0; b<iterations; b++) {
            this.ResetWithRange(hashSetA,setA,0);
            this.ResetWithRange(hashSetB,setB,b);
            this.With(setA,setB);
            this.ExpectedWith(hashSetA,hashSetB);
            Assert.Equal(hashSetA.Count,setA.Count);
            Assert.Equal(hashSetB.Count,setB.Count);
        }
    }

    protected void RunWithAA(int iterations = 8) {
        var (setA,storeA)=this.CreateSet();
        using var _storeA = storeA;
        using var _setA = setA;
        var hashSetA = new global::System.Collections.Generic.HashSet<TKey>();
        for(var b = 0; b<iterations; b++) {
            this.ResetWithRange(hashSetA,setA,b);
            this.With(setA,setA);
            this.ExpectedWith(hashSetA,hashSetA);
            Assert.Equal(hashSetA.Count,setA.Count);
        }
    }

    private void ResetWithRange(global::System.Collections.Generic.HashSet<TKey> hashSet,TSet set,int offset) {
        hashSet.Clear();
        set.Clear();
        for(var a = 0; a<8; a++) {
            var key = this.CreateKey(a+offset);
            hashSet.Add(key);
            set.Add(key);
        }
    }
}
