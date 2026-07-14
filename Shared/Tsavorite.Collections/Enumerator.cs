#if BENCHMARK_PROJECT
namespace Benchmark.Tsavorite.Collections;
#else
namespace Tsavorite.Collections;
#endif


public class Enumerator {
    private static string CreateDiskFolder() =>
        TestPath.Directory<Enumerator>();

    // Enumerator.MoveNext: コレクション 空 false
    [BenchFact, Benchmark]
    public void MoveNext_EmptyCollection() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        using var e = dict.GetEnumerator();
        Assert.False(e.MoveNext());
    }
    // Enumerator.MoveNextの動作を検証する。
    [BenchFact, Benchmark]
    public void MoveNext_WithinChunk_TwoItems() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var count = 0;
        foreach(var _ in dict) count++;
        Assert.Equal(2,count);
    }
    // Enumerator.MoveNextの動作を検証する。
    [BenchFact, Benchmark]
    public void MoveNext_MultiChunk_ThreeItems() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Add(3,"c");
        var count = 0;
        foreach(var _ in dict) count++;
        Assert.Equal(3,count);
    }
    [BenchFact, Benchmark]
    public void MoveNext_HashSet_DoesNotReturnRemovedOrDuplicateKeys() {
        using var store = Store.Create().Create<int>();
        using var set = store.Create();
        for(var i = 0;i<32;i++)
            set.Add(i);
        for(var i = 0;i<32;i+=2)
            Assert.True(set.Remove(i));
        for(var i = 0;i<32;i+=4)
            Assert.True(set.Add(i));

        var expected = Enumerable.Range(0,32).Where(static i => (i&1)!=0||(i&3)==0).ToHashSet();
        var actual = new global::System.Collections.Generic.HashSet<int>();
        foreach(var value in set)
            Assert.True(actual.Add(value),$"Duplicate key {value} was returned.");

        Assert.Equal(expected.Count,set.Count);
        Assert.Equal(expected.Count,actual.Count);
        Assert.All(expected,value => Assert.Contains(value,actual));
        Assert.True(!actual.Contains(2));
    }
    [BenchFact, Benchmark]
    public void MoveNext_Dictionary_KeysAndValuesDoNotReturnRemovedOrDuplicateEntries() {
        using var store = Store.Create().Create<int,int>();
        using var dict = store.Create();
        for(var i = 0;i<32;i++)
            dict.Add(i,i*10);
        for(var i = 0;i<32;i+=2)
            Assert.True(dict.Remove(i,out _));
        for(var i = 0;i<32;i+=4)
            dict.Add(i,i*100);

        var expectedKeys = Enumerable.Range(0,32).Where(static i => (i&1)!=0||(i&3)==0).ToHashSet();
        var actualKeys = new global::System.Collections.Generic.HashSet<int>();
        foreach(var key in dict.Keys)
            Assert.True(actualKeys.Add(key),$"Duplicate key {key} was returned.");

        var actualValues = new global::System.Collections.Generic.HashSet<int>();
        foreach(var value in dict.Values)
            Assert.True(actualValues.Add(value),$"Duplicate value {value} was returned.");

        Assert.Equal(expectedKeys.Count,dict.Count);
        Assert.Equal(expectedKeys.Count,actualKeys.Count);
        Assert.All(expectedKeys,key => Assert.Contains(key,actualKeys));
        Assert.True(!actualKeys.Contains(2));
        Assert.Contains(0,actualValues);
        Assert.Contains(400,actualValues);
        Assert.True(!actualValues.Contains(20));
    }
    // Enumerator.Current の値確誁E    [BenchFact,Benchmark]
    public void Current_ValueIsCorrect() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(42,"hello");
        using var e = dict.GetEnumerator();
        Assert.True(e.MoveNext());
        var kvp = e.Current;
        Assert.Equal(42,kvp.Key);
        Assert.Equal("hello",kvp.Value);
    }
    // この分岐の動作を検証する。
    [BenchFact, Benchmark]
    public void Current_IEnumerator_Object() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(7,"x");
        using var e = dict.GetEnumerator();
        Assert.True(e.MoveNext());
        global::System.Collections.IEnumerator nonGeneric = e;
        Assert.NotNull(nonGeneric.Current);
    }
    // Enumerator.Resetの動作を検証する。
    [BenchFact, Benchmark]
    public void Reset_ReenumeratesFromStart() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        using var e = dict.GetEnumerator();
        Assert.True(e.MoveNext());
        e.Reset();
        var count = 0;
        while(e.MoveNext()) count++;
        Assert.Equal(2,count);
    }
    // Enumerator.Disposeの動作を検証する。
    [BenchFact, Benchmark]
    public void Dispose_IsSafe() {
        using var store = Store.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var e = dict.GetEnumerator();
        e.MoveNext();
        e.Dispose();
    }
    // HashSet における KeyEnumerable struct GetEnumerator() パス
    [BenchFact, Benchmark]
    public void MoveNext_HashSet_MultiChunk_ThreeItems() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.LogMemorySize*4,CheckpointDir: folder).Create<int>(logDevice,objDevice);
            using var set = store.Create();

            const int Count = 625;
            var added = 0;
            for(var i = 0;i<Count;i++) {
                if(set.Add(i))
                    added++;
            }
            Assert.Equal(Count,added);

            for(var i = 0;i<Count;i++)
                Assert.True(set.Contains(i));

            var enumerated = new global::System.Collections.Generic.HashSet<int>();
            using var e = set.GetEnumerator();
            var steps = 0;
            while(e.MoveNext()) {
                steps++;
                if(steps>Count*2)
                    throw new InvalidOperationException($"Enumerator did not finish within expected steps. steps={steps}, unique={enumerated.Count}");
                enumerated.Add(e.Current);
            }
            Assert.Equal(Count,enumerated.Count);

            store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        } finally {
            try {
                Directory.Delete(folder,true);
            } catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            } catch {
            }
        }
    }

    [BenchFact, Benchmark]
    public void HashSet_AfterFlushAndEvict_Unmanaged_PublicEnumerationAndCopyToEnumerateAll() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var store = Store.Create().Create<int>(logDevice,objDevice);
            using var set = store.Create();
            var expected = Enumerable.Range(0,256).ToArray();
            foreach(var value in expected)
                set.Add(value);
            var count0 = set.Count;
            // LightEpoch の取得と解放を検証する。

            var count1 = set.Count;
            var enumerated = new global::System.Collections.Generic.HashSet<int>();
            foreach(var value in set)
                enumerated.Add(value);
            Assert.Equal(expected.Length,enumerated.Count);
            Assert.All(expected,value => Assert.Contains(value,enumerated));

            var copied = new int[expected.Length];
            set.CopyTo(copied,0);
            var copiedSet = copied.ToHashSet();
            Assert.Equal(expected.Length,copiedSet.Count);
            Assert.All(expected,value => Assert.Contains(value,copiedSet));

            var toArray = set.ToArray();
            var toArraySet = toArray.ToHashSet();
            Assert.Equal(expected.Length,toArraySet.Count);
            Assert.All(expected,value => Assert.Contains(value,toArraySet));

            store.Store.Log.FlushAndEvict(wait: true);
        } finally {
            try {
                Directory.Delete(folder,true);
            } catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            } catch {
            }
        }
    }

    [BenchFact, Benchmark]
    public void HashSet_AfterFlushAndEvict_DoesNotReturnRemovedOrDuplicateKeys() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var store = Store.Create(MemorySize: global::Seamless.Tsavorite.Const.MinObjectLogCapacity,MutableFraction: 0).Create<int>(logDevice,objDevice,new BlittableStrategy());
            using var set = store.Create();
            for(var i = 0;i<128;i++)
                set.Add(i);
            for(var i = 0;i<128;i+=2)
                Assert.True(set.Remove(i));
            for(var i = 0;i<128;i+=4)
                Assert.True(set.Add(i));
            store.Store.Log.FlushAndEvict(wait: true);

            var expected = Enumerable.Range(0,128).Where(static i => (i&1)!=0||(i&3)==0).ToHashSet();
            var actual = new global::System.Collections.Generic.HashSet<int>();
            foreach(var value in set)
                Assert.True(actual.Add(value),$"Duplicate key {value} was returned.");

            Assert.Equal(expected.Count,set.Count);
            Assert.Equal(expected.Count,actual.Count);
            Assert.All(expected,value => Assert.Contains(value,actual));
            Assert.True(!actual.Contains(2));
        } finally {
            try {
                Directory.Delete(folder,true);
            } catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            } catch {
            }
        }
    }

    [BenchFact, Benchmark]
    public void HashSet_AfterFlushAndEvict_Managed_PublicEnumerationAndCopyToEnumerateAll() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
            using var store = Store.Create().Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy());
            using var set = store.Create();
            var expected = Enumerable.Range(0,2048).Select(i => "s"+i).ToArray();
            foreach(var value in expected)
                set.Add(value);
            store.Store.Log.FlushAndEvict(wait: true);

            var enumerated = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach(var value in set)
                enumerated.Add(value);
            Assert.Equal(expected.Length,enumerated.Count);
            Assert.All(expected,value => Assert.Contains(value,enumerated));

            var copied = new string[expected.Length];
            set.CopyTo(copied,0);
            var copiedSet = copied.Where(value => value is not null).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(expected.Length,copiedSet.Count);
            Assert.All(expected,value => Assert.Contains(value,copiedSet));

            var toArray = set.ToArray();
            var toArraySet = toArray.ToHashSet(StringComparer.Ordinal);
            Assert.Equal(expected.Length,toArraySet.Count);
            Assert.All(expected,value => Assert.Contains(value,toArraySet));
            store.Store.Log.FlushAndEvict(wait: true);
        } finally {
            try {
                Directory.Delete(folder,true);
            } catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            } catch {
            }
        }
    }
    [BenchFact, Benchmark]
    public void HashSetN() {
        var folder = CreateDiskFolder();
        try {
            for(var Count = 2;Count<=64;Count++) {
                using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
                using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: global::Seamless.Tsavorite.Const.MinObjectLogCapacity);
                using var store = Store.Create(MutableFraction: 0).Create<string>(logDevice,objDevice);
                using var set = store.Create();
                var expected = Enumerable.Range(0,Count).Select(i => "s"+i).ToArray();
                foreach(var value in expected)
                    set.Add(value);
                store.Store.Log.FlushAndEvict(wait: true);
                //global::Seamless.Tsavorite.Const.Chunk_Length
                var enumerated = new global::System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                var e = set.GetEnumerator();
                while(e.MoveNext())
                    enumerated.Add(e.Current);
                Assert.Equal(expected.Length,enumerated.Count);
                Assert.All(expected,value => Assert.Contains(value,enumerated));

                var copied = new string[expected.Length];
                set.CopyTo(copied,0);
                var copiedSet = copied.Where(value => value is not null).ToHashSet(StringComparer.Ordinal);
                Assert.Equal(expected.Length,copiedSet.Count);
                Assert.All(expected,value => Assert.Contains(value,copiedSet));

                var toArray = set.ToArray();
                var toArraySet = toArray.ToHashSet(StringComparer.Ordinal);
                Assert.Equal(expected.Length,toArraySet.Count);
                Assert.All(expected,value => Assert.Contains(value,toArraySet));
                store.Store.Log.FlushAndEvict(wait: true);
            }
        } finally {
            try {
                Directory.Delete(folder,true);
            } catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            } catch {
            }
        }
    }
}
