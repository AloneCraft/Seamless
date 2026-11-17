using System.Globalization;
using System.Text;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>日付と時刻を文字列へ変えるときの元の型。小数秒の桁と既定の書式が型で変わる。</summary>
public enum SqlTemporalType{
    DateTime,SmallDateTime,Date,Time,DateTime2,DateTimeOffset
}
/// <summary>
/// CAST・CONVERT のうち、型の規則だけでは決まらない変換(書式の番号つきの日付と文字列、数値の文字列化、文字列の読み取り)。
/// どの変換を使うかは翻訳器が元と先の型から選ぶ。
/// </summary>
public static class ConversionFunctions{
    private static readonly string[] shortMonths=["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
    // ---- 日付と時刻 → 文字列 ----
    /// <summary>
    /// CONVERT(文字列型, 日時, style)。style は 0〜14・20〜25・100〜114・120〜131 の SQL Server の番号。
    /// datetime の小数秒は3桁、datetime2・time は scale の桁、smalldatetime は秒を出さない書式もある。
    /// </summary>
    public static string? DateTimeToString(DateTime? value,SqlTemporalType type,int style=0,byte scale=7){
        if(value is not{} date){
            return null;
        }
        var fraction=Fraction(date,type,scale);
        var y4=date.Year.ToString("D4",CultureInfo.InvariantCulture);
        var y2=(date.Year%100).ToString("D2",CultureInfo.InvariantCulture);
        var mm=date.Month.ToString("D2",CultureInfo.InvariantCulture);
        var dd=date.Day.ToString("D2",CultureInfo.InvariantCulture);
        var mon=shortMonths[date.Month-1];
        var hms=date.ToString("HH:mm:ss",CultureInfo.InvariantCulture);
        if(style==0||style==100){
            if(type is SqlTemporalType.Date or SqlTemporalType.Time or SqlTemporalType.DateTimeOffset){
                throw new NotSupportedException($"{type} の style {style} は本物との照合が済んでいないので扱わない。");
            }
            return mon+" "+date.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)+" "+y4+" "+Clock12(date,false,"",type);
        }
        return style switch{
            1=>mm+"/"+dd+"/"+y2,
            101=>mm+"/"+dd+"/"+y4,
            2=>y2+"."+mm+"."+dd,
            102=>y4+"."+mm+"."+dd,
            3=>dd+"/"+mm+"/"+y2,
            103=>dd+"/"+mm+"/"+y4,
            4=>dd+"."+mm+"."+y2,
            104=>dd+"."+mm+"."+y4,
            5=>dd+"-"+mm+"-"+y2,
            105=>dd+"-"+mm+"-"+y4,
            6=>dd+" "+mon+" "+y2,
            106=>dd+" "+mon+" "+y4,
            7=>mon+" "+dd+", "+y2,
            107=>mon+" "+dd+", "+y4,
            8 or 24 or 108=>hms,
            9 or 109=>mon+" "+date.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2)+" "+y4+" "+Clock12(date,true,fraction,type),
            10=>mm+"-"+dd+"-"+y2,
            110=>mm+"-"+dd+"-"+y4,
            11=>y2+"/"+mm+"/"+dd,
            111=>y4+"/"+mm+"/"+dd,
            12=>y2+mm+dd,
            112=>y4+mm+dd,
            13 or 113=>dd+" "+mon+" "+y4+" "+hms+Separated(fraction,type),
            14 or 114=>hms+Separated(fraction,type),
            20 or 120=>y4+"-"+mm+"-"+dd+" "+hms,
            21 or 25 or 121=>y4+"-"+mm+"-"+dd+" "+hms+Dot(fraction),
            22=>mm+"/"+dd+"/"+y2+" "+Clock12Seconds(date),
            23=>y4+"-"+mm+"-"+dd,
            126=>y4+"-"+mm+"-"+dd+"T"+hms+Dot(TrimFraction(fraction,type)),
            127=>y4+"-"+mm+"-"+dd+"T"+hms+Dot(TrimFraction(fraction,type)),
            _=>throw new SqlFunctionException($"{style} is not a valid style number when converting from datetime to a character string.",281)
        };
    }
    // ---- 文字列 → 日付と時刻 ----
    /// <summary>
    /// CONVERT(日時型, 文字列, style)。style 112(yyyymmdd)・23/126/127(ISO)・120/121・101・103 などを読む。
    /// style 0 は区切りの無い yyyymmdd・ISO・月名つき・SET DATEFORMAT の順の数字を受け付ける。読めなければ 241。
    /// </summary>
    public static DateTime? StringToDateTime(SqlSession session,string? text,int style=0){
        if(text is null){
            return null;
        }
        var trimmed=text.Trim();
        if(trimmed.Length==0){
            return new DateTime(1900,1,1);
        }
        string[] formats=style switch{
            112 or 12=>["yyyyMMdd","yyMMdd"],
            101 or 1=>["MM/dd/yyyy","MM/dd/yy","MM/dd/yyyy HH:mm:ss","M/d/yyyy"],
            103 or 3=>["dd/MM/yyyy","dd/MM/yy","d/M/yyyy"],
            104 or 4=>["dd.MM.yyyy","dd.MM.yy"],
            105 or 5=>["dd-MM-yyyy","dd-MM-yy"],
            111 or 11=>["yyyy/MM/dd","yy/MM/dd"],
            _=>[]
        };
        if(formats.Length>0&&DateTime.TryParseExact(trimmed,formats,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var exact)){
            return exact;
        }
        if(DateTime.TryParseExact(trimmed,["yyyyMMdd","yyyyMMdd HH:mm:ss","yyyyMMdd HH:mm:ss.FFFFFFF","yyyy-MM-ddTHH:mm:ss","yyyy-MM-ddTHH:mm:ss.FFFFFFF","yyyy-MM-ddTHH:mm:ss.FFFFFFFZ","yyyy-MM-dd","yyyy-MM-dd HH:mm:ss","yyyy-MM-dd HH:mm:ss.FFFFFFF","yyyy-MM-dd HH:mm"],CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var iso)){
            return iso;
        }
        var order=session.DateFormat.ToLowerInvariant() switch{
            "dmy"=>new[]{"d/M/yyyy","d-M-yyyy","d.M.yyyy","d/M/yy","d/M/yyyy H:mm","d/M/yyyy H:mm:ss","d/M/yyyy H:mm:ss.FFF"},
            "ymd"=>["yyyy/M/d","yyyy-M-d","yyyy.M.d","yyyy/M/d H:mm:ss"],
            _=>["M/d/yyyy","M-d-yyyy","M.d.yyyy","M/d/yy","M/d/yyyy H:mm","M/d/yyyy H:mm:ss","M/d/yyyy H:mm:ss.FFF","M/d/yyyy h:mm tt","M/d/yyyy h:mm:ss tt"]
        };
        if(DateTime.TryParseExact(trimmed,order,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var numeric)){
            return numeric;
        }
        if(DateTime.TryParseExact(trimmed,["MMM d yyyy","MMM d yyyy h:mmtt","MMM d yyyy h:mm:ss:ffftt","d MMM yyyy","d MMM yyyy HH:mm:ss","MMMM d, yyyy","MMM d, yyyy","MMMM d yyyy"],CultureInfo.GetCultureInfo("en-US"),DateTimeStyles.AllowWhiteSpaces,out var named)){
            return named;
        }
        throw new SqlFunctionException("Conversion failed when converting date and/or time from character string.",241);
    }
    // ---- 数値 → 文字列 ----
    /// <summary>整数を文字列へ。</summary>
    public static string? IntegerToString(long? value)=>value?.ToString(CultureInfo.InvariantCulture);
    /// <summary>decimal・numeric を文字列へ。小数部は型の scale の桁まで0で埋める。</summary>
    public static string? DecimalToString(decimal? value,byte scale){
        if(value is not{} number){
            return null;
        }
        var text=number.ToString("F"+scale,CultureInfo.InvariantCulture);
        if(scale>0&&text.StartsWith("0.",StringComparison.Ordinal)){
            return text;
        }
        if(scale>0&&text.StartsWith("-0.",StringComparison.Ordinal)){
            return text;
        }
        return text;
    }
    /// <summary>money・smallmoney を文字列へ。style 0 は小数2桁、1 は3桁ごとのカンマと小数2桁、2 は小数4桁。</summary>
    public static string? MoneyToString(decimal? value,int style=0){
        if(value is not{} number){
            return null;
        }
        return style switch{
            0=>number.ToString("0.00",CultureInfo.InvariantCulture),
            1=>number.ToString("#,0.00",CultureInfo.InvariantCulture),
            2=>number.ToString("0.0000",CultureInfo.InvariantCulture),
            126=>number.ToString("0.0000",CultureInfo.InvariantCulture),
            _=>throw new SqlFunctionException($"{style} is not a valid style number when converting from money to a character string.",281)
        };
    }
    /// <summary>
    /// float・real を文字列へ。style 0 は有効6桁(指数が -5 以下か 6 以上なら 1.23457e+006 の形)、1 は有効8桁の指数形、2 は有効16桁の指数形、3 は元に戻せる17桁。
    /// </summary>
    public static string? FloatToString(double? value,int style=0){
        if(value is not{} number){
            return null;
        }
        switch(style){
            case 0:{
                // 有効6桁の一般形式。指数形のときは仮数の末尾の0を省き、指数を符号と3桁にする(1.234e-005)。
                var text=number.ToString("G6",CultureInfo.InvariantCulture);
                var e=text.IndexOf('E');
                if(e<0){
                    return text;
                }
                var exponent=int.Parse(text.AsSpan(e+1),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
                var sign="+";
                if(exponent<0){
                    sign="-";
                }
                return text[..e]+"e"+sign+Math.Abs(exponent).ToString("D3",CultureInfo.InvariantCulture);
            }
            case 1:
                return Scientific(number,7,3);
            case 2:
                return Scientific(number,15,3);
            case 3:
                return number.ToString("R",CultureInfo.InvariantCulture);
            default:
                throw new SqlFunctionException($"{style} is not a valid style number when converting from float to a character string.",281);
        }
    }
    /// <summary>bit を文字列へ。</summary>
    public static string? BitToString(bool? value){
        if(value is not{} bit){
            return null;
        }
        if(bit){
            return "1";
        }
        return "0";
    }
    /// <summary>uniqueidentifier を文字列へ(大文字)。</summary>
    public static string? GuidToString(Guid? value)=>value?.ToString("D").ToUpperInvariant();
    // ---- 文字列 → 数値 ----
    /// <summary>文字列を int へ。前後の空白は無視する。読めなければ 245。</summary>
    public static int? StringToInt32(string? text){
        if(StringToInt64(text,"int") is not{} number){
            return null;
        }
        if(number<int.MinValue||number>int.MaxValue){
            throw new SqlFunctionException($"The conversion of the varchar value '{text}' overflowed an int column.",248);
        }
        return (int)number;
    }
    /// <summary>文字列を bigint へ。</summary>
    public static long? StringToInt64(string? text)=>StringToInt64(text,"bigint");
    /// <summary>文字列を decimal へ。scale の桁へ四捨五入する。読めなければ 8114。</summary>
    public static decimal? StringToDecimal(string? text,byte scale){
        if(text is null){
            return null;
        }
        if(!decimal.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)){
            throw new SqlFunctionException("Error converting data type varchar to numeric.",8114);
        }
        return decimal.Round(number,scale,MidpointRounding.AwayFromZero);
    }
    /// <summary>文字列を float へ。読めなければ 8114。</summary>
    public static double? StringToDouble(string? text){
        if(text is null){
            return null;
        }
        if(!double.TryParse(text.Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out var number)){
            throw new SqlFunctionException("Error converting data type varchar to float.",8114);
        }
        return number;
    }
    /// <summary>文字列を bit へ。'TRUE'・'FALSE' と数字を受け付ける。0以外の数は 1。</summary>
    public static bool? StringToBit(string? text){
        if(text is null){
            return null;
        }
        var trimmed=text.Trim();
        if(trimmed.Equals("TRUE",StringComparison.OrdinalIgnoreCase)){
            return true;
        }
        if(trimmed.Equals("FALSE",StringComparison.OrdinalIgnoreCase)){
            return false;
        }
        if(!decimal.TryParse(trimmed,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)){
            throw new SqlFunctionException($"Conversion failed when converting the varchar value '{text}' to data type bit.",245);
        }
        return number!=0;
    }
    /// <summary>文字列を uniqueidentifier へ。読めなければ 8169。</summary>
    public static Guid? StringToGuid(string? text){
        if(text is null){
            return null;
        }
        if(!Guid.TryParse(text.Trim().Trim('{','}'),out var guid)){
            throw new SqlFunctionException("Conversion failed when converting from a character string to uniqueidentifier.",8169);
        }
        return guid;
    }
    /// <summary>文字列を varbinary へ。nvarchar は UTF-16(リトルエンディアン)、varchar は照合順序の文字コード。</summary>
    public static byte[]? StringToBinary(string? text,bool unicode,SqlCollation collation){
        if(text is null){
            return null;
        }
        if(unicode){
            return Encoding.Unicode.GetBytes(text);
        }
        return collation.Encoding.GetBytes(text);
    }
    // ---- 数値どうし・文字列の長さ ----
    /// <summary>decimal を整数型へ。0の方へ切り捨てる。型に入らなければ 8115。</summary>
    public static long? ToInteger(decimal? value,long minimum,long maximum,string typeName){
        if(value is not{} number){
            return null;
        }
        var truncated=decimal.Truncate(number);
        if(truncated<minimum||truncated>maximum){
            throw new SqlFunctionException($"Arithmetic overflow error converting numeric to data type {typeName}.",8115);
        }
        return (long)truncated;
    }
    /// <summary>float を整数型へ。0の方へ切り捨てる。型に入らなければ 8115。</summary>
    public static long? ToInteger(double? value,long minimum,long maximum,string typeName){
        if(value is not{} number){
            return null;
        }
        var truncated=Math.Truncate(number);
        if(truncated<minimum||truncated>maximum){
            throw new SqlFunctionException($"Arithmetic overflow error converting float to data type {typeName}.",8115);
        }
        return (long)truncated;
    }
    /// <summary>float を decimal へ。桁は呼び出し側が <see cref="SqlArithmetic.Fit"/> で合わせる。</summary>
    public static decimal? ToDecimal(double? value){
        if(value is not{} number){
            return null;
        }
        try{
            return (decimal)number;
        }catch(OverflowException){
            throw new SqlFunctionException("Arithmetic overflow error converting float to data type numeric.",8115);
        }
    }
    /// <summary>整数を長さの決まった文字列型へ。入らなければ "*"(本物と同じ)。</summary>
    public static string? IntegerToString(long? value,int length){
        if(IntegerToString(value) is not{} text){
            return null;
        }
        if(length>=0&&text.Length>length){
            return "*";
        }
        return text;
    }
    /// <summary>char(n)・nchar(n) へ。長すぎれば切り、短ければ空白で埋める。</summary>
    public static string? Fixed(string? text,int length){
        if(text is null){
            return null;
        }
        if(text.Length>length){
            return text[..length];
        }
        return text.PadRight(length);
    }
    /// <summary>bit を整数へ。</summary>
    public static int? BitToInteger(bool? value){
        if(value is not{} bit){
            return null;
        }
        if(bit){
            return 1;
        }
        return 0;
    }
    /// <summary>数を bit へ。0以外は 1。</summary>
    public static bool? ToBit(decimal? value){
        if(value is not{} number){
            return null;
        }
        return number!=0;
    }
    /// <summary>float を bit へ。0以外は 1。</summary>
    public static bool? ToBit(double? value){
        if(value is not{} number){
            return null;
        }
        return number!=0;
    }
    /// <summary>CAST・CONVERT で長さの決まった文字列型へ。長すぎる分は切り捨てる(明示の変換なのでエラーにしない)。</summary>
    public static string? Truncate(string? text,int length){
        if(text is null||length<0||text.Length<=length){
            return text;
        }
        return text[..length];
    }
    /// <summary>varbinary を文字列へ。nvarchar は UTF-16(リトルエンディアン。奇数の最後のバイトは捨てる)、varchar は照合順序の文字コードで読む。</summary>
    public static string? BinaryToString(byte[]? value,bool unicode,SqlCollation collation){
        if(value is null){
            return null;
        }
        if(unicode){
            return Encoding.Unicode.GetString(value,0,value.Length/2*2);
        }
        return collation.Encoding.GetString(value);
    }
    // ---- 補助 ----
    private static long? StringToInt64(string? text,string typeName){
        if(text is null){
            return null;
        }
        var trimmed=text.Trim();
        if(trimmed.Length==0){
            return 0;
        }
        if(!long.TryParse(trimmed,NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var number)){
            throw new SqlFunctionException($"Conversion failed when converting the varchar value '{text}' to data type {typeName}.",245);
        }
        return number;
    }
    private static string Fraction(DateTime date,SqlTemporalType type,byte scale){
        var ticks=date.Ticks%TimeSpan.TicksPerSecond;
        switch(type){
            case SqlTemporalType.DateTime:
                return (ticks/TimeSpan.TicksPerMillisecond).ToString("D3",CultureInfo.InvariantCulture);
            case SqlTemporalType.SmallDateTime:
                return "000";
            case SqlTemporalType.Date:
                return "";
            default:{
                if(scale==0){
                    return "";
                }
                return (ticks/SqlTemporal.Quantum(scale)).ToString("D"+scale,CultureInfo.InvariantCulture);
            }
        }
    }
    // style 126 の datetime は小数部が0のとき書かない。
    private static string TrimFraction(string fraction,SqlTemporalType type){
        if(type==SqlTemporalType.DateTime&&fraction.Trim('0').Length==0){
            return "";
        }
        return fraction;
    }
    private static string Dot(string fraction){
        if(fraction.Length==0){
            return "";
        }
        return "."+fraction;
    }
    // 秒と小数秒の区切り。datetime・smalldatetime は ":"、datetime2 などは "."(style 9・13・14 など)。
    private static string Separated(string fraction,SqlTemporalType type){
        if(type is SqlTemporalType.DateTime or SqlTemporalType.SmallDateTime){
            return Colon(fraction);
        }
        return Dot(fraction);
    }
    private static string Colon(string fraction){
        if(fraction.Length==0){
            return "";
        }
        return ":"+fraction;
    }
    private static string Clock12(DateTime date,bool seconds,string fraction,SqlTemporalType type){
        var hour=date.Hour%12;
        if(hour==0){
            hour=12;
        }
        var suffix="AM";
        if(date.Hour>=12){
            suffix="PM";
        }
        var text=hour.ToString(CultureInfo.InvariantCulture).PadLeft(2)+":"+date.Minute.ToString("D2",CultureInfo.InvariantCulture);
        if(seconds){
            text+=":"+date.Second.ToString("D2",CultureInfo.InvariantCulture)+Separated(fraction,type);
        }
        return text+suffix;
    }
    private static string Clock12Seconds(DateTime date){
        var hour=date.Hour%12;
        if(hour==0){
            hour=12;
        }
        var suffix="AM";
        if(date.Hour>=12){
            suffix="PM";
        }
        return hour.ToString(CultureInfo.InvariantCulture).PadLeft(2)+":"+date.Minute.ToString("D2",CultureInfo.InvariantCulture)+":"+date.Second.ToString("D2",CultureInfo.InvariantCulture)+" "+suffix;
    }
    // 1.23457e+006 の形。指数は符号と3桁。
    private static string Scientific(double number,int decimals,int exponentDigits){
        var text=number.ToString("0."+new string('0',decimals)+"e+"+new string('0',exponentDigits),CultureInfo.InvariantCulture);
        return text;
    }
}
