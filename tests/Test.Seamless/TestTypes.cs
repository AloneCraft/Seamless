using System.Numerics;
using MemoryPack;
namespace Test.Seamless.Collections;

public struct UnmanagedStruct:IEquatable<UnmanagedStruct>,IEqualityOperators<UnmanagedStruct,UnmanagedStruct,bool>,IComparable<UnmanagedStruct>{
    public int Value;
    public bool Equals(UnmanagedStruct other)=>this.Value==other.Value;
    public int CompareTo(UnmanagedStruct other)=>this.Value.CompareTo(other.Value);
    public override bool Equals(object? obj)=>obj is UnmanagedStruct s&&this.Equals(s);
    public override int GetHashCode()=>HashCode.Combine(this.Value);
    public override string ToString()=>$"U{this.Value}";
    public static bool operator==(UnmanagedStruct left,UnmanagedStruct right)=>left.Equals(right);
    public static bool operator!=(UnmanagedStruct left,UnmanagedStruct right)=>!left.Equals(right);
}
[MemoryPackable]
public partial struct ManagedStruct:IEquatable<ManagedStruct>,IEqualityOperators<ManagedStruct,ManagedStruct,bool>,IComparable<ManagedStruct>{
    public string Name;
    public bool Equals(ManagedStruct other)=>this.Name==other.Name;
    public int CompareTo(ManagedStruct other)=>string.Compare(this.Name,other.Name,StringComparison.Ordinal);
    public override bool Equals(object? obj)=>obj is ManagedStruct s&&this.Equals(s);
    public override int GetHashCode()=>HashCode.Combine(this.Name);
    public override string ToString()=>this.Name;
    public static bool operator==(ManagedStruct left,ManagedStruct right)=>left.Equals(right);
    public static bool operator!=(ManagedStruct left,ManagedStruct right)=>!left.Equals(right);
}
[MemoryPackable]
public sealed partial class ManagedClass:IEquatable<ManagedClass>,IEqualityOperators<ManagedClass,ManagedClass,bool>,IComparable<ManagedClass>{
    public required string Name{get; init;}
    public bool Equals(ManagedClass? other)=>other!=null&&this.Name==other.Name;
    public int CompareTo(ManagedClass? other)=>string.Compare(this.Name,other?.Name,StringComparison.Ordinal);
    public override bool Equals(object? obj)=>obj is ManagedClass c&&this.Equals(c);
    public override int GetHashCode()=>HashCode.Combine(this.Name);
    public override string ToString()=>this.Name;
    public static bool operator==(ManagedClass? left,ManagedClass? right)=>left?.Equals(right)??right is null;
    public static bool operator!=(ManagedClass? left,ManagedClass? right)=>!(left==right);
}
