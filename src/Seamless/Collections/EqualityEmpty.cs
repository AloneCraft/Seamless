namespace Seamless.Collections;

#pragma warning disable CS0660, CS0661
public readonly struct EqualityEmpty {
    /// <summary>
    /// Default
    /// </summary>
    public static readonly EqualityEmpty Default = default;
    public static bool operator ==(EqualityEmpty left,EqualityEmpty right) => true;
    public static bool operator !=(EqualityEmpty left,EqualityEmpty right) => false;
}
#pragma warning restore CS0660, CS0661
