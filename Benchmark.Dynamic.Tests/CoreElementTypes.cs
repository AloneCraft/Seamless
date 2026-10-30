using System.Numerics;
using Benchmark.Dynamic.ElementTypes;

namespace Benchmark.Dynamic.Tests;

public sealed class Int256Tests{
    private static readonly BigInteger Modulus=BigInteger.One<<256;
    private static readonly BigInteger HalfModulus=BigInteger.One<<255;

    // 符号つき256ビットへ折り返した値(2の補数)。
    private static BigInteger Wrap(BigInteger value)=>((value+HalfModulus)%Modulus+Modulus)%Modulus-HalfModulus;

    private static IEnumerable<BigInteger> Samples(){
        BigInteger[] fixedValues=[
            0,1,-1,2,-2,ulong.MaxValue,(BigInteger)ulong.MaxValue+1,BigInteger.One<<127,-(BigInteger.One<<127),
            BigInteger.One<<128,BigInteger.One<<192,(BigInteger.One<<255)-1,-(BigInteger.One<<255),
        ];
        foreach(var value in fixedValues){
            yield return value;
        }
        var state=0x9E3779B97F4A7C15UL;
        for(var index=0;index<200;index++){
            var bytes=new byte[32];
            for(var offset=0;offset<32;offset+=8){
                state^=state>>12;
                state^=state<<25;
                state^=state>>27;
                BitConverter.TryWriteBytes(bytes.AsSpan(offset),state*0x2545F4914F6CDD1DUL);
            }
            // 長さを散らす(上位を落として小さい値も混ぜる)。
            var keep=1+index%32;
            for(var offset=keep;offset<32;offset++){
                bytes[offset]=(byte)((bytes[keep-1]&0x80)!=0?0xFF:0x00);
            }
            yield return new BigInteger(bytes);
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 足し算と引き算と掛け算はBigIntegerの折り返しと一致する(){
        var samples=Samples().Select(Wrap).ToArray();
        var checkedPairs=0;
        foreach(var left in samples.Take(60)){
            foreach(var right in samples.Take(60)){
                var a=Int256.FromBigInteger(left);
                var b=Int256.FromBigInteger(right);
                Assert.Equal(Wrap(left+right),(a+b).ToBigInteger());
                Assert.Equal(Wrap(left-right),(a-b).ToBigInteger());
                Assert.Equal(Wrap(left*right),(a*b).ToBigInteger());
                checkedPairs++;
            }
        }
        Assert.Equal(3600,checkedPairs);
    }

    [Fact,Trait("Category","Unit")]
    public void 比較はBigIntegerの符号つき順序と一致する(){
        var samples=Samples().Select(Wrap).ToArray();
        foreach(var left in samples.Take(80)){
            foreach(var right in samples.Take(80)){
                var a=Int256.FromBigInteger(left);
                var b=Int256.FromBigInteger(right);
                Assert.Equal(Math.Sign(left.CompareTo(right)),Math.Sign(a.CompareTo(b)));
                Assert.Equal(left==right,a==b);
                Assert.Equal(left<right,a<b);
                Assert.Equal(left>=right,a>=b);
                if(left==right){
                    Assert.Equal(a.GetHashCode(),b.GetHashCode());
                }
            }
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 境界値と変換と表記(){
        Assert.Equal(Int256.MinValue,Int256.MaxValue+Int256.One);
        Assert.Equal(Int256.MinValue,Int256.MinValue*-1);
        Assert.Equal(Int256.MaxValue,Int256.MinValue-Int256.One);
        Assert.Equal(-Int256.One,(Int256)(-1));
        Assert.Equal((BigInteger)(-5),((Int256)(-5L)).ToBigInteger());
        Assert.Equal((BigInteger)int.MinValue,((Int256)int.MinValue).ToBigInteger());
        Assert.True(Int256.Zero.IsEven);
        Assert.False(Int256.One.IsEven);
        Assert.Equal("-57896044618658097711785492504343953926634992332820282019728792003956564819968",Int256.MinValue.ToString());
        Assert.Equal(64,Int256.MaxValue.ToHex().Length);
        Assert.Equal("7FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",Int256.MaxValue.ToHex());
        Assert.Equal(Int256.FromBigInteger(BigInteger.One<<256),Int256.Zero);
    }

    [Fact,Trait("Category","Unit")]
    public void 参照型の版も値で等しく比べられる(){
        Int256Object a=5;
        Int256Object b=new(Int256.FromInt64(5));
        Assert.NotSame(a,b);
        Assert.Equal(a,b);
        Assert.Equal(a.GetHashCode(),b.GetHashCode());
        Assert.Equal(1,new HashSet<Int256Object>([a,b,new Int256Object(6)]).Count-1);
        Assert.True(a<new Int256Object(6));
        Assert.Equal(new Int256Object(25),a*b);
        Assert.Equal(new Int256Object(10),a+b);
        Assert.Equal(new Int256Object(-5),-a);
        Assert.Equal(1,a.CompareTo(null));
    }
}

public sealed class QuadrupleTests{
    private static Quadruple Bits(ulong high,ulong low)=>new(high,low);

    [Fact,Trait("Category","Unit")]
    public void 既知の定数と一致する(){
        Assert.Equal("3FFF0000000000000000000000000000",Quadruple.One.ToHex());
        Assert.Equal("40000000000000000000000000000000",((Quadruple)2).ToHex());
        Assert.Equal("C0000000000000000000000000000000",((Quadruple)(-2)).ToHex());
        Assert.Equal(Bits(0x3FFD555555555555UL,0x5555555555555555UL).ToHex(),(Quadruple.One/3).ToHex());
        Assert.Equal(Bits(0x3FFE555555555555UL,0x5555555555555555UL).ToHex(),(((Quadruple)2)/3).ToHex());
        Assert.Equal(Bits(0x3FFB999999999999UL,0x999999999999999AUL).ToHex(),(Quadruple.One/10).ToHex());
    }

    [Fact,Trait("Category","Unit")]
    public void doubleで厳密に表せる足し算と掛け算とdoubleとの往復はdoubleと同じ結果になる(){
        var state=0x2545F4914F6CDD1DUL;
        double Next(){
            state^=state>>12;
            state^=state<<25;
            state^=state>>27;
            var mantissa=(state*0x2545F4914F6CDD1DUL)>>11;
            var scaled=(mantissa/(double)(1UL<<53)+0.5)*Math.Pow(2,(int)((state>>3)%61)-30);
            return (state&1)==0?scaled:-scaled;
        }
        for(var index=0;index<3000;index++){
            var x=Next();
            var y=Next();
            var qx=(Quadruple)x;
            var qy=(Quadruple)y;
            Assert.Equal(BitConverter.DoubleToInt64Bits(x),BitConverter.DoubleToInt64Bits((double)qx));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x+y),BitConverter.DoubleToInt64Bits((double)(qx+qy)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x-y),BitConverter.DoubleToInt64Bits((double)(qx-qy)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(x*y),BitConverter.DoubleToInt64Bits((double)(qx*qy)));
            Assert.Equal(Math.Sign(x.CompareTo(y)),Math.Sign(qx.CompareTo(qy)));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 小さい整数の割り算はdoubleの割り算と同じ結果になる(){
        var state=0x9E3779B97F4A7C15UL;
        for(var index=0;index<3000;index++){
            state^=state>>12;
            state^=state<<25;
            state^=state>>27;
            var a=(int)((state>>8)%(1<<26))-(1<<25);
            var b=(int)((state>>40)%(1<<26))+1;
            if((state&1)==0){
                b=-b;
            }
            Assert.Equal(BitConverter.DoubleToInt64Bits((double)a/b),BitConverter.DoubleToInt64Bits((double)((Quadruple)a/(Quadruple)b)));
        }
    }

    [Fact,Trait("Category","Unit")]
    public void 整数との往復は厳密で大きい整数も作れる(){
        Assert.Equal("00000000000000000000000000000000",((Quadruple)0).ToHex());
        Assert.Equal((double)int.MinValue,((Quadruple)int.MinValue).ToDouble());
        Assert.Equal((double)long.MinValue,((Quadruple)long.MinValue).ToDouble());
        Assert.Equal(1e15,(double)((Quadruple)1_000_000_000_000_000L));
        // 2^113+1 は113ビットでは表せず、最近接偶数で 2^113 に丸まる。
        var big=(Quadruple)(long.MaxValue)*(Quadruple)(long.MaxValue);
        Assert.False(big.IsInfinity);
        Assert.True(big>(Quadruple)long.MaxValue);
    }

    [Fact,Trait("Category","Unit")]
    public void NaNと無限大とゼロの扱いはdoubleと同じ(){
        var nan=Quadruple.NaN;
        var otherNan=Quadruple.NaN;
        var positiveInfinity=Quadruple.PositiveInfinity;
        var negativeInfinity=Quadruple.NegativeInfinity;
        Assert.True(nan.Equals(otherNan));
        Assert.False(nan==otherNan);
        Assert.True(nan!=otherNan);
        Assert.False(nan<Quadruple.One);
        Assert.False(nan>Quadruple.One);
        Assert.Equal(nan.GetHashCode(),Bits(0xFFFF_8000_1234_0000UL,7).GetHashCode());
        Assert.True(Bits(0xFFFF_8000_1234_0000UL,7).IsNaN);
        Assert.Equal(nan.ToHex(),Bits(0xFFFF_8000_1234_0000UL,7).ToHex());

        Assert.True(Quadruple.Zero==Quadruple.NegativeZero);
        Assert.True(Quadruple.Zero.Equals(Quadruple.NegativeZero));
        Assert.Equal(Quadruple.Zero.GetHashCode(),Quadruple.NegativeZero.GetHashCode());
        Assert.NotEqual(Quadruple.Zero.ToHex(),Quadruple.NegativeZero.ToHex());

        Quadruple[] ascending=[nan,negativeInfinity,-Quadruple.One,Quadruple.Zero,Quadruple.One,positiveInfinity];
        for(var i=0;i<ascending.Length;i++){
            for(var j=0;j<ascending.Length;j++){
                Assert.Equal(Math.Sign(i.CompareTo(j)),Math.Sign(ascending[i].CompareTo(ascending[j])));
            }
        }

        Assert.True((positiveInfinity+negativeInfinity).IsNaN);
        Assert.True((Quadruple.Zero*positiveInfinity).IsNaN);
        Assert.True((Quadruple.Zero/Quadruple.Zero).IsNaN);
        Assert.True((positiveInfinity/positiveInfinity).IsNaN);
        Assert.Equal(positiveInfinity,Quadruple.One/Quadruple.Zero);
        Assert.Equal(negativeInfinity,-Quadruple.One/Quadruple.Zero);
        Assert.Equal(Quadruple.Zero,Quadruple.One/positiveInfinity);
        Assert.Equal(positiveInfinity,positiveInfinity+Quadruple.One);
        Assert.True((nan+Quadruple.One).IsNaN);
    }

    [Fact,Trait("Category","Unit")]
    public void 範囲の外は無限大か0になり非正規化数は作らない(){
        Assert.Equal(Quadruple.PositiveInfinity,Quadruple.MaxValue*(Quadruple)2);
        Assert.Equal(Quadruple.PositiveInfinity,Quadruple.MaxValue+Quadruple.MaxValue);
        Assert.Equal(Quadruple.NegativeInfinity,-Quadruple.MaxValue*(Quadruple)2);
        var smallestNormal=Bits(0x0001_0000_0000_0000UL,0);
        Assert.False(smallestNormal.IsZero);
        Assert.True((smallestNormal/(Quadruple)2).IsZero);
        Assert.True((smallestNormal*Quadruple.One/(Quadruple)2).IsZero);
        Assert.Equal(smallestNormal.ToHex(),(smallestNormal*Quadruple.One).ToHex());
    }

    [Fact,Trait("Category","Unit")]
    public void ハッシュ表ではNaNと正負のゼロがそれぞれ1つにまとまる(){
        var set=new HashSet<Quadruple>([
            Quadruple.NaN,Bits(0xFFFF_8000_0000_0001UL,0),Quadruple.Zero,Quadruple.NegativeZero,Quadruple.One,Quadruple.One+Quadruple.Zero]);
        Assert.Equal(3,set.Count);
    }

    [Fact,Trait("Category","Unit")]
    public void 参照型の版も値で等しく比べられる(){
        QuadrupleObject a=3;
        var b=new QuadrupleObject((Quadruple)3);
        Assert.NotSame(a,b);
        Assert.Equal(a,b);
        Assert.Equal(a.GetHashCode(),b.GetHashCode());
        Assert.Equal(new QuadrupleObject((Quadruple)9),a*b);
        Assert.Equal(new QuadrupleObject((Quadruple)6),a+b);
        Assert.True(a<new QuadrupleObject((Quadruple)4));
        Assert.Equal(1,a.CompareTo(null));
        Assert.Equal(new QuadrupleObject(Quadruple.NaN),new QuadrupleObject(Quadruple.NaN));
    }
}

public sealed class ElementEntityTests{
    [Fact,Trait("Category","Unit")]
    public void Orderは値で等しくIComparableではない(){
        var a=new Order(1,2,3.50m);
        var b=new Order(1,2,3.50m);
        Assert.NotSame(a,b);
        Assert.Equal(a,b);
        Assert.Equal(a.GetHashCode(),b.GetHashCode());
        Assert.NotEqual(a,new Order(1,2,3.5m+1));
        Assert.False(typeof(IComparable).IsAssignableFrom(typeof(Order)));
    }

    [Fact,Trait("Category","Unit")]
    public void CollidingKeyはハッシュが衝突しても等しくない値を区別する(){
        var keys=Enumerable.Range(0,16).Select(static value=>new CollidingKey(value)).ToArray();
        Assert.Equal(4,keys.Select(static key=>key.GetHashCode()).Distinct().Count());
        Assert.Equal(16,new HashSet<CollidingKey>(keys).Count);
        Assert.Equal(new CollidingKey(1).GetHashCode(),new CollidingKey(5).GetHashCode());
        Assert.NotEqual(new CollidingKey(1),new CollidingKey(5));
        Assert.True(new CollidingKey(1).CompareTo(new CollidingKey(5))<0);
        Assert.Equal(keys.Length,keys.Distinct().Count());
    }
}
