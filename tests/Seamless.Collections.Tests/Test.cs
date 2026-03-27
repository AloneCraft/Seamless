using Xunit.Abstractions;
namespace Seamless.Collections.Tests;
public abstract class Test<TKey,TValue>(ITestOutputHelper? output=null)
    where TKey:IEquatable<TKey> 
    where TValue:notnull{
    protected abstract double MutableFraction{get;}
    protected abstract TKey CreateKey(int seed);
    protected abstract TValue CreateValue(int seed);
    protected void Write(string message)=>output?.WriteLine(message);
    protected const int Count=64;
}
//[Collection("Sequential")]
//public abstract class Benchmark<TKey,TValue>(ITestOutputHelper output):Test<TKey,TValue>(output) 
//    where TKey:IEquatable<TKey> 
//    where TValue:notnull{
//    protected override double MutableFraction=>1.0;
//}
/*
public struct UnmanagedStruct:IEquatable<UnmanagedStruct>,IEqualityOperators<UnmanagedStruct,UnmanagedStruct,bool>{
    public int Value;
    public bool Equals(UnmanagedStruct other)=>
        this.Value==other.Value;
    public override bool Equals(object? obj)=>obj is UnmanagedStruct s&&this.Equals(s);
    public override int GetHashCode()=>HashCode.Combine(this.Value);
    public override string ToString()=>$"U{this.Value}";
    public static bool operator==(UnmanagedStruct left,UnmanagedStruct right)=>left.Equals(right);
    public static bool operator!=(UnmanagedStruct left,UnmanagedStruct right)=>!left.Equals(right);
}
public struct ManagedStruct:IEquatable<ManagedStruct>,IEqualityOperators<ManagedStruct,ManagedStruct,bool>{
    public string Name;
    public bool Equals(ManagedStruct other)=>
        this.Name==other.Name;
    public override bool Equals(object? obj)=>obj is ManagedStruct s&&this.Equals(s);
    public override int GetHashCode()=>HashCode.Combine(this.Name);
    public override string ToString()=>this.Name;
    public static bool operator==(ManagedStruct left,ManagedStruct right)=>left.Equals(right);
    public static bool operator!=(ManagedStruct left,ManagedStruct right)=>!left.Equals(right);
}
public sealed class ManagedClass:IEquatable<ManagedClass>,IEqualityOperators<ManagedClass,ManagedClass,bool>{
    public required string Name{get;init;}
    public bool Equals(ManagedClass? other)=>other!=null&&this.Name==other.Name;
    public override bool Equals(object? obj)=>obj is ManagedClass c&&this.Equals(c);
    public override int GetHashCode()=>HashCode.Combine(this.Name);
    public override string ToString()=>this.Name;
    public static bool operator==(ManagedClass? left,ManagedClass? right)=>left?.Equals(right)??right is null;
    public static bool operator!=(ManagedClass? left,ManagedClass? right)=>!(left==right);
}
*/