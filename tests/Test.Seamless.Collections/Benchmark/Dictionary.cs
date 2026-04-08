using System.Diagnostics;
using Seamless.Collections;
using Seamless.Store;
using Test.Seamless.Collections;
namespace Benchmarks;
public abstract class Dictionary<TKey,TValue>(ITestOutputHelper output):Benchmark<TKey,TValue>(output) 
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    private protected MemoryManagedDictionaryStore<TKey,TValue,ManagedSerializer<TKey>,ManagedSerializer<TValue>> CreateStore(){
        var MemorySize=Seamless.Const.LogMemorySize<<11;
        return Factory.Create<TKey,TValue>(IndexSize:IndexSize,MemorySize:MemorySize);
    }
    [Fact]
	[Trait("Category","")]
	public void _Add_10万件の追加時間を計測する(){
        var sw=new Stopwatch();
		using var store=this.CreateStore();
		using var dict=store.Create();
		sw.Start();
        for(var i=0;i<Count;i++){
            dict.Add(this.CreateKey(i),this.CreateValue(i));
        }
        sw.Stop();
		this.Write($"[Dictionary.Add] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _ContainsKey_10万件の検索時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			_=dict.ContainsKey(this.CreateKey(i));
		sw.Stop();
		this.Write($"[Dictionary.ContainsKey] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _TryGetValue_10万件の取得時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			_=dict.TryGetValue(this.CreateKey(i),out _);
		sw.Stop();
		this.Write($"[Dictionary.TryGetValue] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _Indexer_Set_10万件の上書き時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
        Assert.Equal(Count,dict.GetLongCount());
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			dict[this.CreateKey(i)]=this.CreateValue(i+Count);
		sw.Stop();
		this.Write($"[Dictionary.Indexer set] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
        Assert.Equal(Count,dict.GetLongCount());
	}
	[Fact]
	[Trait("Category","")]
	public void _Remove_10万件の削除時間を計測する(){
        var sw=new Stopwatch();
        using var store=this.CreateStore();
        using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		sw.Start();
        for(var i=0;i<Count;i++)
            dict.Remove(this.CreateKey(i));
        sw.Stop();
        this.Write($"[Dictionary.Remove] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _GetEnumerator_10万件の列挙時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		var count=0;
        foreach(var kvp in dict)
            count++;
		var sw=Stopwatch.StartNew();
        foreach(var kvp in dict){}
		sw.Stop();
		this.Write($"[Dictionary.GetEnumerator] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
		Assert.Equal(Count,dict.GetLongCount());
	}
	[Fact]
	[Trait("Category","")]
	public void _Keys_10万件のKeys取得時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		var sw=Stopwatch.StartNew();
		var keys=dict.Keys;
		sw.Stop();
		this.Write($"[Dictionary.Keys] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
		Assert.Equal(Count,keys.Count);
	}
	[Fact]
	[Trait("Category","")]
	public void _Values_10万件のValues取得時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		var sw=Stopwatch.StartNew();
		var values=dict.Values;
		sw.Stop();
		this.Write($"[Dictionary.Values] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
		Assert.Equal(Count,values.Count);
	}
	[Fact]
	[Trait("Category","")]
	public void _Mixed_0万件のAdd_Read_Remove混合時間を計測する(){
		using var store=this.CreateStore();
		using var dict=store.Create();
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			dict.Add(this.CreateKey(i),this.CreateValue(i));
		for(var i=0;i<Count;i++)
			_=dict.TryGetValue(this.CreateKey(i),out _);
		for(var i=0;i<Count;i++)
			dict.Remove(this.CreateKey(i));
		sw.Stop();
		this.Write($"[Dictionary.Mixed Add+Read+Remove] {Count:N0}件x3: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*3):F2}us/op)");
	}
}
[Trait("TKey","int")]
public abstract class Dictionary_Int<TValue>(ITestOutputHelper output):Dictionary<int,TValue>(output) where TValue:IEquatable<TValue>{
	protected override int CreateKey(int seed)=>seed;
}
// ---StringKey派生---
[Trait("TKey","string")]
public abstract class Dictionary_String<TValue>(ITestOutputHelper output):Dictionary<string,TValue>(output) where TValue:IEquatable<TValue>{
	protected override string CreateKey(int seed)=>seed.ToString();
}
// ---DecimalKey派生---
[Trait("TKey","decimal")]
public abstract class Dictionary_Decimal<TValue>(ITestOutputHelper output):Dictionary<decimal,TValue>(output) where TValue:IEquatable<TValue>{
	protected override decimal CreateKey(int seed)=>(decimal)seed;
}
// ---UnmanagedStructKey派生---
[Trait("TKey","UnmanagedStruct")]
public abstract class Dictionary_UnmanagedStruct<TValue>(ITestOutputHelper output):Dictionary<UnmanagedStruct,TValue>(output) where TValue:IEquatable<TValue>{
	protected override UnmanagedStruct CreateKey(int seed)=>new(){Value=seed};
}
// ---ManagedStructKey派生---
[Trait("TKey","ManagedStruct")]
public abstract class Dictionary_ManagedStruct<TValue>(ITestOutputHelper output):Dictionary<ManagedStruct,TValue>(output) where TValue:IEquatable<TValue>{
	protected override ManagedStruct CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
// ---SampleClassKey派生---
[Trait("TKey","ManagedClass")]
public abstract class Dictionary_Class<TValue>(ITestOutputHelper output):Dictionary<ManagedClass,TValue>(output) where TValue:IEquatable<TValue>{
	protected override ManagedClass CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Int_Int(ITestOutputHelper output):Dictionary_Int<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class Dictionary_Int_String(ITestOutputHelper output):Dictionary_Int<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Int_Decimal(ITestOutputHelper output):Dictionary_Int<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Int_UnmanagedStruct(ITestOutputHelper output):Dictionary_Int<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_Int_ManagedStruct(ITestOutputHelper output):Dictionary_Int<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class Dictionary_Int_SampleClass(ITestOutputHelper output):Dictionary_Int<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// String key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_Int(ITestOutputHelper output):Dictionary_String<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_String(ITestOutputHelper output):Dictionary_String<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_Decimal(ITestOutputHelper output):Dictionary_String<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_UnmanagedStruct(ITestOutputHelper output):Dictionary_String<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_ManagedStruct(ITestOutputHelper output):Dictionary_String<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class Dictionary_String_SampleClass(ITestOutputHelper output):Dictionary_String<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// Decimal key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Decimal_Int(ITestOutputHelper output):Dictionary_Decimal<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class Dictionary_Decimal_String(ITestOutputHelper output):Dictionary_Decimal<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Decimal_Decimal(ITestOutputHelper output):Dictionary_Decimal<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_Decimal_UnmanagedStruct(ITestOutputHelper output):Dictionary_Decimal<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_Decimal_ManagedStruct(ITestOutputHelper output):Dictionary_Decimal<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class Dictionary_Decimal_SampleClass(ITestOutputHelper output):Dictionary_Decimal<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// UnmanagedStruct key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_UnmanagedStruct_Int(ITestOutputHelper output):Dictionary_UnmanagedStruct<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class Dictionary_UnmanagedStruct_String(ITestOutputHelper output):Dictionary_UnmanagedStruct<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_UnmanagedStruct_Decimal(ITestOutputHelper output):Dictionary_UnmanagedStruct<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class Dictionary_UnmanagedStruct_UnmanagedStruct(ITestOutputHelper output):Dictionary_UnmanagedStruct<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_UnmanagedStruct_ManagedStruct(ITestOutputHelper output):Dictionary_UnmanagedStruct<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class Dictionary_UnmanagedStruct_SampleClass(ITestOutputHelper output):Dictionary_UnmanagedStruct<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// ManagedStruct key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_Int(ITestOutputHelper output):Dictionary_ManagedStruct<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_String(ITestOutputHelper output):Dictionary_ManagedStruct<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_Decimal(ITestOutputHelper output):Dictionary_ManagedStruct<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_UnmanagedStruct(ITestOutputHelper output):Dictionary_ManagedStruct<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_ManagedStruct(ITestOutputHelper output):Dictionary_ManagedStruct<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class Dictionary_ManagedStruct_SampleClass(ITestOutputHelper output):Dictionary_ManagedStruct<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// SampleClass key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class DictionaryClassInt(ITestOutputHelper output):Dictionary_Class<int>(output){
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class DictionaryClassString(ITestOutputHelper output):Dictionary_Class<string>(output){
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class DictionaryClassDecimal(ITestOutputHelper output):Dictionary_Class<decimal>(output){
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class DictionaryClassUnmanagedStruct(ITestOutputHelper output):Dictionary_Class<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class DictionaryClassManagedStruct(ITestOutputHelper output):Dictionary_Class<ManagedStruct>(output){
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class DictionaryClassClass(ITestOutputHelper output):Dictionary_Class<ManagedClass>(output){
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
