using System.Collections;
using Seamless.Collections;
using Seamless.Collections.Enumerators;
using Seamless.ScanIteratorFunctions;
using Seamless.SessionFunctions;
using Tsavorite.core;
namespace Test.Seamless.Collections.Enumerators;


public class KeyEnumerable {
    private struct LifeCycle:IDisposable{
        private readonly global::Seamless.Collections.HashSetStore<int> store;
        public readonly global::Seamless.Collections.HashSet<int> set;
        private int count=0;
        public LifeCycle(){
            this.store = new global::Seamless.Collections.HashSetStore<int>();
            this.set = this.store.Create();
            this.set.Add(1);
            this.set.Add(2);
        }
        public void GetEnumerable<T>()where T: IEnumerable{
            var Enumerable=(T)(object)this.set;
            var Enumerator=Enumerable.GetEnumerator();
            while(Enumerator.MoveNext()){}
        }
        public void GetEnumerator<T>()where T: IEnumerator{
            var Enumerator=(T)(object)this.set.GetEnumerator();
            while(Enumerator.MoveNext()){}
        }
        public void Dispose(){
            Assert.Equal(2,this.set.Count);
            this.set.Dispose();
            this.store.Dispose();
        }
    }
    private static void CommonGetEnumerator<T>(T Enumerator)where T: IEnumerator{
        while(Enumerator.MoveNext()){}
    }
    [Fact, Trait("Category","Coverage")]public void GetEnumerator() {
        using var store = new HashSetStore<int>();
        using var set = store.Create();
        set.Add(1);
        set.Add(2);
        var count = 0;
        foreach(var _ in set) count++;
        Assert.Equal(2,count);
    }
}
