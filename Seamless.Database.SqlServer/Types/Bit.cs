namespace Seamless.Database.SqlServer.Types;

/// <summary>0または1を保持するSQL Serverのbit値。</summary>
public readonly struct Bit:
    global::System.IComparable<Bit>,
    global::System.IEquatable<Bit> {
    private readonly byte value;

    public Bit(sbyte value):this((long)value) { }

    public Bit(byte value):this((ulong)value) { }

    public Bit(short value):this((long)value) { }

    public Bit(ushort value):this((ulong)value) { }

    /// <summary>0を0、それ以外の整数を1として構築する。</summary>
    public Bit(int value):this((long)value) { }

    public Bit(uint value):this((ulong)value) { }

    /// <summary>0を0、それ以外の符号付き整数を1として構築する。</summary>
    public Bit(long value) {
        this.value=value==0?(byte)0:(byte)1;
    }

    /// <summary>0を0、それ以外の符号なし整数を1として構築する。</summary>
    public Bit(ulong value) {
        this.value=value==0?(byte)0:(byte)1;
    }

    /// <summary>0。</summary>
    public static Bit Zero=>default;

    /// <summary>1。</summary>
    public static Bit One=>new(1);

    /// <summary>正規化済みの0または1。</summary>
    public byte Value=>this.value;

    public int CompareTo(Bit other)=>this.value.CompareTo(other.value);

    public bool Equals(Bit other)=>this.value==other.value;

    public override bool Equals(object? value)=>value is Bit other&&this.Equals(other);

    public override int GetHashCode()=>this.value;

    public override string ToString()=>this.value==0?"0":"1";

    public static explicit operator Bit(sbyte value)=>new(value);

    public static explicit operator Bit(byte value)=>new(value);

    public static explicit operator Bit(short value)=>new(value);

    public static explicit operator Bit(ushort value)=>new(value);

    public static explicit operator Bit(int value)=>new(value);

    public static explicit operator Bit(uint value)=>new(value);

    public static explicit operator Bit(long value)=>new(value);

    public static explicit operator Bit(ulong value)=>new(value);

    public static explicit operator int(Bit value)=>value.value;

    public static Bit operator &(Bit left,Bit right)=>new(left.value&right.value);

    public static Bit operator |(Bit left,Bit right)=>new(left.value|right.value);

    public static Bit operator ^(Bit left,Bit right)=>new(left.value^right.value);

    public static Bit operator ~(Bit value)=>value.value==0?One:Zero;

    public static Bit operator +(Bit value)=>value;

    public static bool operator ==(Bit left,Bit right)=>left.Equals(right);

    public static bool operator !=(Bit left,Bit right)=>!left.Equals(right);

    public static bool operator <(Bit left,Bit right)=>left.value<right.value;

    public static bool operator <=(Bit left,Bit right)=>left.value<=right.value;

    public static bool operator >(Bit left,Bit right)=>left.value>right.value;

    public static bool operator >=(Bit left,Bit right)=>left.value>=right.value;
}
