using System.Globalization;
using System.Text;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 文字列の関数。引数のどれかが NULL なら NULL を返す(CONCAT・CONCAT_WS は除く)。
/// 検索・置換・大文字小文字は照合順序に従う。結果の長さの上限(varchar(8000) など)は、翻訳器が式の型で切り詰める。
/// </summary>
public static class StringFunctions{
    // ---- 文字と番号 ----
    /// <summary>ASCII。先頭の文字の、照合順序の文字コードでの番号。空文字は NULL。</summary>
    [SqlFunction("ASCII")]
    public static int? Ascii(string? text,SqlCollation collation){
        if(string.IsNullOrEmpty(text)){
            return null;
        }
        var bytes=collation.Encoding.GetBytes(text[..1]);
        return bytes[0];
    }
    /// <summary>CHAR。照合順序(本物ではデータベースの既定の照合順序)の文字コードで番号の文字。0〜255 の外と、1文字にならない番号は NULL。</summary>
    [SqlFunction("CHAR")]
    public static string? Char(int? code,SqlCollation collation){
        if(code is not{} value||value<0||value>255){
            return null;
        }
        // 2バイト文字の文字コード(932 など)で先頭バイトに当たる番号は、1文字にならないので NULL。
        var strict=collation.StrictEncoding;
        try{
            return strict.GetString([(byte)value]);
        }catch(DecoderFallbackException){
            return null;
        }
    }
    /// <summary>NCHAR。UTF-16 の番号の文字。0〜65535 の外は NULL。</summary>
    [SqlFunction("NCHAR")]
    public static string? NChar(int? code){
        if(code is not{} value||value<0||value>0xFFFF){
            return null;
        }
        return ((char)value).ToString();
    }
    /// <summary>UNICODE。先頭の文字の UTF-16 の番号。空文字は NULL。</summary>
    [SqlFunction("UNICODE")]
    public static int? Unicode(string? text){
        if(string.IsNullOrEmpty(text)){
            return null;
        }
        return text[0];
    }
    // ---- 長さ ----
    /// <summary>LEN。末尾の空白を除いた文字数。</summary>
    [SqlFunction("LEN")]
    public static int? Len(string? text)=>text?.TrimEnd(' ').Length;
    /// <summary>DATALENGTH の文字列版。nvarchar は2バイト×文字数、varchar は照合順序の文字コードでのバイト数。末尾の空白も数える。</summary>
    [SqlFunction("DATALENGTH")]
    public static int? DataLength(string? text,bool unicode,SqlCollation collation){
        if(text is null){
            return null;
        }
        if(unicode){
            return checked(text.Length*2);
        }
        return collation.Encoding.GetByteCount(text);
    }
    /// <summary>DATALENGTH の varbinary 版。</summary>
    [SqlFunction("DATALENGTH")]
    public static int? DataLength(byte[]? value)=>value?.Length;
    // ---- 切り出し ----
    /// <summary>LEFT。長さが負なら 536。</summary>
    [SqlFunction("LEFT")]
    public static string? Left(string? text,long? length){
        if(text is null||length is not{} count){
            return null;
        }
        if(count<0){
            throw new SqlFunctionException("Invalid length parameter passed to the left function.",536);
        }
        return text[..(int)Math.Min(count,text.Length)];
    }
    /// <summary>RIGHT。長さが負なら 536。</summary>
    [SqlFunction("RIGHT")]
    public static string? Right(string? text,long? length){
        if(text is null||length is not{} count){
            return null;
        }
        if(count<0){
            throw new SqlFunctionException("Invalid length parameter passed to the right function.",536);
        }
        return text[(int)Math.Max(0,text.Length-count)..];
    }
    /// <summary>SUBSTRING。開始位置が1より前なら、その分だけ長さを減らす。長さが負なら 536。</summary>
    [SqlFunction("SUBSTRING")]
    public static string? Substring(string? text,long? start,long? length){
        if(text is null||start is not{} from||length is not{} count){
            return null;
        }
        if(count<0){
            throw new SqlFunctionException("Invalid length parameter passed to the substring function.",536);
        }
        var end=from+count;
        if(from<1){
            from=1;
        }
        if(end>text.Length+1){
            end=text.Length+1;
        }
        if(from>text.Length||end<=from){
            return "";
        }
        return text.Substring((int)from-1,(int)(end-from));
    }
    /// <summary>SUBSTRING の varbinary 版。</summary>
    [SqlFunction("SUBSTRING")]
    public static byte[]? Substring(byte[]? value,long? start,long? length){
        if(value is null||start is not{} from||length is not{} count){
            return null;
        }
        if(count<0){
            throw new SqlFunctionException("Invalid length parameter passed to the substring function.",536);
        }
        var end=from+count;
        if(from<1){
            from=1;
        }
        if(end>value.Length+1){
            end=value.Length+1;
        }
        if(from>value.Length||end<=from){
            return [];
        }
        return value[(int)(from-1)..(int)(end-1)];
    }
    // ---- 空白 ----
    /// <summary>LTRIM。先頭の空白(または characters の文字)を除く。</summary>
    [SqlFunction("LTRIM")]
    public static string? LTrim(string? text,string? characters=" "){
        if(text is null||characters is null){
            return null;
        }
        return text.TrimStart(characters.ToCharArray());
    }
    /// <summary>RTRIM。末尾の空白(または characters の文字)を除く。</summary>
    [SqlFunction("RTRIM")]
    public static string? RTrim(string? text,string? characters=" "){
        if(text is null||characters is null){
            return null;
        }
        return text.TrimEnd(characters.ToCharArray());
    }
    /// <summary>TRIM。両端の空白(または characters の文字)を除く。</summary>
    [SqlFunction("TRIM")]
    public static string? Trim(string? text,string? characters=" "){
        if(text is null||characters is null){
            return null;
        }
        return text.Trim(characters.ToCharArray());
    }
    /// <summary>SPACE。負なら NULL。</summary>
    [SqlFunction("SPACE")]
    public static string? Space(int? count){
        if(count is not{} value||value<0){
            return null;
        }
        return new string(' ',value);
    }
    // ---- 大文字小文字 ----
    /// <summary>UPPER。</summary>
    [SqlFunction("UPPER")]
    public static string? Upper(string? text,SqlCollation collation){
        if(text is null){
            return null;
        }
        return collation.ToUpper(text);
    }
    /// <summary>LOWER。</summary>
    [SqlFunction("LOWER")]
    public static string? Lower(string? text,SqlCollation collation){
        if(text is null){
            return null;
        }
        return collation.ToLower(text);
    }
    // ---- 検索と置換 ----
    /// <summary>CHARINDEX。見つかった位置(1始まり)、無ければ 0。開始位置が1未満なら1から探す。</summary>
    [SqlFunction("CHARINDEX")]
    public static int? CharIndex(string? expressionToFind,string? expressionToSearch,SqlCollation collation,long? startLocation=1){
        if(expressionToFind is null||expressionToSearch is null||startLocation is not{} start){
            return null;
        }
        if(expressionToFind.Length==0){
            return 0;
        }
        if(start<1){
            start=1;
        }
        if(start>expressionToSearch.Length){
            return 0;
        }
        return collation.IndexOf(expressionToSearch,expressionToFind,(int)start-1,out _)+1;
    }
    /// <summary>PATINDEX。LIKE の型が最初に合う位置(1始まり)、無ければ 0。</summary>
    [SqlFunction("PATINDEX")]
    public static int? PatIndex(string? pattern,string? expression,SqlCollation collation){
        if(pattern is null||expression is null){
            return null;
        }
        return SqlLike.FirstMatch(expression,pattern,collation);
    }
    /// <summary>REPLACE。照合順序が大文字小文字を区別しなければ、区別せずに置き換える。探す文字列が空なら元のまま。</summary>
    [SqlFunction("REPLACE")]
    public static string? Replace(string? text,string? pattern,string? replacement,SqlCollation collation){
        if(text is null||pattern is null||replacement is null){
            return null;
        }
        if(pattern.Length==0){
            return text;
        }
        var builder=new StringBuilder(text.Length);
        var position=0;
        while(position<text.Length){
            var found=collation.IndexOf(text,pattern,position,out var length);
            if(found<0||length==0){
                break;
            }
            builder.Append(text,position,found-position).Append(replacement);
            position=found+length;
        }
        builder.Append(text,position,text.Length-position);
        return builder.ToString();
    }
    /// <summary>STUFF。開始位置が1未満か長さを超える、または長さが負なら NULL。</summary>
    [SqlFunction("STUFF")]
    public static string? Stuff(string? text,long? start,long? length,string? replacement){
        if(text is null||start is not{} from||length is not{} count){
            return null;
        }
        if(from<1||from>text.Length||count<0){
            return null;
        }
        var removed=(int)Math.Min(count,text.Length-from+1);
        return text[..(int)(from-1)]+(replacement??"")+text[(int)(from-1+removed)..];
    }
    /// <summary>TRANSLATE。characters の各文字を translations の同じ位置の文字へ置き換える。長さが違えば 9828。</summary>
    [SqlFunction("TRANSLATE")]
    public static string? Translate(string? text,string? characters,string? translations){
        if(text is null||characters is null||translations is null){
            return null;
        }
        if(characters.Length!=translations.Length){
            throw new SqlFunctionException("The second and third arguments of the TRANSLATE built-in function must contain an equal number of characters.",9828);
        }
        var builder=new StringBuilder(text.Length);
        foreach(var c in text){
            var index=characters.IndexOf(c);
            if(index<0){
                builder.Append(c);
            }else{
                builder.Append(translations[index]);
            }
        }
        return builder.ToString();
    }
    // ---- 組み立て ----
    /// <summary>REPLICATE。回数が負なら NULL。</summary>
    [SqlFunction("REPLICATE")]
    public static string? Replicate(string? text,long? count){
        if(text is null||count is not{} times||times<0){
            return null;
        }
        return string.Concat(Enumerable.Repeat(text,checked((int)times)));
    }
    /// <summary>REVERSE。</summary>
    [SqlFunction("REVERSE")]
    public static string? Reverse(string? text){
        if(text is null){
            return null;
        }
        var characters=text.ToCharArray();
        Array.Reverse(characters);
        return new string(characters);
    }
    /// <summary>CONCAT。NULL は空文字として扱う。引数は翻訳器が文字列へ変換して渡す。</summary>
    [SqlFunction("CONCAT")]
    public static string Concat(params string?[] values)=>string.Concat(values);
    /// <summary>CONCAT_WS。NULL の値は区切りごと飛ばす。区切りが NULL なら空文字。</summary>
    [SqlFunction("CONCAT_WS")]
    public static string ConcatWs(string? separator,params string?[] values)=>string.Join(separator??"",values.Where(static value=>value is not null));
    /// <summary>QUOTENAME。128文字を超える、または囲み記号が使えない文字なら NULL。閉じ記号は2つに重ねる。</summary>
    [SqlFunction("QUOTENAME")]
    public static string? QuoteName(string? text,string? quote="["){
        if(text is null||quote is null||text.Length>128){
            return null;
        }
        if(quote.Length==0){
            quote="[";
        }
        var (open,close)=quote[0] switch{
            '['=>('[',']'),
            ']'=>('[',']'),
            '('=>('(',')'),
            ')'=>('(',')'),
            '<'=>('<','>'),
            '>'=>('<','>'),
            '{'=>('{','}'),
            '}'=>('{','}'),
            '\''=>('\'','\''),
            '"'=>('"','"'),
            '`'=>('`','`'),
            _=>('\0','\0')
        };
        if(open=='\0'){
            return null;
        }
        return open+text.Replace(close.ToString(),new string(close,2),StringComparison.Ordinal)+close;
    }
    /// <summary>
    /// STR。float を右寄せの文字列にする。長さの既定は10、小数の桁の既定は0、小数の桁は16まで。
    /// 整数部が入らなければ長さ分の * を返す。小数部は入る桁まで減らす。
    /// </summary>
    [SqlFunction("STR")]
    public static string? Str(double? value,int? length=10,int? decimals=0){
        if(value is not{} number||length is not{} width||decimals is not{} places){
            return null;
        }
        if(width<=0||width>8000||places<0){
            return null;
        }
        if(places>16){
            places=16;
        }
        var integral=Math.Round(number,0,MidpointRounding.AwayFromZero).ToString("0",CultureInfo.InvariantCulture);
        if(integral.Length>width){
            return new string('*',width);
        }
        var available=width-integral.Length-1;
        if(available<places){
            places=Math.Max(0,available);
        }
        var text=Math.Round((decimal)number,places,MidpointRounding.AwayFromZero).ToString("F"+places,CultureInfo.InvariantCulture);
        if(text.Length>width){
            return new string('*',width);
        }
        return text.PadLeft(width);
    }
    /// <summary>
    /// FORMAT。SQL Server も .NET の書式を使うので、同じ書式文字列と言語で ToString する。
    /// 書式が正しくなければ NULL、言語名が正しくなければ 9818。
    /// </summary>
    [SqlFunction("FORMAT")]
    public static string? Format(SqlSession session,IFormattable? value,string? format,string? culture=null){
        if(value is null||format is null){
            return null;
        }
        CultureInfo info;
        try{
            if(culture is null){
                info=LanguageCulture(session);
            }else{
                info=CultureInfo.GetCultureInfo(culture);
            }
        }catch(CultureNotFoundException){
            throw new SqlFunctionException($"The culture parameter '{culture}' provided in the function call is not supported.",9818);
        }
        try{
            return value.ToString(format,info);
        }catch(FormatException){
            return null;
        }
    }
    // ---- 発音 ----
    /// <summary>SOUNDEX。英語の発音の近さを表す4文字(先頭の英字と3桁の数字)。先頭の文字が英字でなければ(空白でも) 0000。</summary>
    [SqlFunction("SOUNDEX")]
    public static string? Soundex(string? text){
        if(text is null){
            return null;
        }
        var start=0;
        if(text.Length==0||!IsLetter(text[0])){
            return "0000";
        }
        var builder=new StringBuilder(4);
        builder.Append(char.ToUpperInvariant(text[start]));
        var previous=SoundexCode(text[start]);
        for(var i=start+1;i<text.Length&&builder.Length<4;i++){
            var c=char.ToUpperInvariant(text[i]);
            if(!IsLetter(c)){
                break;
            }
            // SQL Server では H と W も母音と同じく、前の子音の番号を切る(Ashcraft は A226)。
            var code=SoundexCode(c);
            if(code!='0'&&code!=previous){
                builder.Append(code);
            }
            previous=code;
        }
        return builder.ToString().PadRight(4,'0');
    }
    /// <summary>DIFFERENCE。2つの SOUNDEX の一致の度合い(0〜4)。</summary>
    [SqlFunction("DIFFERENCE")]
    public static int? Difference(string? left,string? right){
        if(Soundex(left) is not{} first||Soundex(right) is not{} second){
            return null;
        }
        if(first.Length==0||second.Length==0){
            return 0;
        }
        var same=0;
        for(var i=0;i<4;i++){
            if(first[i]==second[i]){
                same++;
            }
        }
        return same;
    }
    private static bool IsLetter(char c)=>c is >='A' and <='Z' or >='a' and <='z';
    private static char SoundexCode(char c)=>char.ToUpperInvariant(c) switch{
        'B' or 'F' or 'P' or 'V'=>'1',
        'C' or 'G' or 'J' or 'K' or 'Q' or 'S' or 'X' or 'Z'=>'2',
        'D' or 'T'=>'3',
        'L'=>'4',
        'M' or 'N'=>'5',
        'R'=>'6',
        _=>'0'
    };
    internal static CultureInfo LanguageCulture(SqlSession session){
        if(session.Language.Equals("us_english",StringComparison.OrdinalIgnoreCase)||session.Language.Equals("English",StringComparison.OrdinalIgnoreCase)){
            return CultureInfo.GetCultureInfo("en-US");
        }
        if(session.Language.Equals("Japanese",StringComparison.OrdinalIgnoreCase)||session.Language=="日本語"){
            return CultureInfo.GetCultureInfo("ja-JP");
        }
        throw new NotSupportedException($"言語 {session.Language} に対応していない。");
    }
}
