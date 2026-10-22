using System.Numerics;

namespace Benchmark.Dynamic.ElementTypes;

/// <summary>
/// 256ビットの符号つき整数(2の補数)。LINQ の要素型として使うための最小の実装。
/// <c>+</c>・<c>-</c>・<c>*</c> は <c>int</c> の unchecked と同じく、桁あふれは折り返す。
/// </summary>
public readonly struct Int256:IComparable<Int256>,IComparable,IEquatable<Int256>{
    private readonly ulong limb0;
    private readonly ulong limb1;
    private readonly ulong limb2;
    private readonly ulong limb3;

    public Int256(ulong limb0,ulong limb1,ulong limb2,ulong limb3){
        this.limb0=limb0;
        this.limb1=limb1;
        this.limb2=limb2;
        this.limb3=limb3;
    }

    public static Int256 Zero=>default;
    public static Int256 One=>new(1,0,0,0);
    public static Int256 MinValue=>new(0,0,0,0x8000_0000_0000_0000UL);
    public static Int256 MaxValue=>new(ulong.MaxValue,ulong.MaxValue,ulong.MaxValue,0x7FFF_FFFF_FFFF_FFFFUL);

    public bool IsEven=>(this.limb0&1)==0;
    public bool IsNegative=>(long)this.limb3<0;

    public static implicit operator Int256(int value)=>FromInt64(value);
    public static implicit operator Int256(long value)=>FromInt64(value);

    public static Int256 FromInt64(long value){
        var extension=value<0?ulong.MaxValue:0UL;
        return new((ulong)value,extension,extension,extension);
    }

    public static Int256 operator -(Int256 value){
        // 2の補数: ビット反転して1を足す。
        return Add(new(~value.limb0,~value.limb1,~value.limb2,~value.limb3),One);
    }

    public static Int256 operator +(Int256 left,Int256 right)=>Add(left,right);

    public static Int256 operator -(Int256 left,Int256 right)=>Add(left,-right);

    public static Int256 operator *(Int256 left,Int256 right){
        // 下位256ビットだけを求める(i+j<4 の部分積だけを足す)。
        Span<ulong> result=stackalloc ulong[4];
        Span<ulong> a=[left.limb0,left.limb1,left.limb2,left.limb3];
        Span<ulong> b=[right.limb0,right.limb1,right.limb2,right.limb3];
        for(var i=0;i<4;i++){
            ulong carry=0;
            for(var j=0;i+j<4;j++){
                var high=Math.BigMul(a[i],b[j],out var low);
                var sum=result[i+j]+low;
                var overflow1=sum<low?1UL:0UL;
                var withCarry=sum+carry;
                var overflow2=withCarry<sum?1UL:0UL;
                result[i+j]=withCarry;
                carry=high+overflow1+overflow2;
            }
        }
        return new(result[0],result[1],result[2],result[3]);
    }

    public static bool operator ==(Int256 left,Int256 right)=>left.Equals(right);
    public static bool operator !=(Int256 left,Int256 right)=>!left.Equals(right);
    public static bool operator <(Int256 left,Int256 right)=>left.CompareTo(right)<0;
    public static bool operator >(Int256 left,Int256 right)=>left.CompareTo(right)>0;
    public static bool operator <=(Int256 left,Int256 right)=>left.CompareTo(right)<=0;
    public static bool operator >=(Int256 left,Int256 right)=>left.CompareTo(right)>=0;

    public int CompareTo(Int256 other){
        var high=((long)this.limb3).CompareTo((long)other.limb3);
        if(high!=0){
            return high;
        }
        var l2=this.limb2.CompareTo(other.limb2);
        if(l2!=0){
            return l2;
        }
        var l1=this.limb1.CompareTo(other.limb1);
        if(l1!=0){
            return l1;
        }
        return this.limb0.CompareTo(other.limb0);
    }

    public int CompareTo(object? obj){
        if(obj is null){
            return 1;
        }
        return obj is Int256 other
            ?this.CompareTo(other)
            :throw new ArgumentException("The object must be an Int256.",nameof(obj));
    }

    public bool Equals(Int256 other){
        return this.limb0==other.limb0&&this.limb1==other.limb1&&this.limb2==other.limb2&&this.limb3==other.limb3;
    }

    public override bool Equals(object? obj)=>obj is Int256 other&&this.Equals(other);

    public override int GetHashCode()=>HashCode.Combine(this.limb0,this.limb1,this.limb2,this.limb3);

    /// <summary>10進数の文字列。</summary>
    public override string ToString()=>this.ToBigInteger().ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>失われない16進の表記(符号つき256ビットの2の補数、64桁)。結果の比較や保存に使う。</summary>
    public string ToHex()=>$"{this.limb3:X16}{this.limb2:X16}{this.limb1:X16}{this.limb0:X16}";

    public BigInteger ToBigInteger(){
        Span<byte> bytes=stackalloc byte[32];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes,this.limb0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..],this.limb1);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes[16..],this.limb2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bytes[24..],this.limb3);
        return new BigInteger(bytes,isUnsigned:false,isBigEndian:false);
    }

    /// <summary>任意の大きさの整数を、256ビットへ折り返して作る。</summary>
    public static Int256 FromBigInteger(BigInteger value){
        var modulus=BigInteger.One<<256;
        var wrapped=((value%modulus)+modulus)%modulus;
        Span<byte> bytes=stackalloc byte[32];
        wrapped.TryWriteBytes(bytes,out _,isUnsigned:true,isBigEndian:false);
        return new(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]),
            System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]));
    }

    private static Int256 Add(Int256 left,Int256 right){
        var l0=left.limb0+right.limb0;
        var carry=l0<left.limb0?1UL:0UL;
        var l1=left.limb1+right.limb1;
        var carry1=l1<left.limb1?1UL:0UL;
        var l1WithCarry=l1+carry;
        carry1+=l1WithCarry<l1?1UL:0UL;
        var l2=left.limb2+right.limb2;
        var carry2=l2<left.limb2?1UL:0UL;
        var l2WithCarry=l2+carry1;
        carry2+=l2WithCarry<l2?1UL:0UL;
        var l3=left.limb3+right.limb3+carry2;
        return new(l0,l1WithCarry,l2WithCarry,l3);
    }
}

/// <summary>
/// <see cref="Int256"/> と同じ値を持つ参照型。参照型の要素(null 可・共有・同一性)での LINQ の経路を試すために、
/// 値の等しさ・比較・ハッシュは値で決まるようにしてある。
/// </summary>
public sealed class Int256Object:IComparable<Int256Object>,IComparable,IEquatable<Int256Object>{
    public Int256Object(Int256 value){
        this.Value=value;
    }

    public Int256 Value{get;}

    public bool IsEven=>this.Value.IsEven;

    public static implicit operator Int256Object(int value)=>new(value);

    public static Int256Object operator +(Int256Object left,Int256Object right)=>new(left.Value+right.Value);

    public static Int256Object operator *(Int256Object left,Int256Object right)=>new(left.Value*right.Value);

    public static Int256Object operator -(Int256Object value)=>new(-value.Value);

    public static bool operator <(Int256Object left,Int256Object right)=>left.CompareTo(right)<0;
    public static bool operator >(Int256Object left,Int256Object right)=>left.CompareTo(right)>0;
    public static bool operator <=(Int256Object left,Int256Object right)=>left.CompareTo(right)<=0;
    public static bool operator >=(Int256Object left,Int256Object right)=>left.CompareTo(right)>=0;

    public int CompareTo(Int256Object? other)=>other is null?1:this.Value.CompareTo(other.Value);

    public int CompareTo(object? obj){
        if(obj is null){
            return 1;
        }
        return obj is Int256Object other
            ?this.CompareTo(other)
            :throw new ArgumentException("The object must be an Int256Object.",nameof(obj));
    }

    public bool Equals(Int256Object? other)=>other is not null&&this.Value.Equals(other.Value);

    public override bool Equals(object? obj)=>obj is Int256Object other&&this.Equals(other);

    public override int GetHashCode()=>this.Value.GetHashCode();

    public override string ToString()=>this.Value.ToString();
}
