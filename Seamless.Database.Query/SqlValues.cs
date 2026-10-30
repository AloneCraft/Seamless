using System.Numerics;
using System.Globalization;
namespace Seamless.Database.Query;
internal static class SqlValues{
    internal static object?ConvertTo(object?value,SqlType type,bool explicitConversion=false){
        if(value is null||value is DBNull){return null;}
        if(type.ClrType==typeof(string)){string text;if(value is byte[]encoded){text=System.Text.Encoding.Unicode.GetString(encoded);}else{text=Convert.ToString(value,CultureInfo.InvariantCulture)??"";}return ConvertString(text,type,explicitConversion);}
        if(type.ClrType==typeof(byte[])){if(value is byte[]bytes){if(type.Length is{}length&&length>=0){if(bytes.Length>length){if(!explicitConversion){throw new SqlQueryException("Binary conversion would truncate.",8152);}return bytes[..length];}if(type.Name=="binary"&&bytes.Length<length){var padded=new byte[length];bytes.CopyTo(padded,0);return padded;}}return bytes;}throw new SqlQueryException("Unsupported binary conversion.",245);}
        if(value is string empty&&string.IsNullOrWhiteSpace(empty)&&type.ClrType!=typeof(Guid)&&type.ClrType!=typeof(TimeSpan)&&type.ClrType!=typeof(DateTimeOffset)){if(type.ClrType==typeof(DateTime)){return new DateTime(1900,1,1);}value=0;}
        if(type.ClrType==typeof(bool)){if(value is bool boolean){return boolean;}if(value is string text&&bool.TryParse(text,out var parsed)){return parsed;}return Convert.ToDecimal(value,CultureInfo.InvariantCulture)!=0;}
        if(type.ClrType==typeof(DateOnly)){if(value is DateOnly dateOnly){return dateOnly;}if(value is DateTime dateValue){return DateOnly.FromDateTime(dateValue);}return DateOnly.FromDateTime(ParseDateTime(Convert.ToString(value,CultureInfo.InvariantCulture)!));}
        if(type.ClrType==typeof(TimeOnly)){if(value is TimeOnly timeOnly){return timeOnly;}if(value is TimeSpan timeSpan){return TimeOnly.FromTimeSpan(timeSpan);}if(value is DateTime dateValue){return TimeOnly.FromDateTime(dateValue);}return TimeOnly.Parse(Convert.ToString(value,CultureInfo.InvariantCulture)!,CultureInfo.InvariantCulture);}
        if(type.ClrType==typeof(DateTime)){if(value is DateOnly dateOnly){return dateOnly.ToDateTime(TimeOnly.MinValue);}if(value is TimeOnly timeOnly){return new DateTime(1900,1,1).Add(timeOnly.ToTimeSpan());}if(value is string text){return ParseDateTime(text);}return Convert.ToDateTime(value,CultureInfo.InvariantCulture);}
        if(type.ClrType==typeof(Guid)){if(value is Guid guid){return guid;}return Guid.Parse(Convert.ToString(value,CultureInfo.InvariantCulture)!);}
        if(type.ClrType==typeof(TimeSpan)){if(value is TimeSpan span){return span;}if(value is TimeOnly timeOnly){return timeOnly.ToTimeSpan();}return TimeSpan.Parse(Convert.ToString(value,CultureInfo.InvariantCulture)!,CultureInfo.InvariantCulture);}
        if(type.ClrType==typeof(DateTimeOffset)){if(value is DateTimeOffset offset){return offset;}return DateTimeOffset.Parse(Convert.ToString(value,CultureInfo.InvariantCulture)!,CultureInfo.InvariantCulture);}
        if(type.ClrType==typeof(decimal)){var number=Convert.ToDecimal(value,CultureInfo.InvariantCulture);if(type.Scale is{}scale&&scale<29&&decimal.Round(number,scale)!=number){if(explicitConversion){number=decimal.Round(number,scale,MidpointRounding.AwayFromZero);}else{throw new SqlQueryException("Decimal conversion would lose scale.",8115);}}CheckDecimalPrecision(number,type);return number;}
        if(type.ClrType==typeof(int)){return checked((int)decimal.Truncate(Convert.ToDecimal(value,CultureInfo.InvariantCulture)));}
        if(type.ClrType==typeof(long)){return checked((long)decimal.Truncate(Convert.ToDecimal(value,CultureInfo.InvariantCulture)));}
        if(type.ClrType==typeof(short)){return checked((short)decimal.Truncate(Convert.ToDecimal(value,CultureInfo.InvariantCulture)));}
        if(type.ClrType==typeof(byte)){return checked((byte)decimal.Truncate(Convert.ToDecimal(value,CultureInfo.InvariantCulture)));}
        if(value.GetType()==type.ClrType){return value;}
        try{return Convert.ChangeType(value,type.ClrType,CultureInfo.InvariantCulture);}catch(Exception exception)when(exception is InvalidCastException or FormatException or OverflowException){throw new SqlQueryException("SQL conversion failed: "+exception.Message,245);}
    }
    private static string SupportedCollation(string collation){var name=collation.ToUpperInvariant();if(name is "SQL_LATIN1_GENERAL_CP1_CI_AS" or "JAPANESE_CI_AS" or "LATIN1_GENERAL_100_CI_AS" or "LATIN1_GENERAL_BIN2" or "LATIN1_GENERAL_100_BIN2" or "LATIN1_GENERAL_100_BIN2_UTF8"){return name;}throw new SqlQueryException("Unsupported collation: "+collation,40515);}
    private static System.Text.Encoding?StringEncoding(SqlType type){
        if(type.Name is not("varchar" or "char" or "text")||type.Collation is null){return null;}
        var name=SupportedCollation(type.Collation);int codePage;
        if(name.StartsWith("JAPANESE",StringComparison.Ordinal)){codePage=932;}
        else if(name.StartsWith("LATIN1_GENERAL",StringComparison.Ordinal)||name.StartsWith("SQL_LATIN1_GENERAL_CP1_",StringComparison.Ordinal)){codePage=1252;}
        else{throw new SqlQueryException("Unsupported collation encoding: "+type.Collation,40515);}
        if(name.EndsWith("_UTF8",StringComparison.Ordinal)){codePage=65001;}
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        return System.Text.Encoding.GetEncoding(codePage);
    }
    internal static int StringLength(string text,SqlType type){var encoding=StringEncoding(type);if(encoding is not null){return encoding.GetByteCount(text);}return text.Length;}
    internal static object?DataLength(object?value,SqlType type){if(value is null){return null;}if(value is byte[]bytes){return bytes.Length;}if(value is string text){if(type.Name is "nvarchar" or "nchar" or "ntext"){return checked(text.Length*2);}if(type.Name is "varchar" or "char" or "text"){return StringLength(text,type);}throw new SqlQueryException("Unsupported DATALENGTH input.",40515);}throw new SqlQueryException("Unsupported DATALENGTH input.",40515);}
    private static string ConvertString(string text,SqlType type,bool explicitConversion){
        var encoding=StringEncoding(type);var size=text.Length;
        if(encoding is not null){var bytes=encoding.GetBytes(text);size=bytes.Length;text=encoding.GetString(bytes);if(type.Length is{}limit&&limit>=0&&size>limit){if(!explicitConversion){throw new SqlQueryException("String conversion would truncate.",8152);}var count=limit;var strict=System.Text.Encoding.GetEncoding(encoding.CodePage,System.Text.EncoderFallback.ExceptionFallback,System.Text.DecoderFallback.ExceptionFallback);while(true){try{text=strict.GetString(bytes,0,count);break;}catch(System.Text.DecoderFallbackException){count--;}}size=count;}}
        else if(type.Length is{}limit&&limit>=0&&size>limit){if(!explicitConversion){throw new SqlQueryException("String conversion would truncate.",8152);}text=text[..limit];size=text.Length;}
        if(type.Name is "char" or "nchar"&&type.Length is{}length&&length>size){text+=new string(' ',length-size);}
        return text;
    }
    private static SqlType CommonStringType(SqlType left,SqlType right){
        int?length=-1;if(left.Length is{}first&&right.Length is{}second&&first>=0&&second>=0){length=Math.Max(first,second);}
        var name=left.Name;if(StringPrecedence(right.Name)>StringPrecedence(name)){name=right.Name;}
        var selected=left;if(left.Collation is null){selected=right;}else if(right.Collation is not null){
            if(left.Coercibility==SqlCollationCoercibility.CoercibleDefault&&right.Coercibility==SqlCollationCoercibility.Implicit){selected=right;}
            else if(left.Coercibility==right.Coercibility&&!string.Equals(left.Collation,right.Collation,StringComparison.OrdinalIgnoreCase)){throw new SqlQueryException("Collation conflict in scalar expression.",468);}
        }
        return new(name,typeof(string),length,null,null,selected.Collation){Coercibility=selected.Coercibility};
    }
    private static int StringPrecedence(string name)=>name switch{"ntext"=>6,"text"=>5,"nvarchar"=>4,"nchar"=>3,"varchar"=>2,"char"=>1,_=>0};
    internal static SqlType CommonType(IEnumerable<SqlType>types){
        using var iterator=types.GetEnumerator();SqlType?result=null;var hasUntypedNull=false;while(iterator.MoveNext()){if(iterator.Current.IsUntypedNull){hasUntypedNull=true;continue;}result=iterator.Current;break;}if(result is null){if(hasUntypedNull){return SqlType.Int;}throw new SqlQueryException("A CASE/COALESCE result requires a typed expression.",8133);}
        while(iterator.MoveNext()){
            var next=iterator.Current;if(next.IsUntypedNull){continue;}
            if(result.ClrType==typeof(string)&&next.ClrType==typeof(string)){
                result=CommonStringType(result,next);continue;
            }
            if(result.ClrType==typeof(byte[])&&next.ClrType==typeof(byte[])){int?length=-1;if(result.Length is{}first&&next.Length is{}second&&first>=0&&second>=0){length=Math.Max(first,second);}var name="binary";if(result.Name=="varbinary"||next.Name=="varbinary"){name="varbinary";}if(result.Name=="image"||next.Name=="image"){throw new SqlQueryException("Unsupported image common type.",40515);}result=new(name,typeof(byte[]),length);continue;}
            if(result.ClrType==next.ClrType){if(result.ClrType==typeof(decimal)){result=CommonDecimalType(result,next);}else if(result.ClrType==typeof(int)){byte?literal=null;if(result.LiteralPrecision is{}left&&next.LiteralPrecision is{}right){literal=Math.Max(left,right);}result=SqlType.Int with{LiteralPrecision=literal};}continue;}
            if(result.ClrType==typeof(string)){result=next;continue;}if(next.ClrType==typeof(string)){continue;}
            if(result.ClrType==typeof(DateTime)||next.ClrType==typeof(DateTime)){result=new("datetime",typeof(DateTime));continue;}
            if(result.ClrType==typeof(double)||next.ClrType==typeof(double)){result=new("float",typeof(double));continue;}if(result.ClrType==typeof(float)||next.ClrType==typeof(float)){result=new("real",typeof(float));continue;}
            if(result.ClrType==typeof(decimal)||next.ClrType==typeof(decimal)){result=CommonDecimalType(result,next);continue;}
            if(result.ClrType==typeof(long)||next.ClrType==typeof(long)){result=SqlType.BigInt;continue;}result=SqlType.Int;
        }
        return result;
    }
    private static SqlType CommonDecimalType(SqlType left,SqlType right){var(p1,s1)=DecimalShape(left);var(p2,s2)=DecimalShape(right);var integral=Math.Max(p1-s1,p2-s2);var scale=Math.Min(Math.Max(s1,s2),38-integral);return new("decimal",typeof(decimal),null,(byte)(integral+scale),(byte)scale);}
    internal static SqlType Promote(SqlType left,SqlType right,SqlBinaryOperator operation){
        if(left.ClrType==typeof(string)&&right.ClrType==typeof(string)&&operation==SqlBinaryOperator.Add){int?length=null;if(left.Length is{}a&&right.Length is{}b){length=-1;if(a>=0&&b>=0){length=checked(a+b);}}var name="varchar";if(left.Name.StartsWith('n')||right.Name.StartsWith('n')){name="nvarchar";}return CommonStringType(left,right) with{Name=name,Length=length};}
        if(left.ClrType==typeof(double)||right.ClrType==typeof(double)){return new("float",typeof(double));}
        if(left.ClrType==typeof(float)||right.ClrType==typeof(float)){return new("real",typeof(float));}
        if((left.Name is "money" or "smallmoney"||right.Name is "money" or "smallmoney")&&left.Name is not("decimal" or "numeric")&&right.Name is not("decimal" or "numeric")){var name="smallmoney";byte precision=10;if(left.Name=="money"||right.Name=="money"){name="money";precision=19;}return new(name,typeof(decimal),null,precision,4);}
        if(left.ClrType==typeof(decimal)||right.ClrType==typeof(decimal)){
            var(p1,s1)=DecimalShape(left);var(p2,s2)=DecimalShape(right);int precision,scale;
            switch(operation){case SqlBinaryOperator.Multiply:precision=p1+p2+1;scale=s1+s2;break;case SqlBinaryOperator.Divide:scale=Math.Max(6,s1+p2+1);precision=p1-s1+s2+scale;break;case SqlBinaryOperator.Modulo:scale=Math.Max(s1,s2);precision=Math.Min(p1-s1,p2-s2)+scale;break;default:scale=Math.Max(s1,s2);precision=Math.Max(p1-s1,p2-s2)+scale+1;break;}
            if(precision>38){var integral=precision-scale;if(operation is SqlBinaryOperator.Multiply or SqlBinaryOperator.Divide&&integral>=32){if(scale>6){scale=6;}}else{scale=Math.Max(0,Math.Min(scale,38-integral));}precision=38;}
            return new("decimal",typeof(decimal),null,(byte)precision,(byte)scale);
        }
        if(left.ClrType==typeof(long)||right.ClrType==typeof(long)){return SqlType.BigInt;}
        return SqlType.Int;
    }
    private static (int Precision,int Scale)DecimalShape(SqlType type){if(type.LiteralPrecision is{}literalPrecision){return(literalPrecision,0);}if(type.ClrType==typeof(decimal)){return(type.Precision??28,type.Scale??6);}if(type.ClrType==typeof(long)){return(19,0);}if(type.ClrType==typeof(short)){return(5,0);}if(type.ClrType==typeof(byte)){return(3,0);}return(10,0);}
    internal static object?Unary(object?value,SqlUnaryOperator operation){
        if(value is null){return null;}
        if(operation==SqlUnaryOperator.Positive){return value;}
        if(operation is not(SqlUnaryOperator.Negative or SqlUnaryOperator.BitwiseNot)){throw new SqlQueryException("Unsupported unary operation.",40515);}
        if(operation==SqlUnaryOperator.BitwiseNot){if(value is int bitInteger){return ~bitInteger;}if(value is short small){return(short)~small;}if(value is byte tiny){return(byte)~tiny;}return ~Convert.ToInt64(value,CultureInfo.InvariantCulture);}
        if(value is int integer){return checked(-integer);}if(value is long big){return checked(-big);}if(value is decimal number){return -number;}if(value is double real){return -real;}if(value is float single){return -single;}return -Convert.ToInt32(value,CultureInfo.InvariantCulture);
    }
    internal static object?Binary(object?left,object?right,SqlBinaryOperator operation,SqlType type){
        if(left is null||right is null){return null;}
        if(type.ClrType==typeof(string)&&operation==SqlBinaryOperator.Add){return Convert.ToString(left,CultureInfo.InvariantCulture)+Convert.ToString(right,CultureInfo.InvariantCulture);}
        if(type.ClrType==typeof(double)||type.ClrType==typeof(float)){var a=Convert.ToDouble(left,CultureInfo.InvariantCulture);var b=Convert.ToDouble(right,CultureInfo.InvariantCulture);if(b==0&&operation is SqlBinaryOperator.Divide or SqlBinaryOperator.Modulo){throw new SqlQueryException("Divide by zero error encountered.",8134);}double number=operation switch{SqlBinaryOperator.Add=>a+b,SqlBinaryOperator.Subtract=>a-b,SqlBinaryOperator.Multiply=>a*b,SqlBinaryOperator.Divide=>a/b,SqlBinaryOperator.Modulo=>a%b,_=>throw new SqlQueryException("Unsupported floating operation.")};if(!double.IsFinite(number)){throw new SqlQueryException("Invalid floating point result.",8115);}if(type.ClrType==typeof(float)){var single=(float)number;if(!float.IsFinite(single)){throw new SqlQueryException("Floating point overflow.",8115);}return single;}return number;}
        if(type.ClrType==typeof(decimal)){return DecimalBinary(Convert.ToDecimal(left,CultureInfo.InvariantCulture),Convert.ToDecimal(right,CultureInfo.InvariantCulture),operation,type);}
        var first=Convert.ToInt64(left,CultureInfo.InvariantCulture);var second=Convert.ToInt64(right,CultureInfo.InvariantCulture);long result=operation switch{SqlBinaryOperator.Add=>checked(first+second),SqlBinaryOperator.Subtract=>checked(first-second),SqlBinaryOperator.Multiply=>checked(first*second),SqlBinaryOperator.Divide=>first/second,SqlBinaryOperator.Modulo=>first%second,SqlBinaryOperator.BitwiseAnd=>first&second,SqlBinaryOperator.BitwiseOr=>first|second,SqlBinaryOperator.BitwiseXor=>first^second,_=>throw new SqlQueryException("Unsupported integer operation.")};if(type.ClrType==typeof(long)){return result;}return checked((int)result);
    }
    private static decimal DecimalBinary(decimal left,decimal right,SqlBinaryOperator operation,SqlType type){
        var(a,firstScale)=DecimalParts(left);var(b,secondScale)=DecimalParts(right);BigInteger numerator,denominator;
        switch(operation){
            case SqlBinaryOperator.Add:case SqlBinaryOperator.Subtract:case SqlBinaryOperator.Modulo:{var common=Math.Max(firstScale,secondScale);a*=BigInteger.Pow(10,common-firstScale);b*=BigInteger.Pow(10,common-secondScale);denominator=BigInteger.Pow(10,common);if(operation==SqlBinaryOperator.Add){numerator=a+b;}else if(operation==SqlBinaryOperator.Subtract){numerator=a-b;}else{if(b.IsZero){throw new DivideByZeroException();}numerator=a%b;}break;}
            case SqlBinaryOperator.Multiply:numerator=a*b;denominator=BigInteger.Pow(10,firstScale+secondScale);break;
            case SqlBinaryOperator.Divide:if(b.IsZero){throw new DivideByZeroException();}numerator=a*BigInteger.Pow(10,secondScale);denominator=b*BigInteger.Pow(10,firstScale);break;
            default:throw new SqlQueryException("Unsupported decimal operation.",40515);
        }
        var scale=(int)(type.Scale??28);if(denominator.Sign<0){numerator=-numerator;denominator=-denominator;}var scaled=numerator*BigInteger.Pow(10,scale);var coefficient=BigInteger.DivRem(scaled,denominator,out var remainder);if(BigInteger.Abs(remainder)*2>=denominator){coefficient+=scaled.Sign;}
        while(scale>0&&coefficient%10==0){coefficient/=10;scale--;}
        var negative=coefficient.Sign<0;coefficient=BigInteger.Abs(coefficient);if(scale>28||coefficient>BigInteger.Pow(2,96)-1){throw new OverflowException("Decimal result exceeds the supported CLR decimal range.");}
        var mask=new BigInteger(uint.MaxValue);var lo=unchecked((int)(uint)(coefficient&mask));var mid=unchecked((int)(uint)((coefficient>>32)&mask));var hi=unchecked((int)(uint)((coefficient>>64)&mask));var result=new decimal(lo,mid,hi,negative,(byte)scale);CheckDecimalPrecision(result,type);return result;
    }
    private static (BigInteger Coefficient,int Scale)DecimalParts(decimal value){var bits=decimal.GetBits(value);var coefficient=(BigInteger)(uint)bits[0]|((BigInteger)(uint)bits[1]<<32)|((BigInteger)(uint)bits[2]<<64);if((bits[3]&int.MinValue)!=0){coefficient=-coefficient;}return(coefficient,(bits[3]>>16)&255);}
    private static void CheckDecimalPrecision(decimal value,SqlType type){if(type.Name=="money"&&(value>922337203685477.5807m||value< -922337203685477.5808m)||type.Name=="smallmoney"&&(value>214748.3647m||value< -214748.3648m)){throw new SqlQueryException("Money arithmetic overflow.",8115);}if(type.Precision is{}precision&&type.Scale is{}scale){var integral=decimal.Truncate(Math.Abs(value));var digits=0;if(integral!=0){digits=integral.ToString("0",CultureInfo.InvariantCulture).Length;}if(digits>precision-scale){throw new SqlQueryException("Arithmetic overflow converting decimal precision.",8115);}}}
    internal static bool?Compare(object?left,object?right,SqlComparisonOperator operation,SqlType?type=null){
        if(left is null||right is null){return null;}
        if(type is not null&&type.ClrType!=typeof(string)){
            if(left is string||left is bool&&type.ClrType!=typeof(bool)){left=ConvertTo(left,type,true);}
            if(right is string||right is bool&&type.ClrType!=typeof(bool)){right=ConvertTo(right,type,true);}
        }
        var result=CompareValue(left,right,type);
        return operation switch{SqlComparisonOperator.Equals=>result==0,SqlComparisonOperator.NotEqual=>result!=0,SqlComparisonOperator.GreaterThan=>result>0,SqlComparisonOperator.LessThan=>result<0,SqlComparisonOperator.GreaterThanOrEqualTo=>result>=0,SqlComparisonOperator.LessThanOrEqualTo=>result<=0,_=>throw new SqlQueryException("Unsupported comparison.")};
    }
    internal static int CompareValue(object?left,object?right,SqlType?type=null){
        if(left is DateOnly leftDate){left=leftDate.ToDateTime(TimeOnly.MinValue);}if(right is DateOnly rightDate){right=rightDate.ToDateTime(TimeOnly.MinValue);}
        if(left is TimeOnly leftTime){left=leftTime.ToTimeSpan();}if(right is TimeOnly rightTime){right=rightTime.ToTimeSpan();}
        if(ReferenceEquals(left,right)){return 0;}if(left is null){return -1;}if(right is null){return 1;}
        if(left is string a&&right is string b){return CompareText(a.TrimEnd(' '),b.TrimEnd(' '),type?.Collation);}
        if(left is byte[]first&&right is byte[]second){return new System.Data.SqlTypes.SqlBinary(first).CompareTo(new System.Data.SqlTypes.SqlBinary(second));}
        if(left is Guid firstGuid&&right is Guid secondGuid){return new System.Data.SqlTypes.SqlGuid(firstGuid).CompareTo(new System.Data.SqlTypes.SqlGuid(secondGuid));}
        if(IsNumber(left)&&IsNumber(right)){if(left is double or float||right is double or float){return Convert.ToDouble(left,CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(right,CultureInfo.InvariantCulture));}return Convert.ToDecimal(left,CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right,CultureInfo.InvariantCulture));}
        if(left is IComparable comparable){return comparable.CompareTo(right);}
        throw new SqlQueryException("Values cannot be compared: "+left.GetType());
    }
    private static int CompareText(string left,string right,string?collation){
        if(string.IsNullOrEmpty(collation)){return string.Compare(left,right,StringComparison.Ordinal);}
        var normalized=SupportedCollation(collation);if(normalized.Contains("_BIN",StringComparison.Ordinal)){return string.Compare(left,right,StringComparison.Ordinal);}
        var culture="en-US";if(normalized.StartsWith("JAPANESE",StringComparison.Ordinal)){culture="ja-JP";}else if(!normalized.Contains("LATIN1_GENERAL",StringComparison.Ordinal)){throw new SqlQueryException("Unsupported collation: "+collation,40515);}
        var options=System.Globalization.CompareOptions.None;if(normalized.Contains("_CI_",StringComparison.Ordinal)||normalized.EndsWith("_CI",StringComparison.Ordinal)){options|=System.Globalization.CompareOptions.IgnoreCase;}if(normalized.Contains("_AI",StringComparison.Ordinal)){options|=System.Globalization.CompareOptions.IgnoreNonSpace;}if(!normalized.Contains("_KS",StringComparison.Ordinal)){options|=System.Globalization.CompareOptions.IgnoreKanaType;}if(!normalized.Contains("_WS",StringComparison.Ordinal)){options|=System.Globalization.CompareOptions.IgnoreWidth;}
        return System.Globalization.CultureInfo.GetCultureInfo(culture).CompareInfo.Compare(left,right,options);
    }
    internal static object?ConvertDate(object?value,SqlType type,int style){
        if(value is null){return null;}if(value is not string text){return ConvertTo(value,type,true);}DateTime date;if(style==112){if(!DateTime.TryParseExact(text,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out date)){if(!text.Contains('-',StringComparison.Ordinal)){throw new SqlQueryException("Conversion failed for date style 112.",241);}date=ParseDateTime(text);}}else{date=ParseDateTime(text);}if(type.ClrType==typeof(DateOnly)){return DateOnly.FromDateTime(date);}return date;
    }
    private static DateTime ParseDateTime(string value){if(DateTime.TryParseExact(value,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var date)){return date;}return DateTime.Parse(value,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces);}
    private static DateTime AsDateTime(object?value,bool normalizeOffset=false){if(value is DateOnly date){return date.ToDateTime(TimeOnly.MinValue);}if(value is TimeOnly time){return new DateTime(1900,1,1).Add(time.ToTimeSpan());}if(value is DateTimeOffset offset){if(normalizeOffset){return offset.UtcDateTime;}return offset.DateTime;}return Convert.ToDateTime(value,CultureInfo.InvariantCulture);}
    private static int DateDiff(string part,DateTime first,DateTime second){
        return part.ToUpperInvariant() switch{"YEAR" or "YY" or "YYYY"=>second.Year-first.Year,"MONTH" or "MM" or "M"=>checked((second.Year-first.Year)*12+second.Month-first.Month),"DAY" or "DD" or "D"=>checked((int)((second.Date.Ticks-first.Date.Ticks)/TimeSpan.TicksPerDay)),"HOUR" or "HH"=>checked((int)(second.Ticks/TimeSpan.TicksPerHour-first.Ticks/TimeSpan.TicksPerHour)),"MINUTE" or "MI" or "N"=>checked((int)(second.Ticks/TimeSpan.TicksPerMinute-first.Ticks/TimeSpan.TicksPerMinute)),"SECOND" or "SS" or "S"=>checked((int)(second.Ticks/TimeSpan.TicksPerSecond-first.Ticks/TimeSpan.TicksPerSecond)),_=>throw new SqlQueryException("Unsupported datepart.",40515)};
    }
    private static DateTime DateAdd(string part,int count,DateTime date)=>part.ToUpperInvariant() switch{"YEAR" or "YY" or "YYYY"=>date.AddYears(count),"MONTH" or "MM" or "M"=>date.AddMonths(count),"DAY" or "DD" or "D"=>date.AddDays(count),"HOUR" or "HH"=>date.AddHours(count),"MINUTE" or "MI" or "N"=>date.AddMinutes(count),"SECOND" or "SS" or "S"=>date.AddSeconds(count),"MILLISECOND" or "MS"=>date.AddMilliseconds(count),_=>throw new SqlQueryException("Unsupported datepart.",40515)};
    private static bool IsNumber(object value)=>value is byte or short or int or long or decimal or float or double;
    internal static object?Function(string name,object?[]args,string database,DateTime timestamp){
        if(name=="DB_NAME"){return database;}
        if(name=="GETDATE"){return timestamp;}
        if(name=="ISNULL"){return args[0]??args[1];}
        if(name=="CONCAT"){return string.Concat(args.Select(x=>Convert.ToString(x,CultureInfo.InvariantCulture)));}
        if(args.Any(x=>x is null)){return null;}
        var text=Convert.ToString(args[0],CultureInfo.InvariantCulture)!;
        switch(name){
            case "DATEDIFF":return DateDiff(Convert.ToString(args[0],CultureInfo.InvariantCulture)!,AsDateTime(args[1],true),AsDateTime(args[2],true));
            case "DATEADD":{var part=Convert.ToString(args[0],CultureInfo.InvariantCulture)!;var count=checked((int)decimal.Truncate(Convert.ToDecimal(args[1],CultureInfo.InvariantCulture)));if(args[2] is DateTimeOffset offset){return new DateTimeOffset(DateAdd(part,count,offset.DateTime),offset.Offset);}return DateAdd(part,count,AsDateTime(args[2]));}
            case "DATEPART":{var date=AsDateTime(args[1]);return Convert.ToString(args[0],CultureInfo.InvariantCulture)!.ToUpperInvariant() switch{"YEAR" or "YY" or "YYYY"=>date.Year,"MONTH" or "MM" or "M"=>date.Month,"DAY" or "DD" or "D"=>date.Day,_=>throw new SqlQueryException("Unsupported datepart.",40515)};}
            case "DECOMPRESS":{using var input=new MemoryStream((byte[])args[0]!);using var gzip=new System.IO.Compression.GZipStream(input,System.IO.Compression.CompressionMode.Decompress);using var output=new MemoryStream();gzip.CopyTo(output);return output.ToArray();}
            case "LEFT":{var count=Convert.ToInt32(args[1],CultureInfo.InvariantCulture);if(count<0){throw new SqlQueryException("Invalid length.",537);}return text[..Math.Min(count,text.Length)];}
            case "RIGHT":{var count=Convert.ToInt32(args[1],CultureInfo.InvariantCulture);if(count<0){throw new SqlQueryException("Invalid length.",537);}return text[Math.Max(0,text.Length-count)..];}
            case "SUBSTRING":{var start=Convert.ToInt32(args[1],CultureInfo.InvariantCulture);var count=Convert.ToInt32(args[2],CultureInfo.InvariantCulture);if(count<0){throw new SqlQueryException("Invalid length.",537);}if(start<1){count=Math.Max(0,count+start-1);start=1;}var offset=Math.Min(text.Length,start-1);return text.Substring(offset,Math.Min(count,text.Length-offset));}
            case "REPLACE":return text.Replace(Convert.ToString(args[1],CultureInfo.InvariantCulture)!,Convert.ToString(args[2],CultureInfo.InvariantCulture)!,StringComparison.Ordinal);
            case "REPLICATE":{var count=Convert.ToInt32(args[1],CultureInfo.InvariantCulture);if(count<0){return null;}return string.Concat(Enumerable.Repeat(text,count));}
            case "LEN":return text.TrimEnd(' ').Length;case "DATALENGTH":return checked(text.Length*2);
            case "UPPER":return text.ToUpperInvariant();case "LOWER":return text.ToLowerInvariant();case "LTRIM":return text.TrimStart(' ');case "RTRIM":return text.TrimEnd(' ');
            case "CHARINDEX":return Convert.ToString(args[1],CultureInfo.InvariantCulture)!.IndexOf(text,StringComparison.Ordinal)+1;
            case "YEAR":return AsDateTime(args[0]).Year;case "MONTH":return AsDateTime(args[0]).Month;case "DAY":return AsDateTime(args[0]).Day;
            case "ABS":{if(args[0] is int i){return Math.Abs(i);}if(args[0] is long l){return Math.Abs(l);}if(args[0] is decimal d){return Math.Abs(d);}return Math.Abs(Convert.ToDouble(args[0],CultureInfo.InvariantCulture));}
            case "ROUND":return Math.Round(Convert.ToDecimal(args[0],CultureInfo.InvariantCulture),Convert.ToInt32(args[1],CultureInfo.InvariantCulture),MidpointRounding.AwayFromZero);
            default:throw new SqlQueryException("Unsupported function: "+name,40515);
        }
    }
}
internal sealed class SqlRowComparer(bool[]?descending=null,SqlType[]?types=null):IComparer<object?[]>{
    private readonly bool[]?descending=descending;
    private readonly SqlType[]?types=types;
    public int Compare(object?[]?left,object?[]?right){if(ReferenceEquals(left,right)){return 0;}if(left is null){return -1;}if(right is null){return 1;}for(var i=0;i<Math.Min(left.Length,right.Length);i++){SqlType?type=null;if(this.types is not null){type=this.types[i];}var result=SqlValues.CompareValue(left[i],right[i],type);if(result!=0){if(this.descending is not null&&this.descending[i]){return -result;}return result;}}return left.Length.CompareTo(right.Length);}
}


