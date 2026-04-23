using Xunit;
using MemoryPack;
using Test.Seamless.Collections;
namespace Seamless.Collections.Tests;
public abstract class TestKeyComparer<T> where T:IEquatable<T>{
	protected abstract T Equal1{get;}
	protected abstract T Equal2{get;}
	protected abstract T Different1{get;}
	protected abstract T Different2{get;}
	[Fact]
	public void GetHashCode64_ReturnsSameHashForEqualKeys(){
		var comparer=new KeyComparer<T>();
		var k1=this.Equal1;
		var k2=this.Equal2;
		var h1=comparer.GetHashCode64(MemoryPackSerializer.Serialize(k1));
		var h2=comparer.GetHashCode64(MemoryPackSerializer.Serialize(k2));
		Assert.Equal(h1,h2);
	}
	[Fact]
	public void GetHashCode64_ReturnsDifferentHashForDifferentKeys(){
		var comparer=new KeyComparer<T>();
		var k1=this.Different1;
		var k2=this.Different2;
		var h1=comparer.GetHashCode64(MemoryPackSerializer.Serialize(k1));
		var h2=comparer.GetHashCode64(MemoryPackSerializer.Serialize(k2));
		Assert.NotEqual(h1,h2);
	}
	[Fact]
	public void Equals_ReturnsTrue_ForEqualKeys(){
		var comparer=new KeyComparer<T>();
		var k1=this.Equal1;
		var k2=this.Equal2;
		Assert.True(comparer.Equals(MemoryPackSerializer.Serialize(k1),MemoryPackSerializer.Serialize(k2)));
	}
	[Fact]
	public void Equals_ReturnsFalse_ForDifferentKeys(){
		var comparer=new KeyComparer<T>();
		var k1=this.Different1;
		var k2=this.Different2;
		Assert.False(comparer.Equals(MemoryPackSerializer.Serialize(k1),MemoryPackSerializer.Serialize(k2)));
	}
}
public class TestKeyComparer_Int:TestKeyComparer<int>{
	protected override int Equal1=>42;
	protected override int Equal2=>42;
	protected override int Different1=>1;
	protected override int Different2=>2;
}
public class TestKeyComparer_String:TestKeyComparer<string>{
	protected override string Equal1=>"hello";
	protected override string Equal2=>"hello";
	protected override string Different1=>"alpha";
	protected override string Different2=>"beta";
}
public class TestKeyComparer_Decimal:TestKeyComparer<decimal>{
	protected override decimal Equal1=>42m;
	protected override decimal Equal2=>42m;
	protected override decimal Different1=>1m;
	protected override decimal Different2=>2m;
}
public class TestKeyComparer_UnmanagedStruct:TestKeyComparer<UnmanagedStruct>{
	protected override UnmanagedStruct Equal1=>new(){Value=42};
	protected override UnmanagedStruct Equal2=>new(){Value=42};
	protected override UnmanagedStruct Different1=>new(){Value=1};
	protected override UnmanagedStruct Different2=>new(){Value=2};
}
public class TestKeyComparer_ManagedStruct:TestKeyComparer<ManagedStruct>{
	protected override ManagedStruct Equal1=>new(){Name="N42"};
	protected override ManagedStruct Equal2=>new(){Name="N42"};
	protected override ManagedStruct Different1=>new(){Name="N1"};
	protected override ManagedStruct Different2=>new(){Name="N2"};
}
public class TestKeyComparer_SampleClass:TestKeyComparer<ManagedClass>{
	protected override ManagedClass Equal1=>new(){Name="N42"};
	protected override ManagedClass Equal2=>new(){Name="N42"};
	protected override ManagedClass Different1=>new(){Name="N1"};
	protected override ManagedClass Different2=>new(){Name="N2"};
}
