namespace Seamless.Collections;

/// <summary>保存・通信・比較で共有する既定のスカラー正規化。</summary>
public static class ScalarNormalization{
    public static float Normalize(float value){
        if(float.IsNaN(value)){return BitConverter.Int32BitsToSingle(0x7fc00000);}
        return value==0?0:value;
    }

    public static double Normalize(double value){
        if(double.IsNaN(value)){return BitConverter.Int64BitsToDouble(0x7ff8000000000000);}
        return value==0?0:value;
    }

    public static Half Normalize(Half value){
        if(Half.IsNaN(value)){return BitConverter.Int16BitsToHalf(0x7e00);}
        return value==(Half)0?(Half)0:value;
    }

    public static decimal Normalize(decimal value){
        if(value==0){return decimal.Zero;}
        Span<int> bits=stackalloc int[4];
        decimal.GetBits(value,bits);
        var low=unchecked((uint)bits[0]);
        var middle=unchecked((uint)bits[1]);
        var high=unchecked((uint)bits[2]);
        var scale=(byte)((bits[3]>>16)&0xff);
        while(scale>0){
            var highQuotient=high/10;
            var middleDividend=((ulong)(high%10)<<32)|middle;
            var middleQuotient=(uint)(middleDividend/10);
            var lowDividend=((middleDividend%10)<<32)|low;
            if(lowDividend%10!=0){break;}
            high=highQuotient;
            middle=middleQuotient;
            low=(uint)(lowDividend/10);
            scale--;
        }
        return new decimal(unchecked((int)low),unchecked((int)middle),unchecked((int)high),bits[3]<0,scale);
    }

    public static DateTime Normalize(DateTime value)=>value.Kind switch{
        DateTimeKind.Utc=>value,
        DateTimeKind.Local=>value.ToUniversalTime(),
        _=>DateTime.SpecifyKind(value,DateTimeKind.Utc)
    };

    public static DateTimeOffset Normalize(DateTimeOffset value)=>value.ToUniversalTime();
    public static float? Normalize(float? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
    public static double? Normalize(double? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
    public static Half? Normalize(Half? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
    public static decimal? Normalize(decimal? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
    public static DateTime? Normalize(DateTime? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
    public static DateTimeOffset? Normalize(DateTimeOffset? value)=>value.HasValue?Normalize(value.GetValueOrDefault()):null;
}
