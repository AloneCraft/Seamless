using Xunit;
namespace Seamless.Collections.Tests;
public abstract class TestHashSet<T>:Test<T,Empty> where T:IEquatable<T>{
	//protected abstract double MutableFraction{get;}
	//protected abstract T CreateKey(int seed);
    protected override Empty CreateValue(int seed)=>default;
	protected HashSetStore<T> CreateStore()=>new(MutableFraction:this.MutableFraction);
	protected HashSetStore<T> CreateStore(long MemorySize)=>new(MemorySize:MemorySize,MutableFraction:this.MutableFraction);
	[Fact]
	public void Add_新しい要素を追加すると真を返す(){
		using var store=this.CreateStore();
		using var set=store.Create();
		{
			var v10=this.CreateKey(10);
			var added=set.Add(v10);
			Assert.True(added);
			Assert.Equal(1,set.GetLongCount());
			Assert.Contains(v10,set);
		}
	}
	[Fact]
	public void Add_既存の要素を追加すると偽を返す(){
		using var store=this.CreateStore();
		using var set=store.Create();
		{
			var v10=this.CreateKey(10);
			set.Add(v10);
			var added=set.Add(v10);
			Assert.False(added);
			Assert.Equal(1,set.GetLongCount());
		}
	}
	[Fact]
	public void Remove_要素が存在する場合真を返す(){
		using var store=this.CreateStore();
		using var set=store.Create();
		{
			var v10=this.CreateKey(10);
			var v20=this.CreateKey(20);
			set.Add(v10);
			set.Add(v20);
			var removed=set.Remove(v10);
			Assert.True(removed);
			Assert.Equal(1,set.GetLongCount());
			Assert.DoesNotContain(v10,set);
			Assert.Contains(v20,set);
		}
	}
	[Fact]
	public void Clear_全ての要素を削除する(){
		using var store=this.CreateStore();
		using var set=store.Create();
		{
			var v1=this.CreateKey(1);
			var v2=this.CreateKey(2);
			var v3=this.CreateKey(3);
			set.Add(v1);
			set.Add(v2);
			set.Add(v3);
			set.Clear();
			Assert.Equal(0,set.GetLongCount());
			Assert.DoesNotContain(v1,set);
			Assert.DoesNotContain(v2,set);
			Assert.DoesNotContain(v3,set);
		}
	}
	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void Add_指定された数の要素を追加してCountが正しい(int elementCount){
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<elementCount;i++){
			set.Add(this.CreateKey(i));
		}
		Assert.Equal(elementCount,set.GetLongCount());
	}
	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void Add_Mutable領域内でInsert_Delete_Updateのサイクルを繰り返す(int elementCount){
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<elementCount;i++){
			var added=set.Add(this.CreateKey(i));
			Assert.True(added);
		}
		Assert.Equal(elementCount,set.GetLongCount());
		for(var i=0;i<elementCount;i++){
			var removed=set.Remove(this.CreateKey(i));
			Assert.True(removed);
			var added=set.Add(this.CreateKey(i+elementCount));
			Assert.True(added);
		}
		Assert.Equal(elementCount,set.GetLongCount());
		for(var i=0;i<elementCount;i++){
			Assert.DoesNotContain(this.CreateKey(i),set);
			Assert.Contains(this.CreateKey(i+elementCount),set);
		}
	}
	[Fact]
	public void Add_Mutable領域内ギリギリでInsert_Update_Deleteの処理を一定回数繰り返す(){
		const int testCount=50;
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<testCount;i++){
			set.Add(this.CreateKey(i));
		}
		Assert.Equal(testCount,set.GetLongCount());
		for(var i=0;i<testCount;i++){
			set.Remove(this.CreateKey(i));
			set.Add(this.CreateKey(i));
		}
		Assert.Equal(testCount,set.GetLongCount());
	}
	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	public void Add_指定された数プラス1の要素を追加して動作を確認(int elementCount){
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<elementCount;i++){
			set.Add(this.CreateKey(i));
		}
		Assert.Equal(elementCount,set.GetLongCount());
		var addedOneMore=set.Add(this.CreateKey(elementCount));
		Assert.True(addedOneMore);
		Assert.Equal(elementCount+1,set.GetLongCount());
	}
	[Fact]public void Add_MutableメモリがオーバーフローするまでUpsertして発生した要素数をログ出力する(){
		const int MaxAttempts=10_000;
		using var store=this.CreateStore();
		using var set=store.Create();
		var count=0;
		OutOfMemoryException? exception=null;
		for(;count<MaxAttempts;count++){
			try{
				set.Add(this.CreateKey(count));
			}catch(OutOfMemoryException ex){
				exception=ex;
				break;
			}
		}
		Assert.True(count>0,"At least one element should have been added before overflow");
		Assert.NotNull(exception);
	}
	[Fact]public void Add_MutableメモリがオーバーフローするまでRMWして発生した要素数をログ出力する(){
		const int MaxAttempts=10_000;
		using var store=this.CreateStore();
		using var set=store.Create();
		var key=this.CreateKey(0);
		Assert.True(set.Add(key));
		var count=0;
		OutOfMemoryException? exception=null;
		for(;count<MaxAttempts;count++){
			Assert.True(set.Remove(key));
			try{
				set.Add(key);
			}catch(OutOfMemoryException ex){
				exception=ex;
				break;
			}
		}
		Assert.True(count>0,"At least one cycle should complete before overflow");
		Assert.NotNull(exception);
	}
	[Fact]
	public void Create_同じStoreから複数のHashSetを作成して並列に処理する(){
		using var store=this.CreateStore();
		using var set1=store.Create();
		using var set2=store.Create();
		using var set3=store.Create();
		var tasks=new Task[]{
			Task.Run(()=>{
				for(var i=0;i<Count*1;i++){
					set1.Add(this.CreateKey(i));
				}
			}),
			Task.Run(()=>{
				for(var i=Count*1;i<Count*2;i++){
					set2.Add(this.CreateKey(i));
				}
			}),
			Task.Run(()=>{
				for(var i=Count*2;i<Count*3;i++){
					set3.Add(this.CreateKey(i));
				}
			})
		};
		Task.WaitAll(tasks);
		var allAdded=true;
		for(var i=0;i<Count*3;i++){
			if(!set1.Contains(this.CreateKey(i))&&!set2.Contains(this.CreateKey(i))&&!set3.Contains(this.CreateKey(i))){
				allAdded=false;
				break;
			}
		}
		Assert.True(allAdded);
	}
	[Fact]
	public void Create_複数のHashSetを作成して異なるStoreで並列に処理する(){
		using var store1=this.CreateStore();
		using var store2=this.CreateStore();
		using var store3=this.CreateStore();
		using var set1=store1.Create();
		using var set2=store2.Create();
		using var set3=store3.Create();
		var tasks=new Task[]{
			Task.Run(()=>{
				for(var i=0;i<Count;i++){
					set1.Add(this.CreateKey(i));
				}
			}),
			Task.Run(()=>{
				for(var i=0;i<Count;i++){
					set2.Add(this.CreateKey(i));
				}
			}),
			Task.Run(()=>{
				for(var i=0;i<Count;i++){
					set3.Add(this.CreateKey(i));
				}
			})
		};
		Task.WaitAll(tasks);
		Assert.Equal(Count,set1.GetLongCount());
		Assert.Equal(Count,set2.GetLongCount());
		Assert.Equal(Count,set3.GetLongCount());
		for(var i=0;i<Count;i++){
			Assert.Contains(this.CreateKey(i),set1);
			Assert.Contains(this.CreateKey(i),set2);
			Assert.Contains(this.CreateKey(i),set3);
		}
	}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_Add_1千件の追加時間を計測する(){
	//	const int elementCount=1_000;
	//	using var store=this.CreateStore();
	//	using var set=store.Create();
	//	var sw=Stopwatch.StartNew();
	//	for(var i=0;i<elementCount;i++){
	//		set.Add(this.CreateValue(i));
	//	}
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.Add] {elementCount:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/elementCount:F2}us/op)");
	//	Assert.Equal(elementCount,set.GetLongCount());
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_Contains_1千件の検索時間を計測する(){
	//	const int elementCount=1_000;
	//	using var store=this.CreateStore();
	//	using var set=store.Create();
	//	for(var i=0;i<elementCount;i++){
	//		set.Add(this.CreateValue(i));
	//	}
	//	var sw=Stopwatch.StartNew();
	//	for(var i=0;i<elementCount;i++){
	//		set.Contains(this.CreateValue(i));
	//	}
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.Contains] {elementCount:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/elementCount:F2}us/op)");
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_Remove_1千件の削除時間を計測する(){
	//	const int elementCount=1_000;
	//	using var store=this.CreateStore();
	//	using var set=store.Create();
	//	for(var i=0;i<elementCount;i++){
	//		set.Add(this.CreateValue(i));
	//	}
	//	var sw=Stopwatch.StartNew();
	//	for(var i=0;i<elementCount;i++){
	//		set.Remove(this.CreateValue(i));
	//	}
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.Remove] {elementCount:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/elementCount:F2}us/op)");
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_GetEnumerator_1千件の列挙時間を計測する(){
	//	const int elementCount=1_000;
	//	using var store=this.CreateStore();
	//	using var set=store.Create();
	//	for(var i=0;i<elementCount;i++){
	//		set.Add(this.CreateValue(i));
	//	}
	//	var count=0;
	//	var sw=Stopwatch.StartNew();
	//	foreach(var item in set){
	//		count++;
	//	}
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.GetEnumerator] {elementCount:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/elementCount:F2}us/op)");
	//	Assert.Equal(elementCount,count);
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_UnionWith_5千件同士の和集合時間を計測する(){
	//	const int elementCount=2_000;
	//	using var store1=this.CreateStore();
	//	using var store2=this.CreateStore();
	//	using var set1=store1.Create();
	//	using var set2=store2.Create();
	//	for(var i=0;i<elementCount;i++){
	//		set1.Add(this.CreateValue(i));
	//	}
	//	for(var i=elementCount/2;i<elementCount+elementCount/2;i++){
	//		set2.Add(this.CreateValue(i));
	//	}
	//	var sw=Stopwatch.StartNew();
	//	set1.UnionWith(set2);
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.UnionWith] {elementCount:N0}件x2: {sw.ElapsedMilliseconds}ms");
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_IntersectWith_5千件同士の積集合時間を計測する(){
	//	const int elementCount=2_000;
	//	using var store1=this.CreateStore();
	//	using var store2=this.CreateStore();
	//	using var set1=store1.Create();
	//	using var set2=store2.Create();
	//	for(var i=0;i<elementCount;i++){
	//		set1.Add(this.CreateValue(i));
	//	}
	//	for(var i=elementCount/2;i<elementCount+elementCount/2;i++){
	//		set2.Add(this.CreateValue(i));
	//	}
	//	var sw=Stopwatch.StartNew();
	//	set1.IntersectWith(set2);
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.IntersectWith] {elementCount:N0}件x2: {sw.ElapsedMilliseconds}ms");
	//}
	//[Fact]
	//[Trait("Category","Benchmark")]
	//public void Benchmark_Mixed_1千件のAdd_Contains_Remove混合時間を計測する(){
	//	const int elementCount=1_000;
	//	using var store=this.CreateStore();
	//	using var set=store.Create();
	//	var sw=Stopwatch.StartNew();
	//	for(var i=0;i<elementCount;i++){
	//		set.Add(this.CreateValue(i));
	//	}
	//	for(var i=0;i<elementCount;i++){
	//		set.Contains(this.CreateValue(i));
	//	}
	//	for(var i=0;i<elementCount;i++){
	//		set.Remove(this.CreateValue(i));
	//	}
	//	sw.Stop();
	//	Console.WriteLine($"[HashSet.Mixed Add+Contains+Remove] {elementCount:N0}件x3: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(elementCount*3):F2}us/op)");
	//}
}
[Trait("TKey","int"),Trait("TKeyTValue","Unmanaged")]
public class TestHashSet_Int:TestHashSet<int>{
	protected override double MutableFraction=>1.0;
	protected override int CreateKey(int seed)=>seed;
}
[Trait("TKey","string"),Trait("TKeyTValue","Managed")]
public class TestHashSet_String:TestHashSet<string>{
	protected override double MutableFraction=>1.0;
	protected override string CreateKey(int seed)=>seed.ToString();
}
[Trait("TKey","decimal"),Trait("TKeyTValue","Unmanaged")]
public class TestHashSet_Decimal:TestHashSet<decimal>{
	protected override double MutableFraction=>1.0;
	protected override decimal CreateKey(int seed)=>(decimal)seed;
}
[Trait("TKey","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class TestHashSet_UnmanagedStruct:TestHashSet<UnmanagedStruct>{
	protected override double MutableFraction=>1.0;
	protected override UnmanagedStruct CreateKey(int seed)=>new(){Value=seed};
}
[Trait("TKey","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class TestHashSet_ManagedStruct:TestHashSet<ManagedStruct>{
	protected override double MutableFraction=>1.0;
	protected override ManagedStruct CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
[Trait("TKey","ManagedClass"),Trait("TKeyTValue","Managed")]
public class TestHashSet_ManagedClass:TestHashSet<ManagedClass>{
	protected override double MutableFraction=>1.0;
	protected override ManagedClass CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
