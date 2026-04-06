using System.Diagnostics;
using Seamless.Collections;
using Test.Seamless.Collections;
using Empty=Seamless.Collections.Empty;
namespace Benchmarks;
public abstract class BenchmarkHashSet<T>(ITestOutputHelper output):Benchmark<T,Empty>(output) where T:IEquatable<T>{
	protected override Empty CreateValue(int seed)=>default;
	protected HashSetStore<T> CreateBenchmarkStore()=>Store.Create<T>();
	[Fact,Trait("Category","Benchmark")]
	public void Benchmark_Add_10万件の追加時間を計測する(){
		var sw=new Stopwatch();
		for(var a=0;a<Loop;a++){
			using var store=this.CreateBenchmarkStore();
			using var set=store.Create();
			sw.Start();
            for(var i=0;i<Count;i++)
                set.Add(this.CreateKey(i));
            sw.Stop();
            Assert.Equal(Count,set.GetLongCount());
        }
		this.Write($"[HashSet.Add] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_Contains_10万件の検索時間を計測する(){
		using var store=this.CreateBenchmarkStore();
		using var set=store.Create();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		var sw=Stopwatch.StartNew();
		for(var a=0;a<Loop;a++)
			for(var i=0;i<Count;i++)
				_=set.Contains(this.CreateKey(i));
		sw.Stop();
		this.Write($"[HashSet.Contains] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_Remove_10万件の削除時間を計測する(){
        var sw=new Stopwatch();
        for(var a=0;a<Loop;a++){
            using var store=this.CreateBenchmarkStore();
            using var set=store.Create();
			for(var i=0;i<Count;i++)
				set.Add(this.CreateKey(i));
			sw.Start();
			for(var i=0;i<Count;i++)
				set.Remove(this.CreateKey(i));
			sw.Stop();
			Assert.Equal(0,set.GetLongCount());
        }
		this.Write($"[HashSet.Remove] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_GetEnumerator_10万件の列挙時間を計測する(){
		using var store=this.CreateBenchmarkStore();
		using var set=store.Create();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		var sw=Stopwatch.StartNew();
        for(var a=0;a<Loop;a++){
            foreach(var item in set){}
        }
        sw.Stop();
		this.Write($"[HashSet.GetEnumerator] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_UnionWith_10万件同士の和集合時間を計測する(){
        var sw=new Stopwatch();
        for(var a=0;a<Loop;a++){
            using var store1=this.CreateBenchmarkStore();
            using var store2=this.CreateBenchmarkStore();
            using var set1=store1.Create();
            using var set2=store2.Create();
            for(var i=0;i<Count;i++)
                set1.Add(this.CreateKey(i));
			for(var i=Count/2;i<Count+Count/2;i++)
				set2.Add(this.CreateKey(i));
			sw.Start();
			set1.UnionWith(set2);
			sw.Stop();
        }
		this.Write($"[HashSet.UnionWith] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_IntersectWith_10万件同士の積集合時間を計測する(){
        var sw=new Stopwatch();
        for(var a=0;a<Loop;a++){
            using var store1=this.CreateBenchmarkStore();
            using var store2=this.CreateBenchmarkStore();
            using var set1=store1.Create();
            using var set2=store2.Create();
            for(var i=0;i<Count;i++)
                set1.Add(this.CreateKey(i));
			for(var i=Count/2;i<Count+Count/2;i++)
				set2.Add(this.CreateKey(i));
			sw.Start();
			set1.IntersectWith(set2);
			sw.Stop();
        }
        this.Write($"[HashSet.IntersectWith] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms");
	}
	[Fact]
	[Trait("Category","Benchmark")]
	public void Benchmark_Mixed_10万件のAdd_Contains_Remove混合時間を計測する(){
		using var store=this.CreateBenchmarkStore();
		using var set=store.Create();
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		for(var i=0;i<Count;i++)
			_=set.Contains(this.CreateKey(i));
		for(var i=0;i<Count;i++)
			set.Remove(this.CreateKey(i));
		sw.Stop();
		this.Write($"[HashSet.Mixed Add+Contains+Remove] {Count:N0}件x3: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/(Count*3):F2}us/op)");
	}
}
// ---Int派生---
[Trait("TKey","int"),Trait("TKeyTValue","Unmanaged")]
public class BenchmarkHashSet_Int(ITestOutputHelper output):BenchmarkHashSet<int>(output){
	protected override int CreateKey(int seed)=>seed;
}
// ---String派生---
[Trait("TKey","string"),Trait("TKeyTValue","Managed")]
public class BenchmarkHashSet_String(ITestOutputHelper output):BenchmarkHashSet<string>(output){
	protected override string CreateKey(int seed)=>seed.ToString();
}
// ---Decimal派生---
[Trait("TKey","decimal"),Trait("TKeyTValue","Unmanaged")]
public class BenchmarkHashSet_Decimal(ITestOutputHelper output):BenchmarkHashSet<decimal>(output){
	protected override decimal CreateKey(int seed)=>(decimal)seed;
}
// ---UnmanagedStruct派生---
[Trait("TKey","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class BenchmarkHashSet_UnmanagedStruct(ITestOutputHelper output):BenchmarkHashSet<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateKey(int seed)=>new(){Value=seed};
}
// ---ManagedStruct派生---
[Trait("TKey","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class BenchmarkHashSet_ManagedStruct(ITestOutputHelper output):BenchmarkHashSet<ManagedStruct>(output){
	protected override ManagedStruct CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
// ---SampleClass派生---
[Trait("TKey","ManagedClass"),Trait("TKeyTValue","Managed")]
public class BenchmarkHashSet_SampleClass(ITestOutputHelper output):BenchmarkHashSet<ManagedClass>(output){
	protected override ManagedClass CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
