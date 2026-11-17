using System.Globalization;
using Seamless.Collections;

namespace Test.Seamless.Database.Records;

public sealed class ScalarNormalizationTests{
    [Fact,Trait("Category","Unit")]
    public void FloatingPoint_NormalizesZeroAndNanWithoutChangingOtherValues(){
        Assert.Equal(0,BitConverter.SingleToInt32Bits(ScalarNormalization.Normalize(BitConverter.Int32BitsToSingle(int.MinValue))));
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(ScalarNormalization.Normalize(BitConverter.Int64BitsToDouble(long.MinValue))));
        Assert.Equal(0,BitConverter.HalfToInt16Bits(ScalarNormalization.Normalize(BitConverter.Int16BitsToHalf(short.MinValue))));
        foreach(var bits in new[]{0x7f800001,0x7fc12345,unchecked((int)0xff800001)}){
            Assert.Equal(0x7fc00000,BitConverter.SingleToInt32Bits(ScalarNormalization.Normalize(BitConverter.Int32BitsToSingle(bits))));
        }
        foreach(var bits in new[]{0x7ff0000000000001L,0x7ff8123456789abcL,unchecked((long)0xfff0000000000001UL)}){
            Assert.Equal(0x7ff8000000000000L,BitConverter.DoubleToInt64Bits(ScalarNormalization.Normalize(BitConverter.Int64BitsToDouble(bits))));
        }
        foreach(var bits in new[]{(short)0x7c01,(short)0x7e21,unchecked((short)0xfc01)}){
            Assert.Equal(0x7e00,BitConverter.HalfToInt16Bits(ScalarNormalization.Normalize(BitConverter.Int16BitsToHalf(bits))));
        }
        foreach(var value in new[]{double.NegativeInfinity,-double.MaxValue,-1.25,double.Epsilon,1.25,double.MaxValue,double.PositiveInfinity}){
            Assert.Equal(BitConverter.DoubleToInt64Bits(value),BitConverter.DoubleToInt64Bits(ScalarNormalization.Normalize(value)));
        }
    }

    [Theory,Trait("Category","Unit")]
    [InlineData("0.000","0")]
    [InlineData("-0.000","0")]
    [InlineData("1.000","1")]
    [InlineData("-1.000","-1")]
    [InlineData("1.230000","1.23")]
    [InlineData("0.1000000000000000000000000000","0.1")]
    [InlineData("0.0000000000000000000000000001","0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335","79228162514264337593543950335")]
    [InlineData("-79228162514264337593543950335","-79228162514264337593543950335")]
    [InlineData("7922816251426433759354395033.5","7922816251426433759354395033.5")]
    public void Decimal_RemovesOnlyRedundantScale(string input,string expected){
        var value=decimal.Parse(input,CultureInfo.InvariantCulture);
        var canonical=decimal.Parse(expected,CultureInfo.InvariantCulture);
        var normalized=ScalarNormalization.Normalize(value);
        Assert.Equal(value,normalized);
        Assert.Equal(decimal.GetBits(canonical),decimal.GetBits(normalized));
        Assert.Equal(decimal.GetBits(normalized),decimal.GetBits(ScalarNormalization.Normalize(normalized)));
    }

    [Fact,Trait("Category","Unit")]
    public void Decimal_CanonicalizesSignedZeroAtMaximumScale(){
        var value=new decimal(0,0,0,true,28);
        Assert.Equal(new int[4],decimal.GetBits(ScalarNormalization.Normalize(value)));
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void DateTime_NormalizesKindAndInstant(DateTimeKind kind){
        foreach(var ticks in new[]{DateTime.MinValue.Ticks,new DateTime(2026,11,7,12,34,56).Ticks,DateTime.MaxValue.Ticks}){
            var input=new DateTime(ticks,kind);
            var expected=kind==DateTimeKind.Local?input.ToUniversalTime():DateTime.SpecifyKind(input,DateTimeKind.Utc);
            var normalized=ScalarNormalization.Normalize(input);
            Assert.Equal(DateTimeKind.Utc,normalized.Kind);
            Assert.Equal(expected.Ticks,normalized.Ticks);
        }
    }

    [Theory,Trait("Category","Unit")]
    [InlineData(-14)]
    [InlineData(-7)]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(14)]
    public void DateTimeOffset_PreservesInstantAndClearsOffset(int hours){
        var input=new DateTimeOffset(2026,11,7,12,34,56,TimeSpan.FromHours(hours));
        var normalized=ScalarNormalization.Normalize(input);
        Assert.Equal(input.UtcTicks,normalized.UtcTicks);
        Assert.Equal(TimeSpan.Zero,normalized.Offset);
    }

    [Fact,Trait("Category","Unit")]
    public void Nullable_PreservesNullAndNormalizesPresentValues(){
        Assert.Null(ScalarNormalization.Normalize((float?)null));
        Assert.Null(ScalarNormalization.Normalize((double?)null));
        Assert.Null(ScalarNormalization.Normalize((Half?)null));
        Assert.Null(ScalarNormalization.Normalize((decimal?)null));
        Assert.Null(ScalarNormalization.Normalize((DateTime?)null));
        Assert.Null(ScalarNormalization.Normalize((DateTimeOffset?)null));
        Assert.Equal(0,BitConverter.DoubleToInt64Bits(ScalarNormalization.Normalize((double?)BitConverter.Int64BitsToDouble(long.MinValue))!.Value));
        Assert.Equal(DateTimeKind.Utc,ScalarNormalization.Normalize((DateTime?)default(DateTime))!.Value.Kind);
    }
}
