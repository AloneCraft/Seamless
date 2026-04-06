using Seamless.Collections;

using System.Diagnostics;

using Test.Seamless.Collections;
using Tsavorite.core;
using Empty=Seamless.Collections.Empty;
namespace Benchmarks;
public abstract class HashSet<T>(ITestOutputHelper output):Benchmark<T,Empty>(output) where T:IEquatable<T>{
	protected override Empty CreateValue(int seed)=>default;
    protected HashSetStore<T> CreateStore(){
        var MemorySize=Seamless.Const.LogMemorySize<<10;
        return Store.Create<T>(IndexSize:IndexSize,MemorySize:MemorySize);
    }
    [Fact, Trait("Category",Unit)]
    public void ログファイルが作られる(){
        const long Count=1L<<12;
        var CurrentDirectory=Environment.CurrentDirectory;
        Console.WriteLine($"CurrentDirectory: {CurrentDirectory}");
        var CheckpointDir=@$"{CurrentDirectory}\Checkpoint";
        var Log=@$"{CurrentDirectory}\Log\Table1";
        var ObjectLog=@$"{CurrentDirectory}\ObjectLog\Table1";
        Console.WriteLine($"Log: {Log}");
        Console.WriteLine($"ObjectLog: {ObjectLog}");
        using var LogDevice=Devices.CreateLogDevice(Log,DeviceType.Default,false,false,-1,false,false,true,false);
        using var ObjectLogDevice=Devices.CreateLogDevice(ObjectLog,DeviceType.Default,false,false,-1,false,false,true,false);
        const int PageSize=4096,MemorySize=8192;
        const double MutableFraction=(double)PageSize/MemorySize;
        using var store = Store.Create<int>(LogDevice:LogDevice,ObjectLogDevice:ObjectLogDevice,CheckpointDir:CheckpointDir,MutableFraction:MutableFraction,IndexSize:Count,PageSize:PageSize,MemorySize:MemorySize);
        //$$\text{MutableFraction} = \frac{\text{PageSize}}{\text{LogMemorySize}}$$/
        using var h= store.Create();
        var s=Stopwatch.StartNew();
        var ms=s.ElapsedMilliseconds;
        var List=new List<long>();
        for(var a=0;a<Count;a++){
            if(a%(1<<12)==0){
                var next=s.ElapsedMilliseconds;
                List.Add(next-ms);
                Trace.WriteLine($"Elapsed: {next-ms}ms,a:{a},{(double)a/Count}");
                ms =next;
            }
            h.Add(a);
        }
    }
	[Fact,Trait("Category","")]
	public void _Add_10万件の追加時間を計測する(){
		var sw=new Stopwatch();
		using var store=this.CreateStore();
		using var set=store.Create();
		sw.Start();
        for(var i=0;i<Count;i++)
            set.Add(this.CreateKey(i));
        sw.Stop();
        Assert.Equal(Count,set.GetLongCount());
		this.Write($"[HashSet.Add] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _Contains_10万件の検索時間を計測する(){
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		var sw=Stopwatch.StartNew();
		for(var i=0;i<Count;i++)
			_=set.Contains(this.CreateKey(i));
		sw.Stop();
		this.Write($"[HashSet.Contains] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _Remove_10万件の削除時間を計測する(){
        var sw=new Stopwatch();
        using var store=this.CreateStore();
        using var set=store.Create();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		sw.Start();
		for(var i=0;i<Count;i++)
			set.Remove(this.CreateKey(i));
		sw.Stop();
		Assert.Equal(0,set.GetLongCount());
		this.Write($"[HashSet.Remove] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _GetEnumerator_10万件の列挙時間を計測する(){
		using var store=this.CreateStore();
		using var set=store.Create();
		for(var i=0;i<Count;i++)
			set.Add(this.CreateKey(i));
		var sw=Stopwatch.StartNew();
        foreach(var item in set){}
        sw.Stop();
		this.Write($"[HashSet.GetEnumerator] {Count:N0}件: {sw.ElapsedMilliseconds}ms ({sw.Elapsed.TotalMicroseconds/Count:F2}us/op)");
	}
	[Fact]
	[Trait("Category","")]
	public void _UnionWith_10万件同士の和集合時間を計測する(){
        var sw=new Stopwatch();
        using var store1=this.CreateStore();
        using var store2=this.CreateStore();
        using var set1=store1.Create();
        using var set2=store2.Create();
        for(var i=0;i<Count;i++)
            set1.Add(this.CreateKey(i));
		for(var i=Count/2;i<Count+Count/2;i++)
			set2.Add(this.CreateKey(i));
		sw.Start();
		set1.UnionWith(set2);
		sw.Stop();
		this.Write($"[HashSet.UnionWith] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms");
	}
	[Fact]
	[Trait("Category","")]
	public void _IntersectWith_10万件同士の積集合時間を計測する(){
        var sw=new Stopwatch();
        using var store1=this.CreateStore();
        using var store2=this.CreateStore();
        using var set1=store1.Create();
        using var set2=store2.Create();
        for(var i=0;i<Count;i++)
            set1.Add(this.CreateKey(i));
		for(var i=Count/2;i<Count+Count/2;i++)
			set2.Add(this.CreateKey(i));
		sw.Start();
		set1.IntersectWith(set2);
		sw.Stop();
        this.Write($"[HashSet.IntersectWith] {Count:N0}件x2: {sw.ElapsedMilliseconds}ms");
	}
	[Fact]
	[Trait("Category","")]
	public void _Mixed_10万件のAdd_Contains_Remove混合時間を計測する(){
		using var store=this.CreateStore();
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
public class HashSet_Int(ITestOutputHelper output):HashSet<int>(output){
	protected override int CreateKey(int seed)=>seed;
}
// ---String派生---
[Trait("TKey","string"),Trait("TKeyTValue","Managed")]
public class HashSet_String(ITestOutputHelper output):HashSet<string>(output){
	protected override string CreateKey(int seed)=>seed.ToString();
}
// ---Decimal派生---
[Trait("TKey","decimal"),Trait("TKeyTValue","Unmanaged")]
public class HashSet_Decimal(ITestOutputHelper output):HashSet<decimal>(output){
	protected override decimal CreateKey(int seed)=>(decimal)seed;
}
// ---UnmanagedStruct派生---
[Trait("TKey","UnmanagedStruct"),Trait("TKeyTValue","Unmanaged")]
public class HashSet_UnmanagedStruct(ITestOutputHelper output):HashSet<UnmanagedStruct>(output){
	protected override UnmanagedStruct CreateKey(int seed)=>new(){Value=seed};
}
// ---ManagedStruct派生---
[Trait("TKey","ManagedStruct"),Trait("TKeyTValue","Managed")]
public class HashSet_ManagedStruct(ITestOutputHelper output):HashSet<ManagedStruct>(output){
	protected override ManagedStruct CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
// ---SampleClass派生---
[Trait("TKey","ManagedClass"),Trait("TKeyTValue","Managed")]
public class HashSet_SampleClass(ITestOutputHelper output):HashSet<ManagedClass>(output){
	protected override ManagedClass CreateKey(int seed)=>new(){Name=$"N{seed}"};
}
