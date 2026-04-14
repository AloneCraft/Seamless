using Seamless.Collections;
using Seamless.Stores;
namespace Test.Seamless.Collections;

/// <summary>
/// .NET 10 の <see cref="Dictionary{TKey,TValue}"/> をオラクルとするモデルベーステスト。
/// ランダム操作列を両方に適用し、常に同じ状態であることを検証する。
/// </summary>
public class ModelBasedDictionaryTests {
    private enum Op { Add, TryAdd, Remove, RemoveKvp, ContainsKey, TryGetValue, Indexer_Get, Indexer_Set, Clear, Count, Keys, Values }
    private static readonly Op[] Ops=Enum.GetValues<Op>();

    [Fact, Trait("Category","Unit")]
    public void ManagedDictionary_ランダム操作列がNET10Dictionaryと一致する(){
        using var store=Factory.Create().Create<int,string>();
        using var subject=store.Create();
        var oracle=new Dictionary<int,string>();
        RunRandomOps(subject,oracle,seed: 42,opCount: 500);
    }

    [Fact, Trait("Category","Unit")]
    public void UnmanagedDictionary_ランダム操作列がNET10Dictionaryと一致する(){
        using var store=Factory.Create().Create<int,int>();
        using var subject=store.Create();
        var oracle=new Dictionary<int,int>();
        RunRandomOpsUnmanaged(subject,oracle,seed: 42,opCount: 500);
    }

    [Theory, Trait("Category","Unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(99)]
    public void ManagedDictionary_複数シードでも一致する(int seed){
        using var store=Factory.Create().Create<int,string>();
        using var subject=store.Create();
        var oracle=new Dictionary<int,string>();
        RunRandomOps(subject,oracle,seed,opCount: 300);
    }

    private static void RunRandomOps(ISeamlessDictionary<int,string> subject,Dictionary<int,string> oracle,int seed,int opCount){
        var rng=new Random(seed);

        for(var i=0; i<opCount; i++){
            var op=Ops[rng.Next(Ops.Length)];
            var key=rng.Next(50);
            var value=$"v{rng.Next(100)}";

            switch(op){
                case Op.Add:
                    if(oracle.ContainsKey(key)){
                        Assert.Throws<ArgumentException>(() => subject.Add(key,value));
                    }
                    else {
                        oracle.Add(key,value);
                        subject.Add(key,value);
                    }
                    break;
                case Op.TryAdd:
                    var oracleAdded=oracle.TryAdd(key,value);
                    var subjectResult=subject.TryAdd(key,value);
                    Assert.Equal(oracleAdded,subjectResult==TryAddResult.Success);
                    break;
                case Op.Remove:
                    Assert.Equal(oracle.Remove(key),subject.Remove(key));
                    break;
                case Op.RemoveKvp:
                    if(oracle.TryGetValue(key,out var existing)){
                        var kvp=new KeyValuePair<int,string>(key,existing);
                        var oracleRemoved=((ICollection<KeyValuePair<int,string>>)oracle).Remove(kvp);
                        var subjectRemoved=((ICollection<KeyValuePair<int,string>>)subject).Remove(kvp);
                        Assert.Equal(oracleRemoved,subjectRemoved);
                    }
                    break;
                case Op.ContainsKey:
                    Assert.Equal(oracle.ContainsKey(key),subject.ContainsKey(key));
                    break;
                case Op.TryGetValue:
                    var oracleTgv=oracle.TryGetValue(key,out var oracleVal);
                    var subjectTgv=subject.TryGetValue(key,out var subjectVal);
                    Assert.Equal(oracleTgv,subjectTgv);
                    if(oracleTgv) Assert.Equal(oracleVal,subjectVal);
                    break;
                case Op.Indexer_Get:
                    if(oracle.ContainsKey(key)){
                        Assert.Equal(oracle[key],subject[key]);
                    }
                    else {
                        Assert.Throws<KeyNotFoundException>(() => subject[key]);
                    }
                    break;
                case Op.Indexer_Set:
                    oracle[key]=value;
                    subject[key]=value;
                    break;
                case Op.Clear:
                    oracle.Clear();
                    subject.Clear();
                    break;
                case Op.Count:
                    Assert.Equal(oracle.Count,subject.Count);
                    break;
                case Op.Keys:
                    var oracleKeys=oracle.Keys.OrderBy(k => k).ToList();
                    var subjectKeys=subject.Keys.OrderBy(k => k).ToList();
                    Assert.Equal(oracleKeys,subjectKeys);
                    break;
                case Op.Values:
                    var oracleVals=oracle.Values.OrderBy(v => v).ToList();
                    var subjectVals=subject.Values.OrderBy(v => v).ToList();
                    Assert.Equal(oracleVals,subjectVals);
                    break;
            }

            Assert.Equal(oracle.Count,subject.Count);
        }

        // 最終状態の全要素一致を検証
        AssertDictEquals(oracle,subject);
    }

    private static void RunRandomOpsUnmanaged(ISeamlessDictionary<int,int> subject,Dictionary<int,int> oracle,int seed,int opCount){
        var rng=new Random(seed);

        for(var i=0; i<opCount; i++){
            var op=Ops[rng.Next(Ops.Length)];
            var key=rng.Next(50);
            var value=rng.Next(1000);

            switch(op){
                case Op.Add:
                    if(oracle.ContainsKey(key)){
                        Assert.Throws<ArgumentException>(() => subject.Add(key,value));
                    }
                    else {
                        oracle.Add(key,value);
                        subject.Add(key,value);
                    }
                    break;
                case Op.TryAdd:
                    var oracleAdded=oracle.TryAdd(key,value);
                    var subjectResult=subject.TryAdd(key,value);
                    Assert.Equal(oracleAdded,subjectResult==TryAddResult.Success);
                    break;
                case Op.Remove:
                    Assert.Equal(oracle.Remove(key),subject.Remove(key));
                    break;
                case Op.RemoveKvp:
                    if(oracle.TryGetValue(key,out var existing)){
                        var kvp=new KeyValuePair<int,int>(key,existing);
                        var oracleRemoved=((ICollection<KeyValuePair<int,int>>)oracle).Remove(kvp);
                        var subjectRemoved=((ICollection<KeyValuePair<int,int>>)subject).Remove(kvp);
                        Assert.Equal(oracleRemoved,subjectRemoved);
                    }
                    break;
                case Op.ContainsKey:
                    Assert.Equal(oracle.ContainsKey(key),subject.ContainsKey(key));
                    break;
                case Op.TryGetValue:
                    var oracleTgv=oracle.TryGetValue(key,out var oracleVal);
                    var subjectTgv=subject.TryGetValue(key,out var subjectVal);
                    Assert.Equal(oracleTgv,subjectTgv);
                    if(oracleTgv) Assert.Equal(oracleVal,subjectVal);
                    break;
                case Op.Indexer_Get:
                    if(oracle.ContainsKey(key)){
                        Assert.Equal(oracle[key],subject[key]);
                    }
                    else {
                        Assert.Throws<KeyNotFoundException>(() => subject[key]);
                    }
                    break;
                case Op.Indexer_Set:
                    oracle[key]=value;
                    subject[key]=value;
                    break;
                case Op.Clear:
                    oracle.Clear();
                    subject.Clear();
                    break;
                case Op.Count:
                    Assert.Equal(oracle.Count,subject.Count);
                    break;
                case Op.Keys:
                    var oracleKeys=oracle.Keys.OrderBy(k => k).ToList();
                    var subjectKeys=subject.Keys.OrderBy(k => k).ToList();
                    Assert.Equal(oracleKeys,subjectKeys);
                    break;
                case Op.Values:
                    var oracleVals=oracle.Values.OrderBy(v => v).ToList();
                    var subjectVals=subject.Values.OrderBy(v => v).ToList();
                    Assert.Equal(oracleVals,subjectVals);
                    break;
            }

            Assert.Equal(oracle.Count,subject.Count);
        }

        AssertDictEquals(oracle,subject);
    }

    private static void AssertDictEquals<TKey, TValue>(Dictionary<TKey,TValue> oracle,ISeamlessDictionary<TKey,TValue> subject)
        where TKey:IEquatable<TKey>
        where TValue:IEquatable<TValue> {
        Assert.Equal(oracle.Count,subject.Count);
        foreach(var kvp in oracle){
            Assert.True(subject.TryGetValue(kvp.Key,out var val),$"Subject missing key {kvp.Key}");
            Assert.Equal(kvp.Value,val);
        }
    }
}
