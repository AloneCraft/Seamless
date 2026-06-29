using Seamless.Collections;

namespace Persisted.Collection;

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

    private static string CreatePath(string tag){
        var directoryPath=TestPath.Directory<Dictionary>(tag);
        return Path.Combine(directoryPath,"items.dat");
    }
}
