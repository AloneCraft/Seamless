using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 照合順序。文字列の比較・検索・大文字小文字の変換と、varchar の文字コードを決める。
/// 名前の末尾(_CI/_CS、_AI/_AS、_KS、_WS、_BIN/_BIN2、_UTF8)から比較の仕方を作る。
/// 扱える言語は Latin1_General と SQL_Latin1_General_CP1(コードページ1252)、Japanese(932)だけで、ほかは 448 のエラーにする。
/// </summary>
/// <remarks>
/// SQL_ で始まる照合順序の varchar は、本物では SQL 独自の並び順を使う。ここでは Windows の照合順序と同じ比較で代用している。
/// 記号(ハイフンなど)を含む文字列の大小が本物と違うことがある(等しいかどうかは変わらない)。
/// </remarks>
public sealed class SqlCollation{
    private static readonly ConcurrentDictionary<string,SqlCollation> cache=new(StringComparer.OrdinalIgnoreCase);
    private readonly CompareInfo compareInfo;
    private readonly CultureInfo culture;
    private SqlCollation(string name){
        this.Name=name;
        var upper=name.ToUpperInvariant();
        string cultureName;
        if(upper.StartsWith("JAPANESE",StringComparison.Ordinal)){
            cultureName="ja-JP";
            this.CodePage=932;
        }else if(upper.StartsWith("LATIN1_GENERAL",StringComparison.Ordinal)||upper.StartsWith("SQL_LATIN1_GENERAL_CP1_",StringComparison.Ordinal)){
            cultureName="en-US";
            this.CodePage=1252;
        }else{
            throw new SqlFunctionException($"Invalid collation '{name}'.",448);
        }
        if(upper.Contains("_UTF8",StringComparison.Ordinal)){
            this.CodePage=65001;
        }
        this.culture=CultureInfo.GetCultureInfo(cultureName);
        this.compareInfo=this.culture.CompareInfo;
        this.IsBinary=upper.Contains("_BIN",StringComparison.Ordinal);
        var options=CompareOptions.None;
        if(upper.Contains("_CI",StringComparison.Ordinal)){
            options|=CompareOptions.IgnoreCase;
        }
        if(upper.Contains("_AI",StringComparison.Ordinal)){
            options|=CompareOptions.IgnoreNonSpace;
        }
        if(!upper.Contains("_KS",StringComparison.Ordinal)){
            options|=CompareOptions.IgnoreKanaType;
        }
        if(!upper.Contains("_WS",StringComparison.Ordinal)){
            options|=CompareOptions.IgnoreWidth;
        }
        this.Options=options;
    }
    /// <summary>照合順序の名前。</summary>
    public string Name{get;}
    /// <summary>varchar・char・text の文字コード。</summary>
    public int CodePage{get;}
    /// <summary>_BIN・_BIN2 のとき true。比較は UTF-16 の符号単位の順になる。</summary>
    public bool IsBinary{get;}
    /// <summary>文字の比較の仕方。<see cref="IsBinary"/> のときは使わない。</summary>
    public CompareOptions Options{get;}
    /// <summary>名前から照合順序を得る。同じ名前には同じものを返す。</summary>
    public static SqlCollation Get(string name)=>cache.GetOrAdd(name,static key=>new SqlCollation(key));
    /// <summary>SQL Server を既定の設定で入れたときの照合順序。</summary>
    public static SqlCollation Default{get;}=Get("SQL_Latin1_General_CP1_CI_AS");
    /// <summary>varchar の文字コード。</summary>
    public Encoding Encoding{
        get{
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(this.CodePage);
        }
    }
    /// <summary>varchar の文字コードで、表せない文字や1文字にならないバイト列を例外にするもの。</summary>
    public Encoding StrictEncoding{
        get{
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(this.CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);
        }
    }
    /// <summary>2つの文字列を比べる。末尾の空白は無視しない(比較演算子での扱いは呼び出し側が決める)。</summary>
    public int Compare(string left,string right){
        if(this.IsBinary){
            return string.CompareOrdinal(left,right);
        }
        return this.compareInfo.Compare(left,right,this.Options);
    }
    /// <summary>text の startIndex 以降で value が最初に現れる位置と、一致した長さ。無ければ -1。</summary>
    public int IndexOf(string text,string value,int startIndex,out int matchLength){
        if(this.IsBinary){
            var index=text.IndexOf(value,startIndex,StringComparison.Ordinal);
            matchLength=value.Length;
            return index;
        }
        var position=this.compareInfo.IndexOf(text.AsSpan(startIndex),value.AsSpan(),this.Options,out matchLength);
        if(position<0){
            return -1;
        }
        return position+startIndex;
    }
    /// <summary>大文字へ変換する。言語はこの照合順序の言語。</summary>
    public string ToUpper(string text)=>this.culture.TextInfo.ToUpper(text);
    /// <summary>小文字へ変換する。言語はこの照合順序の言語。</summary>
    public string ToLower(string text)=>this.culture.TextInfo.ToLower(text);
    /// <inheritdoc/>
    public override string ToString()=>this.Name;
}
