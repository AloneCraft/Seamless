using System.Numerics;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 算術演算子(+ - * / %)と文字列の連結。どちらかが NULL なら NULL。
/// 型の結果(tinyint 同士の和は tinyint など)は翻訳器が型の規則で決め、その型の版を呼ぶ。あふれは 8115、0での割り算は 8134。
/// decimal の結果は <see cref="Fit"/> で型の精度と桁へ合わせる。
/// </summary>
public static class SqlArithmetic{
    /// <summary>+ の整数版。</summary>
    public static byte? Add(byte? left,byte? right)=>Integer(left,right,static (a,b)=>a+b,byte.MinValue,byte.MaxValue,"tinyint",static value=>(byte)value);
    /// <summary>+ の smallint 版。</summary>
    public static short? Add(short? left,short? right)=>Integer(left,right,static (a,b)=>a+b,short.MinValue,short.MaxValue,"smallint",static value=>(short)value);
    /// <summary>+ の int 版。</summary>
    public static int? Add(int? left,int? right)=>Integer(left,right,static (a,b)=>a+b,int.MinValue,int.MaxValue,"int",static value=>(int)value);
    /// <summary>+ の bigint 版。</summary>
    public static long? Add(long? left,long? right)=>Integer(left,right,static (a,b)=>a+b,long.MinValue,long.MaxValue,"bigint",static value=>(long)value);
    /// <summary>+ の decimal 版。</summary>
    public static decimal? Add(decimal? left,decimal? right)=>Decimal(left,right,static (a,b)=>a+b);
    /// <summary>+ の float 版。</summary>
    public static double? Add(double? left,double? right)=>Floating(left,right,static (a,b)=>a+b);
    /// <summary>+ の real 版(単精度で計算する)。</summary>
    public static float? Add(float? left,float? right)=>Single(left,right,static (a,b)=>a+b);
    /// <summary>- の real 版。</summary>
    public static float? Subtract(float? left,float? right)=>Single(left,right,static (a,b)=>a-b);
    /// <summary>* の real 版。</summary>
    public static float? Multiply(float? left,float? right)=>Single(left,right,static (a,b)=>a*b);
    /// <summary>/ の real 版。</summary>
    public static float? Divide(float? left,float? right)=>Single(left,right,static (a,b)=>{
        if(b==0){
            throw DivideByZero();
        }
        return a/b;
    });
    /// <summary>単項の - の real 版。</summary>
    public static float? Negate(float? value)=>Subtract(0f,value);
    /// <summary>- の tinyint 版。</summary>
    public static byte? Subtract(byte? left,byte? right)=>Integer(left,right,static (a,b)=>a-b,byte.MinValue,byte.MaxValue,"tinyint",static value=>(byte)value);
    /// <summary>- の smallint 版。</summary>
    public static short? Subtract(short? left,short? right)=>Integer(left,right,static (a,b)=>a-b,short.MinValue,short.MaxValue,"smallint",static value=>(short)value);
    /// <summary>- の int 版。</summary>
    public static int? Subtract(int? left,int? right)=>Integer(left,right,static (a,b)=>a-b,int.MinValue,int.MaxValue,"int",static value=>(int)value);
    /// <summary>- の bigint 版。</summary>
    public static long? Subtract(long? left,long? right)=>Integer(left,right,static (a,b)=>a-b,long.MinValue,long.MaxValue,"bigint",static value=>(long)value);
    /// <summary>- の decimal 版。</summary>
    public static decimal? Subtract(decimal? left,decimal? right)=>Decimal(left,right,static (a,b)=>a-b);
    /// <summary>- の float 版。</summary>
    public static double? Subtract(double? left,double? right)=>Floating(left,right,static (a,b)=>a-b);
    /// <summary>* の tinyint 版。</summary>
    public static byte? Multiply(byte? left,byte? right)=>Integer(left,right,static (a,b)=>a*b,byte.MinValue,byte.MaxValue,"tinyint",static value=>(byte)value);
    /// <summary>* の smallint 版。</summary>
    public static short? Multiply(short? left,short? right)=>Integer(left,right,static (a,b)=>a*b,short.MinValue,short.MaxValue,"smallint",static value=>(short)value);
    /// <summary>* の int 版。</summary>
    public static int? Multiply(int? left,int? right)=>Integer(left,right,static (a,b)=>a*b,int.MinValue,int.MaxValue,"int",static value=>(int)value);
    /// <summary>* の bigint 版。</summary>
    public static long? Multiply(long? left,long? right)=>Integer(left,right,static (a,b)=>a*b,long.MinValue,long.MaxValue,"bigint",static value=>(long)value);
    /// <summary>* の decimal 版。</summary>
    public static decimal? Multiply(decimal? left,decimal? right)=>Decimal(left,right,static (a,b)=>a*b);
    /// <summary>* の float 版。</summary>
    public static double? Multiply(double? left,double? right)=>Floating(left,right,static (a,b)=>a*b);
    /// <summary>/ の tinyint 版(0の方へ切り捨て)。</summary>
    public static byte? Divide(byte? left,byte? right)=>Integer(left,right,static (a,b)=>Quotient(a,b),byte.MinValue,byte.MaxValue,"tinyint",static value=>(byte)value);
    /// <summary>/ の smallint 版。</summary>
    public static short? Divide(short? left,short? right)=>Integer(left,right,static (a,b)=>Quotient(a,b),short.MinValue,short.MaxValue,"smallint",static value=>(short)value);
    /// <summary>/ の int 版。</summary>
    public static int? Divide(int? left,int? right)=>Integer(left,right,static (a,b)=>Quotient(a,b),int.MinValue,int.MaxValue,"int",static value=>(int)value);
    /// <summary>/ の bigint 版。</summary>
    public static long? Divide(long? left,long? right)=>Integer(left,right,static (a,b)=>Quotient(a,b),long.MinValue,long.MaxValue,"bigint",static value=>(long)value);
    /// <summary>/ の decimal 版。結果の桁は <see cref="Fit"/> で合わせる。</summary>
    public static decimal? Divide(decimal? left,decimal? right)=>Decimal(left,right,static (a,b)=>{
        if(b==0){
            throw DivideByZero();
        }
        return a/b;
    });
    /// <summary>/ の float 版。</summary>
    public static double? Divide(double? left,double? right)=>Floating(left,right,static (a,b)=>{
        if(b==0){
            throw DivideByZero();
        }
        return a/b;
    });
    /// <summary>% の tinyint 版。</summary>
    public static byte? Modulo(byte? left,byte? right)=>Integer(left,right,static (a,b)=>Remainder(a,b),byte.MinValue,byte.MaxValue,"tinyint",static value=>(byte)value);
    /// <summary>% の smallint 版。</summary>
    public static short? Modulo(short? left,short? right)=>Integer(left,right,static (a,b)=>Remainder(a,b),short.MinValue,short.MaxValue,"smallint",static value=>(short)value);
    /// <summary>% の int 版。</summary>
    public static int? Modulo(int? left,int? right)=>Integer(left,right,static (a,b)=>Remainder(a,b),int.MinValue,int.MaxValue,"int",static value=>(int)value);
    /// <summary>% の bigint 版。</summary>
    public static long? Modulo(long? left,long? right)=>Integer(left,right,static (a,b)=>Remainder(a,b),long.MinValue,long.MaxValue,"bigint",static value=>(long)value);
    /// <summary>% の decimal 版。</summary>
    public static decimal? Modulo(decimal? left,decimal? right)=>Decimal(left,right,static (a,b)=>{
        if(b==0){
            throw DivideByZero();
        }
        return a%b;
    });
    /// <summary>単項の - の int 版。</summary>
    public static int? Negate(int? value)=>Subtract(0,value);
    /// <summary>単項の - の bigint 版。</summary>
    public static long? Negate(long? value)=>Subtract(0L,value);
    /// <summary>単項の - の smallint 版。</summary>
    public static short? Negate(short? value)=>Subtract((short)0,value);
    /// <summary>単項の - の decimal 版。</summary>
    public static decimal? Negate(decimal? value)=>Subtract(0m,value);
    /// <summary>単項の - の float 版。</summary>
    public static double? Negate(double? value)=>Subtract(0d,value);
    /// <summary>文字列の + 。どちらかが NULL なら NULL(CONCAT と違う)。</summary>
    public static string? Concat(string? left,string? right){
        if(left is null||right is null){
            return null;
        }
        return left+right;
    }
    /// <summary>
    /// decimal を型 decimal(precision,scale) へ合わせる。scale の桁へ四捨五入(0から遠い方)し、整数部が入らなければ 8115。
    /// </summary>
    public static decimal? Fit(decimal? value,byte precision,byte scale)=>Fit(value,precision,scale,false);
    /// <summary>money の割り算の結果を合わせる。本物は小数4桁で切り捨てる(250.25/3 は 83.4166)。</summary>
    public static decimal? FitTruncated(decimal? value,byte precision,byte scale)=>Fit(value,precision,scale,true);
    private static decimal? Fit(decimal? value,byte precision,byte scale,bool truncate){
        if(value is not{} number){
            return null;
        }
        var rounded=decimal.Round(number,Math.Min(scale,(byte)28),MidpointRounding.AwayFromZero);
        if(truncate){
            rounded=decimal.Round(number,Math.Min(scale,(byte)28),MidpointRounding.ToZero);
        }
        var integral=decimal.Truncate(Math.Abs(rounded));
        var digits=0;
        while(integral>=1){
            integral=decimal.Truncate(integral/10);
            digits++;
        }
        if(digits>precision-scale){
            throw new SqlFunctionException("Arithmetic overflow error converting numeric to data type numeric.",8115);
        }
        return rounded;
    }
    // ---- 補助 ----
    private static BigInteger Quotient(BigInteger a,BigInteger b){
        if(b.IsZero){
            throw DivideByZero();
        }
        return BigInteger.Divide(a,b);
    }
    private static BigInteger Remainder(BigInteger a,BigInteger b){
        if(b.IsZero){
            throw DivideByZero();
        }
        return BigInteger.Remainder(a,b);
    }
    private static T? Integer<T>(T? left,T? right,Func<BigInteger,BigInteger,BigInteger> operation,BigInteger minimum,BigInteger maximum,string typeName,Func<BigInteger,T> narrow)where T:struct,IBinaryInteger<T>{
        if(left is not{} a||right is not{} b){
            return null;
        }
        var result=operation(BigInteger.CreateChecked(a),BigInteger.CreateChecked(b));
        if(result<minimum||result>maximum){
            throw new SqlFunctionException($"Arithmetic overflow error converting expression to data type {typeName}.",8115);
        }
        return narrow(result);
    }
    private static decimal? Decimal(decimal? left,decimal? right,Func<decimal,decimal,decimal> operation){
        if(left is not{} a||right is not{} b){
            return null;
        }
        try{
            return operation(a,b);
        }catch(OverflowException){
            throw new SqlFunctionException("Arithmetic overflow error converting expression to data type numeric.",8115);
        }
    }
    private static double? Floating(double? left,double? right,Func<double,double,double> operation){
        if(left is not{} a||right is not{} b){
            return null;
        }
        var result=operation(a,b);
        if(!double.IsFinite(result)){
            throw new SqlFunctionException("Arithmetic overflow error converting expression to data type float.",8115);
        }
        return result;
    }
    private static float? Single(float? left,float? right,Func<float,float,float> operation){
        if(left is not{} a||right is not{} b){
            return null;
        }
        var result=operation(a,b);
        if(!float.IsFinite(result)){
            throw new SqlFunctionException("Arithmetic overflow error converting expression to data type real.",8115);
        }
        return result;
    }
    private static SqlFunctionException DivideByZero()=>new("Divide by zero error encountered.",8134);
}
