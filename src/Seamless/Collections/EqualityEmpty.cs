namespace Seamless.Collections;

public readonly struct EqualityEmpty:IEquatable<EqualityEmpty>{
    /// <summary>
    /// Default
    /// </summary>
    public static readonly EqualityEmpty Default=default;
    public bool Equals(EqualityEmpty other)=>true;
    public override bool Equals(object? obj)=>true;
    public override int GetHashCode()=>0;
    public static bool operator==(EqualityEmpty left,EqualityEmpty right)=>true;
    public static bool operator!=(EqualityEmpty left,EqualityEmpty right)=>false;
}
