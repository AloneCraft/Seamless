namespace Seamless.Collections;
public readonly struct Empty:IEquatable<Empty> {
	/// <summary>
	/// Default
	/// </summary>
	public static readonly Empty Default = default;
	public bool Equals(Empty other) => true;
	public override bool Equals(object? obj) => true;
	public override int GetHashCode() => 0;
	public static bool operator ==(Empty left,Empty right) => true;
	public static bool operator !=(Empty left,Empty right) => false;
}
