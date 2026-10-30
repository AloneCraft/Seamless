namespace Seamless.TestKit;
[MemoryPack.MemoryPackable]
public sealed partial class ClassInt32:IEquatable<ClassInt32>, IComparable<ClassInt32>, global::System.Numerics.IEqualityOperators<ClassInt32,ClassInt32,bool> {
    public int Value { get; init; }
    public bool Equals(ClassInt32? other) => other is not null&&this.Value==other.Value;
    public int CompareTo(ClassInt32? other) => other is null ? 1 : this.Value.CompareTo(other.Value);
    public override bool Equals(object? obj) => obj is ClassInt32 value&&this.Equals(value);
    public override int GetHashCode() => this.Value;
    public override string ToString() => this.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
    public static bool operator ==(ClassInt32? left,ClassInt32? right) => left?.Equals(right)??right is null;
    public static bool operator !=(ClassInt32? left,ClassInt32? right) => !(left==right);
}
