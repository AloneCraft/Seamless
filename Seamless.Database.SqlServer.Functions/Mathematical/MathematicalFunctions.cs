namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 数学の関数。整数・decimal・float の版を分け、戻り値の型は SQL Server と同じにする
/// (例: RADIANS(180) は int の 3、POWER(2,-1) は int の 0)。
/// float の定義域の外(LOG(0) など)は 3623、整数のあふれは 8115。
/// </summary>
public static class MathematicalFunctions{
    // ---- 符号と絶対値 ----
    /// <summary>ABS の int 版。</summary>
    [SqlFunction("ABS")]
    public static int? Abs(int? value){
        if(value is not{} number){
            return null;
        }
        if(number==int.MinValue){
            throw Overflow("int");
        }
        return Math.Abs(number);
    }
    /// <summary>ABS の bigint 版。</summary>
    [SqlFunction("ABS")]
    public static long? Abs(long? value){
        if(value is not{} number){
            return null;
        }
        if(number==long.MinValue){
            throw Overflow("bigint");
        }
        return Math.Abs(number);
    }
    /// <summary>ABS の decimal 版。</summary>
    [SqlFunction("ABS")]
    public static decimal? Abs(decimal? value)=>Map(value,static number=>Math.Abs(number));
    /// <summary>ABS の float 版。</summary>
    [SqlFunction("ABS")]
    public static double? Abs(double? value)=>Map(value,static number=>Math.Abs(number));
    /// <summary>SIGN の int 版。</summary>
    [SqlFunction("SIGN")]
    public static int? Sign(int? value)=>Map(value,static number=>Math.Sign(number));
    /// <summary>SIGN の bigint 版。</summary>
    [SqlFunction("SIGN")]
    public static long? Sign(long? value)=>Map(value,static number=>Math.Sign(number));
    /// <summary>SIGN の decimal 版。</summary>
    [SqlFunction("SIGN")]
    public static decimal? Sign(decimal? value)=>Map(value,static number=>Math.Sign(number));
    /// <summary>SIGN の float 版。</summary>
    [SqlFunction("SIGN")]
    public static double? Sign(double? value)=>Map(value,static number=>Math.Sign(number));
    // ---- 丸め ----
    /// <summary>CEILING の decimal 版。</summary>
    [SqlFunction("CEILING")]
    public static decimal? Ceiling(decimal? value)=>Map(value,static number=>decimal.Ceiling(number));
    /// <summary>CEILING の float 版。</summary>
    [SqlFunction("CEILING")]
    public static double? Ceiling(double? value)=>Map(value,static number=>Math.Ceiling(number));
    /// <summary>FLOOR の decimal 版。</summary>
    [SqlFunction("FLOOR")]
    public static decimal? Floor(decimal? value)=>Map(value,static number=>decimal.Floor(number));
    /// <summary>FLOOR の float 版。</summary>
    [SqlFunction("FLOOR")]
    public static double? Floor(double? value)=>Map(value,static number=>Math.Floor(number));
    /// <summary>
    /// ROUND の decimal 版。length が負なら小数点より左を丸める。function が 0 以外なら切り捨て。
    /// 半分は0から遠い方へ。結果の桁があふれるかどうかは、翻訳器が型の精度で確かめる。
    /// </summary>
    [SqlFunction("ROUND")]
    public static decimal? Round(decimal? value,int? length,int? function=0){
        if(value is not{} number||length is not{} digits||function is not{} mode){
            return null;
        }
        if(digits>=0){
            if(digits>28){
                return number;
            }
            if(mode!=0){
                return decimal.Truncate(number*Pow10(digits))/Pow10(digits);
            }
            return decimal.Round(number,digits,MidpointRounding.AwayFromZero);
        }
        if(-digits>28){
            return 0m;
        }
        var factor=Pow10(-digits);
        if(mode!=0){
            return decimal.Truncate(number/factor)*factor;
        }
        return decimal.Round(number/factor,0,MidpointRounding.AwayFromZero)*factor;
    }
    /// <summary>ROUND の float 版。</summary>
    [SqlFunction("ROUND")]
    public static double? Round(double? value,int? length,int? function=0){
        if(value is not{} number||length is not{} digits||function is not{} mode){
            return null;
        }
        var factor=Math.Pow(10,digits);
        if(mode!=0){
            return Math.Truncate(number*factor)/factor;
        }
        return Math.Round(number*factor,MidpointRounding.AwayFromZero)/factor;
    }
    /// <summary>ROUND の int 版。length が負のときだけ値が変わる。</summary>
    [SqlFunction("ROUND")]
    public static int? Round(int? value,int? length,int? function=0){
        if(value is not{} number||length is not{} digits||function is not{} mode){
            return null;
        }
        if(Round((decimal)number,digits,mode) is not{} rounded){
            return null;
        }
        if(rounded>int.MaxValue||rounded<int.MinValue){
            throw Overflow("int");
        }
        return (int)rounded;
    }
    // ---- 累乗と対数 ----
    /// <summary>POWER の float 版。</summary>
    [SqlFunction("POWER")]
    public static double? Power(double? value,double? exponent)=>Checked(value,exponent,Math.Pow);
    /// <summary>POWER の int 版。float で計算して0の方へ切り捨てる(POWER(2,-1) は 0)。</summary>
    [SqlFunction("POWER")]
    public static int? Power(int? value,double? exponent){
        if(Checked(value,exponent,Math.Pow) is not{} result){
            return null;
        }
        var truncated=Math.Truncate(result);
        if(truncated>int.MaxValue||truncated<int.MinValue){
            throw new SqlFunctionException("Arithmetic overflow error for type int.",232);
        }
        return (int)truncated;
    }
    /// <summary>POWER の bigint 版。</summary>
    [SqlFunction("POWER")]
    public static long? Power(long? value,double? exponent){
        if(Checked(value,exponent,Math.Pow) is not{} result){
            return null;
        }
        var truncated=Math.Truncate(result);
        if(truncated>=9.2233720368547758E18||truncated<-9.2233720368547758E18){
            throw new SqlFunctionException("Arithmetic overflow error for type bigint.",232);
        }
        return (long)truncated;
    }
    /// <summary>SQRT。負なら 3623。</summary>
    [SqlFunction("SQRT")]
    public static double? Sqrt(double? value)=>Checked(value,Math.Sqrt);
    /// <summary>SQUARE。</summary>
    [SqlFunction("SQUARE")]
    public static double? Square(double? value)=>Checked(value,static x=>x*x);
    /// <summary>EXP。</summary>
    [SqlFunction("EXP")]
    public static double? Exp(double? value)=>Checked(value,Math.Exp);
    /// <summary>LOG。底を省くと自然対数。0以下は 3623。</summary>
    [SqlFunction("LOG")]
    public static double? Log(double? value,double? newBase=null){
        if(value is not{} number){
            return null;
        }
        if(newBase is{} b){
            return Checked((double?)number,static x=>x,result=>Math.Log(result,b));
        }
        return Checked(value,Math.Log);
    }
    /// <summary>LOG10。</summary>
    [SqlFunction("LOG10")]
    public static double? Log10(double? value)=>Checked(value,Math.Log10);
    // ---- 三角関数 ----
    /// <summary>PI。</summary>
    [SqlFunction("PI")]
    public static double Pi()=>Math.PI;
    /// <summary>SIN。</summary>
    [SqlFunction("SIN")]
    public static double? Sin(double? value)=>Checked(value,Math.Sin);
    /// <summary>COS。</summary>
    [SqlFunction("COS")]
    public static double? Cos(double? value)=>Checked(value,Math.Cos);
    /// <summary>TAN。</summary>
    [SqlFunction("TAN")]
    public static double? Tan(double? value)=>Checked(value,Math.Tan);
    /// <summary>COT。0 は 3623。</summary>
    [SqlFunction("COT")]
    public static double? Cot(double? value)=>Checked(value,static x=>1/Math.Tan(x));
    /// <summary>ASIN。-1〜1 の外は 3623。</summary>
    [SqlFunction("ASIN")]
    public static double? Asin(double? value)=>Checked(value,Math.Asin);
    /// <summary>ACOS。-1〜1 の外は 3623。</summary>
    [SqlFunction("ACOS")]
    public static double? Acos(double? value)=>Checked(value,Math.Acos);
    /// <summary>ATAN。</summary>
    [SqlFunction("ATAN")]
    public static double? Atan(double? value)=>Checked(value,Math.Atan);
    /// <summary>ATN2。</summary>
    [SqlFunction("ATN2")]
    public static double? Atn2(double? y,double? x)=>Checked(y,x,Math.Atan2);
    /// <summary>DEGREES の float 版。</summary>
    [SqlFunction("DEGREES")]
    public static double? Degrees(double? value)=>Checked(value,static x=>x*180/Math.PI);
    /// <summary>DEGREES の int 版。float で計算して切り捨てる。</summary>
    [SqlFunction("DEGREES")]
    public static int? Degrees(int? value)=>Map(value,static number=>(int)Math.Truncate(number*180/Math.PI));
    /// <summary>RADIANS の float 版。</summary>
    [SqlFunction("RADIANS")]
    public static double? Radians(double? value)=>Checked(value,static x=>x*Math.PI/180);
    /// <summary>RADIANS の int 版。float で計算して切り捨てる(RADIANS(180) は 3)。</summary>
    [SqlFunction("RADIANS")]
    public static int? Radians(int? value)=>Map(value,static number=>(int)Math.Truncate(number*Math.PI/180));
    // ---- 補助 ----
    private static TResult? Map<T,TResult>(T? value,Func<T,TResult> function)where T:struct where TResult:struct{
        if(value is{} number){
            return function(number);
        }
        return null;
    }
    private static decimal Pow10(int digits){
        var result=1m;
        for(var i=0;i<digits;i++){
            result*=10;
        }
        return result;
    }
    private static double? Checked(double? value,Func<double,double> function){
        if(value is not{} number){
            return null;
        }
        var result=function(number);
        if(!double.IsFinite(result)){
            throw InvalidFloat();
        }
        return result;
    }
    private static double? Checked(double? value,Func<double,double> first,Func<double,double> second){
        if(Checked(value,first) is not{} intermediate){
            return null;
        }
        return Checked(intermediate,second);
    }
    private static double? Checked(double? left,double? right,Func<double,double,double> function){
        if(left is not{} a||right is not{} b){
            return null;
        }
        var result=function(a,b);
        if(!double.IsFinite(result)){
            throw InvalidFloat();
        }
        return result;
    }
    private static SqlFunctionException InvalidFloat()=>new("An invalid floating point operation occurred.",3623);
    private static SqlFunctionException Overflow(string type)=>new($"Arithmetic overflow error converting expression to data type {type}.",8115);
}
