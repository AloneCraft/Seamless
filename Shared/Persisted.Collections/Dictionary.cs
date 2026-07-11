#if BENCHMARK_PROJECT
namespace Benchmark.Persisted.Collections;
#else
namespace Persisted.Collections;
#endif

public sealed class Dictionary{
    [BenchFact,Benchmark]
    public void BlittableDictionary_AddTryGetContains_CompareBcl(){
        var path=CreatePath(nameof(BlittableDictionary_AddTryGetContains_CompareBcl));
        using var actual=new global::Seamless.Persisted.Collections.BlittableDictionary<int,int>(path,initialCapacity: 1,initialDataCapacity: 128);
        var expected=new global::System.Collections.Generic.Dictionary<int,int>();

        for(var i=0;i<32;i++){
            actual.Add(i,i*2);
            expected.Add(i,i*2);
        }

        Assert.Equal(32,actual.Count);
        Assert.True(actual.ContainsKey(7));
        Assert.True(actual.TryGetValue(7,out var value));
        Assert.Equal(14,value);
        BclCollectionComparison.DictionaryEqual(expected,actual);
    }

    [BenchFact,Benchmark]
    public void SerializedDictionary_DuplicateKeysCompact_CompareBcl(){
        var path=CreatePath(nameof(SerializedDictionary_DuplicateKeysCompact_CompareBcl));
        using var actual=new global::Seamless.Persisted.Collections.SerializedDictionary<int,ManagedStruct>(path,initialCapacity: 1,initialDataCapacity: 128);
        var expected=new global::System.Collections.Generic.Dictionary<int,ManagedStruct>();

        for(var i=0;i<16;i++){
            var first=new ManagedStruct{ Name=$"first-{i}" };
            var latest=new ManagedStruct{ Name=$"latest-{i}" };
            actual.Add(i,first);
            actual.Add(i,latest);
            expected[i]=latest;
        }

        actual.Compact();

        Assert.Equal(expected.Count,actual.Count);
        Assert.True(actual.ContainsKey(3));
        Assert.True(actual.TryGetValue(3,out var value));
        Assert.Equal(expected[3],value);
        BclCollectionComparison.DictionaryEqual(expected,actual);
    }

    [BenchFact,Benchmark]
    public void BlittableDictionary_KeysValuesAndEnumeration_CompareBcl(){
        var path=CreatePath(nameof(BlittableDictionary_KeysValuesAndEnumeration_CompareBcl));
        using var actual=new global::Seamless.Persisted.Collections.BlittableDictionary<int,int>(path,initialCapacity: 1,initialDataCapacity: 128);
        var expected=new global::System.Collections.Generic.Dictionary<int,int>();

        for(var i=0;i<24;i++){
            actual.Add(i%8,i);
            expected[i%8]=i;
        }

        BclCollectionComparison.SetEqual(expected.Keys,actual.Keys);
        BclCollectionComparison.SetEqual(expected.Values,actual.Values);
        BclCollectionComparison.DictionaryEqual(expected,actual);
    }

    [BenchFact,Benchmark]
    public void Store_CreateDictionary_UnmanagedFacade_CompareBcl(){
        var path=CreatePath(nameof(Store_CreateDictionary_UnmanagedFacade_CompareBcl));
        using var actual=global::Seamless.Persisted.Collections.Store.CreateDictionary<int,int>(path,InitialCapacity: 1,InitialDataCapacity: 128);
        var expected=new global::System.Collections.Generic.Dictionary<int,int>();

        for(var i=0;i<24;i++){
            actual.Add(i%8,i);
            expected[i%8]=i;
        }

        actual.Compact();

        Assert.Equal(expected.Count,actual.Count);
        Assert.True(actual.ContainsKey(3));
        Assert.True(actual.TryGetValue(3,out var value));
        Assert.Equal(expected[3],value);
        BclCollectionComparison.DictionaryEqual(expected,actual);
    }

    [BenchFact,Benchmark]
    public void Store_CreateDictionary_ManagedFacade_CompareBcl(){
        var path=CreatePath(nameof(Store_CreateDictionary_ManagedFacade_CompareBcl));
        using var actual=global::Seamless.Persisted.Collections.Store.CreateDictionary<int,ManagedStruct>(path,InitialCapacity: 1,InitialDataCapacity: 128);
        var expected=new global::System.Collections.Generic.Dictionary<int,ManagedStruct>();

        for(var i=0;i<16;i++){
            var value=new ManagedStruct{ Name=$"value-{i}" };
            actual.Add(i,value);
            expected[i]=value;
        }

        BclCollectionComparison.DictionaryEqual(expected,actual);
    }

    [BenchFact,Benchmark]
    public void Store_CreateTempDictionary_DeletesDirectoryOnDispose(){
        var actual=global::Seamless.Persisted.Collections.Store.CreateTempDictionary<int,int>(InitialCapacity: 1,InitialDataCapacity: 128);
        var path=actual.Path;

        actual.Add(1,2);
        actual.Dispose();

        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    private static string CreatePath(string tag){
        var directoryPath=TestPath.Directory<Dictionary>(tag);
        return Path.Combine(directoryPath,"items.dat");
    }
}
