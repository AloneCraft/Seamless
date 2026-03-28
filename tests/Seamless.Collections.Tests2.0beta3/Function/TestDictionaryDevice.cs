using Tsavorite.core;
using Xunit;
namespace Seamless.Collections.Tests;
/// <summary>
/// デバイスを型引数で切り替え可能なDictionaryテストの基底クラス
/// </summary>
[Collection("Sequential")]
public abstract class TestDictionaryDeviceDevice<TKey,TValue>(ITestOutputHelper output):Test<TKey,TValue>(output)
    where TKey:IEquatable<TKey>
    where TValue:IEquatable<TValue>{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability","CA1416:プラットフォームの互換性を検証",Justification = "<保留中>")]
    private DictionaryStore<TKey,TValue>Create(){
        var folder=Path.Combine(Path.GetTempPath(),"SeamlessTest_"+Guid.NewGuid().ToString("N"));
        var LogDevice=new LocalStorageDevice(folder);
        var ObjectLogDevice=new LocalStorageDevice(folder+"_obj");
        return new DictionaryStore<TKey,TValue>(LogDevice,ObjectLogDevice,MutableFraction:0.1);
    }
	[Fact]
	public void Add_大量データでストレージスピルをテスト(){
		using var Store=this.Create();
        using var dict=Store.Create();
        //メモリサイズを超えるデータを追加してストレージにスピルさせる
        var count=100000;
		for(var a=0;a<count;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		Assert.Equal(count,dict.GetLongCount());
		//データの整合性確認
		for(var a=0;a<count;a++){
			Assert.True(dict.TryGetValue(this.CreateKey(a),out var value));
			Assert.Equal(this.CreateValue(a),value);
		}
	}
	[Fact]
	public void Add_スピル後の削除と再追加(){
		using var store=this.Create();
		using var dict=store.Create();
		//大量データを追加
		var count=50000;
		for(var a=0;a<count;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		//半分削除
		for(var a=0;a<count/2;a++){
			Assert.True(dict.Remove(this.CreateKey(a)));
		}
		Assert.Equal(count/2,dict.GetLongCount());
		//削除したキーを再追加
		for(var a=0;a<count/2;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		Assert.Equal(count,dict.GetLongCount());
		//データの整合性確認
		for(var a=0;a<count/2;a++){
			Assert.True(dict.TryGetValue(this.CreateKey(a),out var value));
			Assert.Equal(this.CreateValue(a),value);
		}
		for(var a=count/2;a<count;a++){
			Assert.True(dict.TryGetValue(this.CreateKey(a),out var value));
			Assert.Equal(this.CreateValue(a),value);
		}
	}
	[Fact]
	public void Add_スピル後のClear(){
		using var store=this.Create();
		using var dict=store.Create();
		//大量データを追加
		var count=50000;
		for(var a=0;a<count;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		Assert.Equal(count,dict.GetLongCount());
		//全削除
		dict.Clear();
		Assert.Equal(0,dict.GetLongCount());
		//削除後に再追加
		for(var a=0;a<10;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		Assert.Equal(10,dict.GetLongCount());
	}
	[Fact]
	public void Add_スピル後のCompaction(){
		using var store=this.Create();
		using var dict=store.Create();
		//大量データを追加
		var count=50000;
		for(var a=0;a<count;a++){
			dict.Add(this.CreateKey(a),this.CreateValue(a));
		}
		//コンパクション実行
		store.CompactWithLookup();
		//データの整合性確認
		Assert.Equal(count,dict.GetLongCount());
		for(var a=0;a<count;a++){
			Assert.True(dict.TryGetValue(this.CreateKey(a),out var value));
			Assert.Equal(this.CreateValue(a),value);
		}
	}
}
[Trait("TKey","int")]
public abstract class TestDictionaryDevice_IntKey<TValue>:Test<int,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override int CreateKey(int seed)=>seed;
}
[Trait("TKey","string")]
public abstract class TestDictionaryDevice_StringKey<TValue>:Test<string,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override string CreateKey(int seed)=>seed.ToString();
}
[Trait("TKey","decimal")]
public abstract class TestDictionaryDevice_DecimalKey<TValue>:Test<decimal,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override decimal CreateKey(int seed)=>(decimal)seed;
}
[Trait("TKey","UnmanagedStruct")]
public abstract class TestDictionaryDevice_UnmanagedStructKey<TValue>:Test<UnmanagedStruct,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override UnmanagedStruct CreateKey(int seed)=>new(){Value=seed};
}
[Trait("TKey","ManagedStruct")]
public abstract class TestDictionaryDevice_ManagedStructKey<TValue>:Test<ManagedStruct,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override ManagedStruct CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TKey","ManagedClass")]
public abstract class TestDictionaryDevice_ManagedClassKey<TValue>:Test<ManagedClass,TValue> where TValue:notnull{
	protected override double MutableFraction=>1.0;
	protected override ManagedClass CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
// ---全36組み合わせのConcreteクラス---
// Int key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Int_Int:TestDictionaryDevice_IntKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Int_String:TestDictionaryDevice_IntKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Int_Decimal:TestDictionaryDevice_IntKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Int_UnmanagedStruct:TestDictionaryDevice_IntKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Int_ManagedStruct:TestDictionaryDevice_IntKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Int_ManagedClass:TestDictionaryDevice_IntKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// String key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_Int:TestDictionaryDevice_StringKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_String:TestDictionaryDevice_StringKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_Decimal:TestDictionaryDevice_StringKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_UnmanagedStruct:TestDictionaryDevice_StringKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_ManagedStruct:TestDictionaryDevice_StringKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_String_ManagedClass:TestDictionaryDevice_StringKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// Decimal key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Decimal_Int:TestDictionaryDevice_DecimalKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Decimal_String:TestDictionaryDevice_DecimalKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Decimal_Decimal:TestDictionaryDevice_DecimalKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_Decimal_UnmanagedStruct:TestDictionaryDevice_DecimalKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Decimal_ManagedStruct:TestDictionaryDevice_DecimalKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_Decimal_ManagedClass:TestDictionaryDevice_DecimalKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// UnmanagedStruct key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_UnmanagedStruct_Int:TestDictionaryDevice_UnmanagedStructKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_UnmanagedStruct_String:TestDictionaryDevice_UnmanagedStructKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_UnmanagedStruct_Decimal:TestDictionaryDevice_UnmanagedStructKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class TestDictionaryDevice_UnmanagedStruct_UnmanagedStruct:TestDictionaryDevice_UnmanagedStructKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_UnmanagedStruct_ManagedStruct:TestDictionaryDevice_UnmanagedStructKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_UnmanagedStruct_ManagedClass:TestDictionaryDevice_UnmanagedStructKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// ManagedStruct key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_Int:TestDictionaryDevice_ManagedStructKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_String:TestDictionaryDevice_ManagedStructKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_Decimal:TestDictionaryDevice_ManagedStructKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_UnmanagedStruct:TestDictionaryDevice_ManagedStructKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_ManagedStruct:TestDictionaryDevice_ManagedStructKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedStruct_ManagedClass:TestDictionaryDevice_ManagedStructKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
// ManagedClass key × 6 values
[Trait("TValue","int"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_Int:TestDictionaryDevice_ManagedClassKey<int>{
	protected override int CreateValue(int seed)=>seed;
}
[Trait("TValue","string"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_String:TestDictionaryDevice_ManagedClassKey<string>{
	protected override string CreateValue(int seed)=>seed.ToString();
}
[Trait("TValue","decimal"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_Decimal:TestDictionaryDevice_ManagedClassKey<decimal>{
	protected override decimal CreateValue(int seed)=>(decimal)seed;
}
[Trait("TValue","UnmanagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_UnmanagedStruct:TestDictionaryDevice_ManagedClassKey<UnmanagedStruct>{
	protected override UnmanagedStruct CreateValue(int seed)=>new(){Value=seed};
}
[Trait("TValue","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_ManagedStruct:TestDictionaryDevice_ManagedClassKey<ManagedStruct>{
	protected override ManagedStruct CreateValue(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TValue","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestDictionaryDevice_ManagedClass_ManagedClass:TestDictionaryDevice_ManagedClassKey<ManagedClass>{
	protected override ManagedClass CreateValue(int seed)=>new(){Name=$"N{seed}"};
}

