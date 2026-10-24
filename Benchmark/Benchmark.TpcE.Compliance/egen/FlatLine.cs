using System.Globalization;
using System.Text;
using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance;
/// <summary>EGen のフラットファイルの 1 行(`|` 区切り)。行ごとに作り直さず、バッファを使い回して配列を増やさない。</summary>
internal sealed class FlatLine{
    private string line="";
    private int[] starts=new int[32];
    private int[] ends=new int[32];
    private int count;
    public int Count=>this.count;
    public void Set(string text){
        this.line=text;
        this.count=0;
        var start=0;
        for(var index=0;index<=text.Length;index++){
            if(index==text.Length||text[index]=='|'){
                if(this.count==this.starts.Length){
                    Array.Resize(ref this.starts,this.count*2);
                    Array.Resize(ref this.ends,this.count*2);
                }
                this.starts[this.count]=start;
                this.ends[this.count]=index;
                this.count++;
                start=index+1;
            }
        }
    }
    public ReadOnlySpan<char> Span(int field){
        this.Check(field);
        return this.line.AsSpan(this.starts[field],this.ends[field]-this.starts[field]);
    }
    public string Text(int field)=>this.Span(field).ToString();
    /// <summary>空欄は null(EGen は NULL を空欄で出す)。</summary>
    public string? OptionalText(int field){
        var span=this.Span(field);
        if(span.Length==0){
            return null;
        }
        return span.ToString();
    }
    public long Long(int field)=>long.Parse(this.Span(field),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
    public int Int(int field)=>int.Parse(this.Span(field),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture);
    public decimal Decimal(int field)=>decimal.Parse(this.Span(field),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
    public decimal? OptionalDecimal(int field){
        if(this.Span(field).Length==0){
            return null;
        }
        return this.Decimal(field);
    }
    /// <summary>EGen の真偽値は 1/0。</summary>
    public bool Flag(int field)=>this.Span(field).SequenceEqual("1");
    /// <summary>yyyy-MM-dd を日番号にする(既存の日付列と同じ表し方)。</summary>
    public long Date(int field){
        var span=this.Span(field);
        return TpcETime.DayNumber(Digits(span,0,4),Digits(span,5,2),Digits(span,8,2));
    }
    /// <summary>yyyy-MM-dd HH:mm:ss.fff を、0001-01-01 からのミリ秒にする。</summary>
    public long DateTime(int field){
        var span=this.Span(field);
        var value=new System.DateTime(Digits(span,0,4),Digits(span,5,2),Digits(span,8,2),Digits(span,11,2),Digits(span,14,2),Digits(span,17,2));
        var milliseconds=0;
        if(span.Length>=23){
            milliseconds=Digits(span,20,3);
        }
        return TpcETime.FromDateTime(value)+milliseconds;
    }
    public Code8 Code8(int field)=>Benchmark.Tpc.Core.Code8.From(this.Span(field).ToString());
    public Code16 Code16(int field)=>Benchmark.Tpc.Core.Code16.From(this.Span(field).ToString());
    public Code24 Code24(int field)=>Benchmark.Tpc.Core.Code24.From(this.Span(field).ToString());
    public byte[] Utf8(int field)=>Encoding.UTF8.GetBytes(this.Span(field).ToString());
    private static int Digits(ReadOnlySpan<char> span,int offset,int length){
        var value=0;
        for(var index=offset;index<offset+length;index++){
            value=value*10+(span[index]-'0');
        }
        return value;
    }
    private void Check(int field){
        if(field>=this.count){
            throw new FormatException($"The line has {this.count} fields but field {field} was requested: {this.line}");
        }
    }
}
