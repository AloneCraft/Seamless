using System.Numerics;

namespace Benchmark.Dynamic.ElementTypes;

/// <summary>
/// 4倍精度(IEEE 754 binary128)の浮動小数点数。LINQ の要素型として使うための最小の実装で、
/// 足し算・引き算・掛け算・割り算・比較・int と double との変換だけを持つ。
/// 丸めは最近接偶数。非正規化数は作らず、計算結果が正規化数の最小より小さければ 0 にする(入力の非正規化数も 0 と扱う)。
/// 計算は BigInteger で厳密に行ってから丸めるので、速さは要らない(正しさを優先)。
/// </summary>
/// <remarks>
/// <c>Equals</c> は NaN 同士・+0 と -0 を等しいとし、<c>GetHashCode</c> もそれに合わせる(<c>double</c> と同じ)。
/// 演算子 <c>==</c> は IEEE どおり NaN を含むと false。<see cref="CompareTo(Quadruple)"/> は NaN を最小とする。
/// </remarks>
public readonly struct Quadruple:IComparable<Quadruple>,IComparable,IEquatable<Quadruple>{
    private const int FractionBits=112;
    private const int ExponentBias=16383;
    private const int MaximumBiasedExponent=0x7FFE;
    private const ulong SignMask=0x8000_0000_0000_0000UL;
    private const ulong HighFractionMask=0x0000_FFFF_FFFF_FFFFUL;

    private readonly ulong high;
    private readonly ulong low;

    public Quadruple(ulong high,ulong low){
        this.high=high;
        this.low=low;
    }

    public static Quadruple Zero=>default;
    public static Quadruple NegativeZero=>new(SignMask,0);
    public static Quadruple One=>FromInt64(1);
    public static Quadruple NaN=>new(0x7FFF_8000_0000_0000UL,0);
    public static Quadruple PositiveInfinity=>new(0x7FFF_0000_0000_0000UL,0);
    public static Quadruple NegativeInfinity=>new(0xFFFF_0000_0000_0000UL,0);
    public static Quadruple MaxValue=>new(0x7FFE_FFFF_FFFF_FFFFUL,ulong.MaxValue);

    public ulong HighBits=>this.high;
    public ulong LowBits=>this.low;

    private bool Sign=>(this.high&SignMask)!=0;
    private int BiasedExponent=>(int)((this.high>>48)&0x7FFF);
    private bool FractionIsZero=>(this.high&HighFractionMask)==0&&this.low==0;

    public bool IsNaN=>this.BiasedExponent==0x7FFF&&!this.FractionIsZero;
    public bool IsInfinity=>this.BiasedExponent==0x7FFF&&this.FractionIsZero;
    public bool IsNegative=>this.Sign;

    /// <summary>0(非正規化数のビット列も 0 と扱う)。</summary>
    public bool IsZero=>this.BiasedExponent==0;

    public static implicit operator Quadruple(int value)=>FromInt64(value);
    public static implicit operator Quadruple(long value)=>FromInt64(value);

    public static Quadruple FromInt64(long value){
        if(value==0){
            return Zero;
        }
        var negative=value<0;
        var magnitude=negative?-(BigInteger)value:(BigInteger)value;
        return Round(negative,magnitude,0);
    }

    public static Quadruple FromDouble(double value){
        if(double.IsNaN(value)){
            return NaN;
        }
        var bits=BitConverter.DoubleToUInt64Bits(value);
        var negative=(bits&SignMask)!=0;
        var exponent=(int)((bits>>52)&0x7FF);
        var fraction=bits&0x000F_FFFF_FFFF_FFFFUL;
        if(exponent==0x7FF){
            return negative?NegativeInfinity:PositiveInfinity;
        }
        if(exponent==0){
            return fraction==0
                ?(negative?NegativeZero:Zero)
                :Round(negative,fraction,-1074);
        }
        return Round(negative,fraction|0x0010_0000_0000_0000UL,exponent-1075);
    }

    public static explicit operator Quadruple(double value)=>FromDouble(value);

    public double ToDouble(){
        if(this.IsNaN){
            return double.NaN;
        }
        if(this.IsInfinity){
            return this.Sign?double.NegativeInfinity:double.PositiveInfinity;
        }
        if(this.IsZero){
            return this.Sign?-0.0:0.0;
        }
        Unpack(this,out var negative,out var significand,out var exponent);
        // 53ビットへ最近接偶数で丸める。値は significand * 2^(exponent-112)。
        var shift=FractionBits+1-53;
        var remainder=significand&((BigInteger.One<<shift)-1);
        var quotient=significand>>shift;
        var half=BigInteger.One<<(shift-1);
        if(remainder>half||(remainder==half&&!quotient.IsEven)){
            quotient+=1;
        }
        var result=Math.ScaleB((double)(ulong)quotient,exponent-52);
        return negative?-result:result;
    }

    public static explicit operator double(Quadruple value)=>value.ToDouble();

    public static Quadruple operator -(Quadruple value){
        return value.IsNaN?NaN:new(value.high^SignMask,value.low);
    }

    public static Quadruple operator +(Quadruple left,Quadruple right){
        if(left.IsNaN||right.IsNaN){
            return NaN;
        }
        if(left.IsInfinity||right.IsInfinity){
            if(left.IsInfinity&&right.IsInfinity&&left.Sign!=right.Sign){
                return NaN;
            }
            return left.IsInfinity?left:right;
        }
        if(left.IsZero&&right.IsZero){
            return left.Sign&&right.Sign?NegativeZero:Zero;
        }
        if(left.IsZero){
            return right;
        }
        if(right.IsZero){
            return left;
        }
        Unpack(left,out var leftNegative,out var leftSignificand,out var leftExponent);
        Unpack(right,out var rightNegative,out var rightSignificand,out var rightExponent);
        var smallest=Math.Min(leftExponent,rightExponent);
        var exact=(leftNegative?-leftSignificand:leftSignificand)<<(leftExponent-smallest);
        exact+=(rightNegative?-rightSignificand:rightSignificand)<<(rightExponent-smallest);
        if(exact.IsZero){
            return Zero;
        }
        var negative=exact.Sign<0;
        return Round(negative,BigInteger.Abs(exact),smallest-FractionBits);
    }

    public static Quadruple operator -(Quadruple left,Quadruple right)=>left+(-right);

    public static Quadruple operator *(Quadruple left,Quadruple right){
        if(left.IsNaN||right.IsNaN){
            return NaN;
        }
        var negative=left.Sign!=right.Sign;
        if(left.IsInfinity||right.IsInfinity){
            return left.IsZero||right.IsZero?NaN:(negative?NegativeInfinity:PositiveInfinity);
        }
        if(left.IsZero||right.IsZero){
            return negative?NegativeZero:Zero;
        }
        Unpack(left,out _,out var leftSignificand,out var leftExponent);
        Unpack(right,out _,out var rightSignificand,out var rightExponent);
        return Round(
            negative,
            leftSignificand*rightSignificand,
            (leftExponent-FractionBits)+(rightExponent-FractionBits));
    }

    public static Quadruple operator /(Quadruple left,Quadruple right){
        if(left.IsNaN||right.IsNaN){
            return NaN;
        }
        var negative=left.Sign!=right.Sign;
        if(left.IsInfinity){
            return right.IsInfinity?NaN:(negative?NegativeInfinity:PositiveInfinity);
        }
        if(right.IsInfinity){
            return negative?NegativeZero:Zero;
        }
        if(right.IsZero){
            return left.IsZero?NaN:(negative?NegativeInfinity:PositiveInfinity);
        }
        if(left.IsZero){
            return negative?NegativeZero:Zero;
        }
        Unpack(left,out _,out var leftSignificand,out var leftExponent);
        Unpack(right,out _,out var rightSignificand,out var rightExponent);
        // 商を230ビット余分に求め、余りがあれば最下位に1を足して(スティッキー)丸めに使う。
        const int Extra=230;
        var quotient=BigInteger.DivRem(leftSignificand<<Extra,rightSignificand,out var remainder);
        quotient=(quotient<<1)|(remainder.IsZero?BigInteger.Zero:BigInteger.One);
        return Round(negative,quotient,(leftExponent-rightExponent)-Extra-1);
    }

    public static bool operator ==(Quadruple left,Quadruple right)=>!left.IsNaN&&!right.IsNaN&&Compare(left,right)==0;
    public static bool operator !=(Quadruple left,Quadruple right)=>!(left==right);
    public static bool operator <(Quadruple left,Quadruple right)=>!left.IsNaN&&!right.IsNaN&&Compare(left,right)<0;
    public static bool operator >(Quadruple left,Quadruple right)=>!left.IsNaN&&!right.IsNaN&&Compare(left,right)>0;
    public static bool operator <=(Quadruple left,Quadruple right)=>!left.IsNaN&&!right.IsNaN&&Compare(left,right)<=0;
    public static bool operator >=(Quadruple left,Quadruple right)=>!left.IsNaN&&!right.IsNaN&&Compare(left,right)>=0;

    public int CompareTo(Quadruple other)=>Compare(this,other);

    public int CompareTo(object? obj){
        if(obj is null){
            return 1;
        }
        return obj is Quadruple other
            ?this.CompareTo(other)
            :throw new ArgumentException("The object must be a Quadruple.",nameof(obj));
    }

    public bool Equals(Quadruple other)=>Compare(this,other)==0;

    public override bool Equals(object? obj)=>obj is Quadruple other&&this.Equals(other);

    public override int GetHashCode(){
        if(this.IsNaN){
            return NaN.high.GetHashCode();
        }
        if(this.IsZero){
            return 0;
        }
        return HashCode.Combine(this.high,this.low);
    }

    /// <summary>失われない16進の表記(32桁)。NaN は1通りに、±0 は区別して書く。結果の比較や保存に使う。</summary>
    public string ToHex(){
        var canonical=this.IsNaN?NaN:this;
        return $"{canonical.high:X16}{canonical.low:X16}";
    }

    public override string ToString(){
        if(this.IsNaN){
            return "NaN";
        }
        if(this.IsInfinity){
            return this.Sign?"-Infinity":"Infinity";
        }
        return this.ToDouble().ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"q";
    }

    // NaN を最小、+0 と -0 は等しいとする全順序(double.CompareTo と同じ)。
    private static int Compare(Quadruple left,Quadruple right){
        if(left.IsNaN){
            return right.IsNaN?0:-1;
        }
        if(right.IsNaN){
            return 1;
        }
        if(left.IsZero&&right.IsZero){
            return 0;
        }
        if(left.Sign!=right.Sign){
            return left.Sign?-1:1;
        }
        var magnitude=Magnitude(left).CompareTo(Magnitude(right));
        return left.Sign?-magnitude:magnitude;
    }

    private static UInt128 Magnitude(Quadruple value){
        return (new UInt128(value.high&~SignMask,value.low));
    }

    // 正規化数を (符号, 113ビットの仮数, 非バイアスの指数) にする。値は 仮数 * 2^(指数-112)。
    private static void Unpack(Quadruple value,out bool negative,out BigInteger significand,out int exponent){
        negative=value.Sign;
        var fraction=((new BigInteger(value.high&HighFractionMask))<<64)|new BigInteger(value.low);
        significand=fraction|(BigInteger.One<<FractionBits);
        exponent=value.BiasedExponent-ExponentBias;
    }

    // 値 magnitude * 2^exponent2 を、113ビットの仮数へ最近接偶数で丸めて作る。
    private static Quadruple Round(bool negative,BigInteger magnitude,int exponent2){
        if(magnitude.IsZero){
            return negative?NegativeZero:Zero;
        }
        var length=(int)magnitude.GetBitLength();
        BigInteger significand;
        if(length>FractionBits+1){
            var shift=length-(FractionBits+1);
            var remainder=magnitude&((BigInteger.One<<shift)-1);
            significand=magnitude>>shift;
            var half=BigInteger.One<<(shift-1);
            if(remainder>half||(remainder==half&&!significand.IsEven)){
                significand+=1;
            }
            exponent2+=shift;
            if(significand==(BigInteger.One<<(FractionBits+1))){
                significand>>=1;
                exponent2++;
            }
        }else{
            significand=magnitude<<(FractionBits+1-length);
            exponent2-=FractionBits+1-length;
        }
        var exponent=exponent2+FractionBits;
        if(exponent>MaximumBiasedExponent-ExponentBias){
            return negative?NegativeInfinity:PositiveInfinity;
        }
        if(exponent<1-ExponentBias){
            return negative?NegativeZero:Zero;
        }
        var fraction=significand&((BigInteger.One<<FractionBits)-1);
        var fractionHigh=(ulong)(fraction>>64);
        var fractionLow=(ulong)(fraction&ulong.MaxValue);
        var biased=(ulong)(exponent+ExponentBias);
        return new((negative?SignMask:0UL)|(biased<<48)|fractionHigh,fractionLow);
    }
}

/// <summary><see cref="Quadruple"/> と同じ値を持つ参照型。値の等しさ・比較・ハッシュは値で決まる。</summary>
public sealed class QuadrupleObject:IComparable<QuadrupleObject>,IComparable,IEquatable<QuadrupleObject>{
    public QuadrupleObject(Quadruple value){
        this.Value=value;
    }

    public Quadruple Value{get;}

    public static implicit operator QuadrupleObject(int value)=>new(value);

    public static QuadrupleObject operator +(QuadrupleObject left,QuadrupleObject right)=>new(left.Value+right.Value);

    public static QuadrupleObject operator *(QuadrupleObject left,QuadrupleObject right)=>new(left.Value*right.Value);

    public static QuadrupleObject operator -(QuadrupleObject value)=>new(-value.Value);

    public static bool operator <(QuadrupleObject left,QuadrupleObject right)=>left.Value<right.Value;
    public static bool operator >(QuadrupleObject left,QuadrupleObject right)=>left.Value>right.Value;
    public static bool operator <=(QuadrupleObject left,QuadrupleObject right)=>left.Value<=right.Value;
    public static bool operator >=(QuadrupleObject left,QuadrupleObject right)=>left.Value>=right.Value;

    public int CompareTo(QuadrupleObject? other)=>other is null?1:this.Value.CompareTo(other.Value);

    public int CompareTo(object? obj){
        if(obj is null){
            return 1;
        }
        return obj is QuadrupleObject other
            ?this.CompareTo(other)
            :throw new ArgumentException("The object must be a QuadrupleObject.",nameof(obj));
    }

    public bool Equals(QuadrupleObject? other)=>other is not null&&this.Value.Equals(other.Value);

    public override bool Equals(object? obj)=>obj is QuadrupleObject other&&this.Equals(other);

    public override int GetHashCode()=>this.Value.GetHashCode();

    public override string ToString()=>this.Value.ToString();
}
