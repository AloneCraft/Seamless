using System.Linq;

using Tsavorite;
using Tsavorite.Stores;
namespace Test.Tsavorite.Collections;


public class Enumerator {
    private static string CreateDiskFolder() =>
        TestPath.Directory<Enumerator>();

    // Enumerator.MoveNext: コレクション 空 false
    [Fact, Trait("Category","Unit")]
    public void MoveNext_EmptyCollection() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        using var e = dict.GetEnumerator();
        Assert.False(e.MoveNext());
    }
    // Enumerator.MoveNext: Chunk_LENGTH=2 アイテム チャンクパス (Index < Count)
    [Fact, Trait("Category","Unit")]
    public void MoveNext_WithinChunk_TwoItems() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        var count = 0;
        foreach(var _ in dict) count++;
        Assert.Equal(2,count);
    }
    // Enumerator.MoveNext: 3アイテムで複数チャンク (1チャンク目2件+2チャンク目1件)
    [Fact, Trait("Category","Unit")]
    public void MoveNext_MultiChunk_ThreeItems() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        dict.Add(2,"b");
        dict.Add(3,"c");
        var count = 0;
        foreach(var _ in dict) count++;
        Assert.Equal(3,count);
    }
    // Enumerator.Current の値確認
    [Fact, Trait("Category","Unit")]
    public void Current_ValueIsCorrect() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(42,"hello");
        using var e = dict.GetEnumerator();
        Assert.True(e.MoveNext());
        var kvp = e.Current;
        Assert.Equal(42,kvp.Key);
        Assert.Equal("hello",kvp.Value);
    }
    // IEnumerator.Current (object 型)
    [Fact, Trait("Category","Unit")]
    public void Current_IEnumerator_Object() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(7,"x");
        using var e = dict.GetEnumerator();
        Assert.True(e.MoveNext());
        IEnumerator nonGeneric = e;
        Assert.NotNull(nonGeneric.Current);
    }
    // Enumerator.Reset で再列挙可能
    [Fact, Trait("Category","Unit")]
    public void Reset_ReenumeratesFromStart() {
        using var store = HashStore.Create().Create<int,string>();
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
    // Enumerator.Dispose は安全
    [Fact, Trait("Category","Unit")]
    public void Dispose_IsSafe() {
        using var store = HashStore.Create().Create<int,string>();
        using var dict = store.Create();
        dict.Add(1,"a");
        var e = dict.GetEnumerator();
        e.MoveNext();
        e.Dispose();
    }
    // HashSet における KeyEnumerable struct GetEnumerator() パス
    [Fact, Trait("Category","Unit")]
    public void MoveNext_HashSet_MultiChunk_ThreeItems() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = HashStore.Create(MemorySize: Const.LogMemorySize*4,CheckpointDir: folder).Create<int>(logDevice,objDevice);
            using var set = store.Create();

            const int Count = 625;
            var added = 0;
            for(var i = 0; i<Count; i++) {
                if(set.Add(i))
                    added++;
            }
            Assert.Equal(Count,added);

            for(var i = 0; i<Count; i++)
                Assert.True(set.Contains(i));

            var enumerated = new HashSet<int>();
            using var e = set.GetEnumerator();
            var steps = 0;
            while(e.MoveNext()) {
                steps++;
                if(steps>Count*2)
                    throw new Xunit.Sdk.XunitException($"Enumerator did not finish within expected steps. steps={steps}, unique={enumerated.Count}");
                enumerated.Add(e.Current);
            }
            Assert.Equal(Count,enumerated.Count);

            store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);
        }
        finally {
            try {
                Directory.Delete(folder,true);
            }
            catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            }
            catch {
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void HashSet_AfterFlushAndEvict_Unmanaged_PublicEnumerationAndCopyToEnumerateAll() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = HashStore.Create().Create<int>(logDevice,objDevice);
            using var set = store.Create();
            var expected = Enumerable.Range(0,256).ToArray();
            foreach(var value in expected)
                set.Add(value);
            var count0 = set.Count;
            //Tsavorite 1.1.3 の BufferAndLoad が epoch.Resume() を保持したまま BumpCurrentEpoch を呼ぶ epoch デッドロックバグがある
            // FlushAndEvict は HeadAddress=TailAddress にするため IterateLookup でスキャン不可。
            // ShiftReadOnlyAddress でディスクに書き出しつつ HeadAddress は変えない。
            store.Store.Log.ShiftReadOnlyAddress(store.Store.Log.TailAddress,wait: true);

            var count1 = set.Count;
            var enumerated = new HashSet<int>();
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
        }
        finally {
            try {
                Directory.Delete(folder,true);
            }
            catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            }
            catch {
            }
        }
    }

    [Fact, Trait("Category","Unit")]
    public void HashSet_AfterFlushAndEvict_Managed_PublicEnumerationAndCopyToEnumerateAll() {
        var folder = CreateDiskFolder();
        try {
            using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
            using var store = HashStore.Create().Create<string>(logDevice,objDevice,new MemoryPackSerializerStrategy());
            using var set = store.Create();
            var expected = Enumerable.Range(0,2048).Select(i => "s"+i).ToArray();
            foreach(var value in expected)
                set.Add(value);

#if TSAVORITE2_0BETA3
            store.Store.Log.FlushAndEvict(wait:true);
#endif

            var enumerated = new HashSet<string>(StringComparer.Ordinal);
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

#if TSAVORITE2_0BETA3
            store.Store.Log.FlushAndEvict(wait:true);
#endif
        }
        finally {
            try {
                Directory.Delete(folder,true);
            }
            catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            }
            catch {
            }
        }
    }
    [Fact, Trait("Category","Unit")]
    public void HashSetN() {
        var folder = CreateDiskFolder();
        try {
            for(var Count = 2; Count<=64; Count++) {
                using var logDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder,"log.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
                using var objDevice = global::Tsavorite.core.Devices.CreateLogDevice(Path.Combine(folder+"_obj","obj.data"),deleteOnClose: true,capacity: Const.MinObjectLogCapacity);
                using var store = HashStore.Create(MutableFraction: 0).Create<string>(logDevice,objDevice);
                using var set = store.Create();
                var expected = Enumerable.Range(0,Count).Select(i => "s"+i).ToArray();
                foreach(var value in expected)
                    set.Add(value);
                store.Store.Log.FlushAndEvict(wait: true);
                //Const.Chunk_Length
                var enumerated = new HashSet<string>(StringComparer.Ordinal);
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
        }
        finally {
            try {
                Directory.Delete(folder,true);
            }
            catch {
            }
            try {
                Directory.Delete(folder+"_obj",true);
            }
            catch {
            }
        }
    }
}
