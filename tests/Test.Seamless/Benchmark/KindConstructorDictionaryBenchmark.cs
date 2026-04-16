using Seamless.Collections;
using Seamless.Stores;

namespace Benchmarks;

public abstract class ManagedUnmanagedDictionaryCtorBenchmark<TKey, TValue>(ITestOutputHelper output)
    :DictionaryConstructorBenchmarkBase<TKey,TValue,ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>>(output)
    where TKey : IEquatable<TKey>
    where TValue : unmanaged, IEquatable<TValue> {
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    protected override string BenchmarkName => "ManagedUnmanagedDictionary";

    protected override ConstructorBenchmarkHandle<ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>> CreateHandle() {
        var store = Factory.Create(IndexSize: this.IndexSize,MemorySize: MemorySize)
            .Create<TKey,TValue>(new ManagedTag(),new UnmanagedTag());
        var dict = new ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>>(store);
        return new(store,dict);
    }

    protected override void AddItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key,TValue value) => dict.Add(key,value);
    protected override bool TryGetItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key,out TValue value) {
        var found = dict.TryGetValue(key,out var actual);
        value=actual;
        return found;
    }
    protected override bool ContainsItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key) => dict.ContainsKey(key);
    protected override bool TryAddItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key,TValue value) => dict.TryAdd(key,value)==TryAddResult.Success;
    protected override void SetItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key,TValue value) => dict[key]=value;
    protected override bool RemoveItem(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict,TKey key) => dict.Remove(key);
    protected override long GetItemCount(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict) => dict.GetLongCount();
    protected override void ClearItems(ManagedUnmanagedDictionary<TKey,TValue,ManagedSerializer<TKey>,UnmanagedSerializer<TValue>> dict) => dict.Clear();
}

public abstract class UnmanagedManagedDictionaryCtorBenchmark<TKey, TValue>(ITestOutputHelper output)
    :DictionaryConstructorBenchmarkBase<TKey,TValue,UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>>(output)
    where TKey : unmanaged, IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    protected override string BenchmarkName => "UnmanagedManagedDictionary";

    protected override ConstructorBenchmarkHandle<UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>> CreateHandle() {
        var store = Factory.Create(IndexSize: this.IndexSize,MemorySize: MemorySize)
            .Create<TKey,TValue>(new UnmanagedTag(),new ManagedTag());
        var dict = new UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>>(store);
        return new(store,dict);
    }

    protected override void AddItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict.Add(key,value);
    protected override bool TryGetItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,out TValue value) {
        var found = dict.TryGetValue(key,out var actual);
        value=actual!;
        return found;
    }
    protected override bool ContainsItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key) => dict.ContainsKey(key);
    protected override bool TryAddItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict.TryAdd(key,value)==TryAddResult.Success;
    protected override void SetItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict[key]=value;
    protected override bool RemoveItem(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key) => dict.Remove(key);
    protected override long GetItemCount(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict) => dict.GetLongCount();
    protected override void ClearItems(UnmanagedManagedDictionary<TKey,TValue,UnmanagedSerializer<TKey>,ManagedSerializer<TValue>> dict) => dict.Clear();
}

[Trait("TKey","int")]
[Trait("TValue","int")]
public sealed class ManagedUnmanagedDictionaryCtor_Int_Int(ITestOutputHelper output)
    :ManagedUnmanagedDictionaryCtorBenchmark<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
}

[Trait("TKey","string")]
[Trait("TValue","int")]
public sealed class ManagedUnmanagedDictionaryCtor_String_Int(ITestOutputHelper output)
    :ManagedUnmanagedDictionaryCtorBenchmark<string,int>(output) {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override int CreateValue(int seed) => seed;
}

[Trait("TKey","int")]
[Trait("TValue","int")]
public sealed class UnmanagedManagedDictionaryCtor_Int_Int(ITestOutputHelper output)
    :UnmanagedManagedDictionaryCtorBenchmark<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
}

[Trait("TKey","int")]
[Trait("TValue","string")]
public sealed class UnmanagedManagedDictionaryCtor_Int_String(ITestOutputHelper output)
    :UnmanagedManagedDictionaryCtorBenchmark<int,string>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override string CreateValue(int seed) => seed.ToString();
}

public abstract class ManagedManagedDictionaryCtorBenchmark<TKey, TValue>(ITestOutputHelper output)
    :DictionaryConstructorBenchmarkBase<TKey,TValue,ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>>(output)
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    private const long MemorySize = Seamless.Const.LogMemorySize<<11;

    protected override string BenchmarkName => "ManagedManagedDictionary";

    protected override ConstructorBenchmarkHandle<ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>> CreateHandle() {
        var store = Factory.Create(IndexSize: this.IndexSize,MemorySize: MemorySize)
            .Create<TKey,TValue>(new ManagedTag(),new ManagedTag());
        var dict = new ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>>(store);
        return new(store,dict);
    }

    protected override void AddItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict.Add(key,value);
    protected override bool TryGetItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,out TValue value) {
        var found = dict.TryGetValue(key,out var actual);
        value=actual!;
        return found;
    }
    protected override bool ContainsItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key) => dict.ContainsKey(key);
    protected override bool TryAddItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict.TryAdd(key,value)==TryAddResult.Success;
    protected override void SetItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key,TValue value) => dict[key]=value;
    protected override bool RemoveItem(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict,TKey key) => dict.Remove(key);
    protected override long GetItemCount(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict) => dict.GetLongCount();
    protected override void ClearItems(ManagedManagedDictionary<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> dict) => dict.Clear();
}

[Trait("TKey","int")]
[Trait("TValue","int")]
public sealed class ManagedManagedDictionaryCtor_Int_Int(ITestOutputHelper output)
    :ManagedManagedDictionaryCtorBenchmark<int,int>(output) {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
}

[Trait("TKey","string")]
[Trait("TValue","string")]
public sealed class ManagedManagedDictionaryCtor_String_String(ITestOutputHelper output)
    :ManagedManagedDictionaryCtorBenchmark<string,string>(output) {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override string CreateValue(int seed) => seed.ToString();
}