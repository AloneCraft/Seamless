using Common;
using Xunit;
namespace Seamless.Collections.Tests;

public abstract class TestDictionary<TKey, TValue>:Test<TKey,TValue>
    where TKey : IEquatable<TKey>
    where TValue : IEquatable<TValue> {
    protected override double MutableFraction => 1;
    protected DictionaryStore<TKey,TValue> CreateStore() => new(MutableFraction: this.MutableFraction);
    [Fact]
    public void Add_新しいキーと値を追加できる() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var v1 = this.CreateValue(1);
            dict.Add(k1,v1);
            Assert.Equal(v1,dict[k1]);
        }
    }
    [Fact]
    public void Add_重複したキーを追加すると例外が発生する() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var v1 = this.CreateValue(1);
            var v2 = this.CreateValue(2);
            dict.Add(k1,v1);
            var exception = Assert.Throws<ArgumentException>(() => dict.Add(k1,v2));
            Assert.NotNull(exception);
        }
    }
    [Fact]
    public void Indexer_既存の値を上書きする() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var v1 = this.CreateValue(1);
            var v2 = this.CreateValue(2);
            dict[k1]=v1;
            dict[k1]=v2;
            Assert.Equal(v2,dict[k1]);
            Assert.Equal(1,dict.GetLongCount());
        }
    }
    [Fact]
    public void Remove_キーが存在する場合削除してCountを減らす() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var k2 = this.CreateKey(2);
            dict.Add(k1,this.CreateValue(1));
            dict.Add(k2,this.CreateValue(2));
            var removed = dict.Remove(k1);
            Assert.True(removed);
            Assert.False(dict.ContainsKey(k1));
            Assert.Equal(1,dict.GetLongCount());
        }
    }
    [Fact]
    public void Remove_キーが存在しない場合偽を返す() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var removed = dict.Remove(k1);
            Assert.False(removed);
            Assert.False(dict.ContainsKey(k1));
            Assert.Equal(0,dict.GetLongCount());
        }
    }
    [Fact]
    public void Clear_全てのエントリを削除する() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        {
            var k1 = this.CreateKey(1);
            var k2 = this.CreateKey(2);
            var k3 = this.CreateKey(3);
            dict.Add(k1,this.CreateValue(1));
            dict.Add(k2,this.CreateValue(2));
            dict.Add(k3,this.CreateValue(3));
            dict.Clear();
            Assert.Equal(0,dict.GetLongCount());
            Assert.False(dict.ContainsKey(k1));
        }
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Add_指定された数の要素を追加してCountが正しい(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        for(var i = 0; i<Count; i++) {
            dict.Add(this.CreateKey(i),this.CreateValue(i));
        }
        Assert.Equal(Count,dict.GetLongCount());
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Add_Mutable領域内でInsert_Delete_Updateのサイクルを繰り返す(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        for(var i = 0; i<Count; i++) {
            dict[this.CreateKey(i)]=this.CreateValue(i);
        }
        Assert.Equal(Count,dict.GetLongCount());
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Add_指定された数プラス1の要素を追加して動作を確認(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        for(var i = 0; i<Count; i++) {
            dict.Add(this.CreateKey(i),this.CreateValue(i));
        }
        Assert.Equal(Count,dict.GetLongCount());
        dict.Add(this.CreateKey(Count),this.CreateValue(Count));
        Assert.Equal(Count+1,dict.GetLongCount());
        Assert.True(dict.ContainsKey(this.CreateKey(Count)));
    }
    [Fact]
    public void Add_MutableメモリがオーバーフローするまでUpsertして発生した要素数をログ出力する() {
        const int MaxAttempts = 10_000;
        using var store = this.CreateStore();
        using var dict = store.Create();
        var count = 0;
        for(; count<MaxAttempts; count++) {
            var result = dict.TryAdd(this.CreateKey(count),this.CreateValue(count));
            if(result==TryAddResult.LogFull) {
                break;
            }
            Assert.Equal(TryAddResult.Success,result);
        }
        Assert.True(count>0,"At least one element should have been added before overflow");
        Assert.True(count<MaxAttempts,"Overflow was not observed within the bounded attempts");
        Assert.Equal(TryAddResult.LogFull,dict.TryAdd(this.CreateKey(count),this.CreateValue(count)));
    }
    [Fact]
    public void Add_MutableメモリがオーバーフローするまでRMWして発生した要素数をログ出力する() {
        const int MaxAttempts = 10_000;
        using var store = this.CreateStore();
        using var dict = store.Create();
        var count = 0;
        for(; count<MaxAttempts; count++) {
            var result = dict.TryAdd(this.CreateKey(count),this.CreateValue(count));
            if(result==TryAddResult.LogFull) {
                return;
            }
        }
        Assert.True(count>0,"At least one element should have been added before overflow");
        Assert.True(count<MaxAttempts,"Overflow was not observed within the bounded attempts");
        Assert.Throws<OutOfMemoryException>(() => dict[this.CreateKey(count)]=this.CreateValue(count));
    }
    [Fact]
    public void Create_同じStoreから複数のDictionaryを作成して並列に処理する() {
        const int Count = 10;
        using var store = this.CreateStore();
        using var dict1 = store.Create();
        using var dict2 = store.Create();
        using var dict3 = store.Create();
        var tasks = new Task[]{
            Task.Run(()=>{
                for(var i=0;i<Count*1;i++){
                    dict1[this.CreateKey(i)]=this.CreateValue(i);
                }
            }),
            Task.Run(()=>{
                for(var i=Count*1;i<Count*2;i++){
                    dict2[this.CreateKey(i)]=this.CreateValue(i);
                }
            }),
            Task.Run(()=>{
                for(var i=Count*2;i<Count*3;i++){
                    dict3[this.CreateKey(i)]=this.CreateValue(i);
                }
            })
        };
        Task.WaitAll(tasks);
        var allAdded = true;
        for(var i = 0; i<Count*3; i++) {
            if(!dict1.ContainsKey(this.CreateKey(i))&&!dict2.ContainsKey(this.CreateKey(i))&&!dict3.ContainsKey(this.CreateKey(i))) {
                allAdded=false;
                break;
            }
        }
        Assert.True(allAdded);
    }
    [Fact]
    public void Create_複数のDictionaryを作成して異なるStoreで並列に処理する() {
        const int Count = 128;
        using var store1 = this.CreateStore();
        using var store2 = this.CreateStore();
        using var store3 = this.CreateStore();
        using var dict1 = store1.Create();
        using var dict2 = store2.Create();
        using var dict3 = store3.Create();
        var tasks = new Task[]{
            Task.Run(()=>{
                for(var i=0;i<Count;i++){
                    dict1.Add(this.CreateKey(i),this.CreateValue(i));
                }
            }),
            Task.Run(()=>{
                for(var i=0;i<Count;i++){
                    dict2.Add(this.CreateKey(i),this.CreateValue(i));
                }
            }),
            Task.Run(()=>{
                for(var i=0;i<Count;i++){
                    dict3.Add(this.CreateKey(i),this.CreateValue(i));
                }
            })
        };
        Task.WaitAll(tasks);
        Assert.Equal(Count,dict1.GetLongCount());
        Assert.Equal(Count,dict2.GetLongCount());
        Assert.Equal(Count,dict3.GetLongCount());
        for(var i = 0; i<Count; i++) {
            Assert.True(dict1.ContainsKey(this.CreateKey(i)));
            Assert.True(dict2.ContainsKey(this.CreateKey(i)));
            Assert.True(dict3.ContainsKey(this.CreateKey(i)));
        }
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void GetLongCount(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        for(var i = 0; i<Count; i++) {
            dict.Add(this.CreateKey(i),this.CreateValue(i));
        }
        var expected = dict.GetLongCount();
        Assert.Equal(expected,dict.GetLongCount());
    }
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    public void GetEnumerator(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        var lookup = new System.Collections.Generic.Dictionary<TKey,TValue>();
        for(var i = 0; i<Count; i++) {
            var k = this.CreateKey(i);
            var v = this.CreateValue(i);
            lookup[k]=v;
            dict.Add(k,v);
        }
        foreach(var kvp in dict) {
            if(lookup.TryGetValue(kvp.Key,out var expectedValue)) {
                Assert.Equal(expectedValue,kvp.Value);
            } else {
                Assert.Fail($"Unexpected key in enumeration");
            }
        }
    }
    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(128)]
    [InlineData(129)]
    public void IterateLookup_指定された数の要素をバッチ処理で正しく取得する(int Count) {
        using var store = this.CreateStore();
        using var dict = store.Create();
        for(var i = 0; i<Count; i++) {
            dict.Add(this.CreateKey(i),this.CreateValue(i));
        }
        var totalCount = 0;
        var seenKeys = new System.Collections.Generic.HashSet<TKey>();
        foreach(var kvp in dict) {
            Assert.True(seenKeys.Add(kvp.Key),$"Duplicate key");
            totalCount++;
        }
        Assert.Equal(Count,totalCount);
        Assert.Equal(seenKeys.Count,totalCount);
    }
    [Fact]
    public void Add_Sequence() {
        using var store = this.CreateStore();
        using var dict0 = store.Create();
        var dict1 = new System.Collections.Generic.Dictionary<TKey,TValue>();
        for(var Count = 0; Count<10; Count++) {
            for(var Start = 0; Start<Count; Start++) {
                for(var End = Start; End<Count; End++) {
                    dict0.Clear();
                    dict1.Clear();
                    Add(dict0,Start+1,End+1);
                    Add(dict1,Start+1,End+1);
                    var KeyValues0 = ToList(dict0);
                    var KeyValues1 = ToList(dict1);
                    Assert.True(KeyValues0.SequenceEqual(KeyValues1));
                }
            }
        }
        return;
        void Add(IDictionary<TKey,TValue> dict,int Start,int End) {
            for(var a = Start; a<End; a++) {
                var aKey = this.CreateKey(a);
                var aValue = this.CreateValue(a);
                dict.Add(aKey,aValue);
            }
        }
        static List<(TKey Key,TValue Value)> ToList(IDictionary<TKey,TValue> dict) {
            var KeyValues = dict.ToList();
            KeyValues.Sort((l,r) => Comparer<TKey>.Default.Compare(l.Key,r.Key));
            return KeyValues.Select(a => (a.Key,a.Value)).ToList();
        }
    }
    [Fact]
    public void Add_メモリ不足時にfalseを返す() {
        using var store = this.CreateStore();
        using var dict = store.Create();
        var count = 0;
        while(true) {
            var Result = dict.TryAdd(this.CreateKey(count),this.CreateValue(count));
            if(Result==TryAddResult.LogFull) {
                break;
            }
            count++;
            if(count%100000==0) {
                this.Write($"Current count: {count:N0}");
            }
        }
        Assert.True(count>0,"At least one element should have been added before overflow");
    }
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void Add_Remove(int Count) {
        for(var Start = 0; Start<Count; Start++) {
            for(var End = Start; End<Count; End++) {
                using var store = this.CreateStore();
                using var dict0 = store.Create();
                var dict1 = new System.Collections.Generic.Dictionary<TKey,TValue>();
                {
                    Add(dict0,Start,End);
                    Add(dict1,Start,End);
                    var KeyValues0 = ToList(dict0);
                    var KeyValues1 = ToList(dict1);
                    Assert.True(KeyValues0.SequenceEqual(KeyValues1));
                }
                {
                    Remove(dict0,Start,End);
                    Remove(dict1,Start,End);
                    var KeyValues0 = ToList(dict0);
                    var KeyValues1 = ToList(dict1);
                    Assert.True(KeyValues0.SequenceEqual(KeyValues1));
                }
            }
        }
        return;
        void Add(IDictionary<TKey,TValue> dict,int Start,int End) {
            dict.Clear();
            for(var a = Start; a<End; a++) {
                var aKey = this.CreateKey(a);
                var aValue = this.CreateValue(a);
                dict.Add(aKey,aValue);
            }
        }
        void Remove(IDictionary<TKey,TValue> dict,int Start,int End) {
            dict.Clear();
            for(var a = Start; a<End; a++) {
                var aKey = this.CreateKey(a);
                dict.Remove(aKey);
            }
        }
        static List<(TKey Key,TValue Value)> ToList(IDictionary<TKey,TValue> dict) {
            var KeyValues = dict.ToList();
            KeyValues.Sort((l,r) => Comparer<TKey>.Default.Compare(l.Key,r.Key));
            return KeyValues.Select(a => (a.Key,a.Value)).ToList();
        }
    }
}
[Trait("TValue","int"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Int_Int:TestDictionary<int,int> {
    protected override int CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Int_String:TestDictionary<int,string> {
    protected override int CreateKey(int seed) => seed;
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Int_Decimal:TestDictionary<int,decimal> {
    protected override int CreateKey(int seed) => seed;
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Int_UnmanagedStruct:TestDictionary<int,UnmanagedStruct> {
    protected override int CreateKey(int seed) => seed;
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Int_ManagedStruct:TestDictionary<int,ManagedStruct> {
    protected override int CreateKey(int seed) => seed;
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Int_ManagedClass:TestDictionary<int,ManagedClass> {
    protected override int CreateKey(int seed) => seed;
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}
// String key × 6 values
[Trait("TValue","int"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_Int:TestDictionary<string,int> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_String:TestDictionary<string,string> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_Decimal:TestDictionary<string,decimal> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_UnmanagedStruct:TestDictionary<string,UnmanagedStruct> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_ManagedStruct:TestDictionary<string,ManagedStruct> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_String_ManagedClass:TestDictionary<string,ManagedClass> {
    protected override string CreateKey(int seed) => seed.ToString();
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}
// Decimal key × 6 values
[Trait("TValue","int"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Decimal_Int:TestDictionary<decimal,int> {
    protected override decimal CreateKey(int seed) => seed;
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Decimal_String:TestDictionary<decimal,string> {
    protected override decimal CreateKey(int seed) => seed;
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Decimal_Decimal:TestDictionary<decimal,decimal> {
    protected override decimal CreateKey(int seed) => seed;
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_Decimal_UnmanagedStruct:TestDictionary<decimal,UnmanagedStruct> {
    protected override decimal CreateKey(int seed) => seed;
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Decimal_ManagedStruct:TestDictionary<decimal,ManagedStruct> {
    protected override decimal CreateKey(int seed) => seed;
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_Decimal_ManagedClass:TestDictionary<decimal,ManagedClass> {
    protected override decimal CreateKey(int seed) => seed;
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}
// UnmanagedStruct key × 6 values
[Trait("TValue","int"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_UnmanagedStruct_Int:TestDictionary<UnmanagedStruct,int> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_UnmanagedStruct_String:TestDictionary<UnmanagedStruct,string> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_UnmanagedStruct_Decimal:TestDictionary<UnmanagedStruct,decimal> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Unmanaged")]
public class TestDictionary_UnmanagedStruct_UnmanagedStruct:TestDictionary<UnmanagedStruct,UnmanagedStruct> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_UnmanagedStruct_ManagedStruct:TestDictionary<UnmanagedStruct,ManagedStruct> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_UnmanagedStruct_ManagedClass:TestDictionary<UnmanagedStruct,ManagedClass> {
    protected override UnmanagedStruct CreateKey(int seed) => new() { Value=seed };
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}
// ManagedStruct key × 6 values
[Trait("TValue","int"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_Int:TestDictionary<ManagedStruct,int> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_String:TestDictionary<ManagedStruct,string> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_Decimal:TestDictionary<ManagedStruct,decimal> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_UnmanagedStruct:TestDictionary<ManagedStruct,UnmanagedStruct> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_ManagedStruct:TestDictionary<ManagedStruct,ManagedStruct> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedStruct_ManagedClass:TestDictionary<ManagedStruct,ManagedClass> {
    protected override ManagedStruct CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}
// ManagedClass key × 6 values
[Trait("TValue","int"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_Int:TestDictionary<ManagedClass,int> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override int CreateValue(int seed) => seed;
}
[Trait("TValue","string"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_String:TestDictionary<ManagedClass,string> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override string CreateValue(int seed) => seed.ToString();
}
[Trait("TValue","decimal"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_Decimal:TestDictionary<ManagedClass,decimal> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override decimal CreateValue(int seed) => (decimal)seed;
}
[Trait("TValue","UnmanagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_UnmanagedStruct:TestDictionary<ManagedClass,UnmanagedStruct> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override UnmanagedStruct CreateValue(int seed) => new() { Value=seed };
}
[Trait("TValue","ManagedStruct"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_ManagedStruct:TestDictionary<ManagedClass,ManagedStruct> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override ManagedStruct CreateValue(int seed) => new() { Name=$"N{seed}" };
}
[Trait("TValue","ManagedClass"), Trait("TKeyTValue","Managed")]
public class TestDictionary_ManagedClass_ManagedClass:TestDictionary<ManagedClass,ManagedClass> {
    protected override ManagedClass CreateKey(int seed) => new() { Name=$"N{seed}" };
    protected override ManagedClass CreateValue(int seed) => new() { Name=$"N{seed}" };
}

