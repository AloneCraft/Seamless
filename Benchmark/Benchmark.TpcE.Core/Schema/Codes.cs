using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Benchmark.TpcE.Core;
/// <summary>
/// 文字の ID(ST_ID・TT_ID など 8 文字以下)を ulong に詰めた値。
/// ASCII を上位バイトから順に詰めるので、数値の大小が文字列の Ordinal 比較と一致する。
/// Tsavorite と Persisted はキーをバイト列で比べるので、文字列ではなくこの型をキーに使う(パディングも無い)。
/// </summary>
[JsonConverter(typeof(CodeJsonConverter<Code8>))]
public readonly record struct Code8(ulong Value):IComparable<Code8>,ICode<Code8>{
    public const int Length=8;
    public static Code8 From(string text){
        CodePacking.Validate(text,Length);
        return new(CodePacking.Pack(text,0));
    }
    public int CompareTo(Code8 other)=>this.Value.CompareTo(other.Value);
    public override string ToString()=>CodePacking.Unpack(this.Value);
    static Code8 ICode<Code8>.Parse(string text)=>From(text);
}
/// <summary>16 文字以下の文字の ID(S_SYMB・ZC_CODE など)。2 つの ulong に上位から詰める。</summary>
[JsonConverter(typeof(CodeJsonConverter<Code16>))]
public readonly record struct Code16(ulong High,ulong Low):IComparable<Code16>,ICode<Code16>{
    public const int Length=16;
    public static Code16 From(string text){
        CodePacking.Validate(text,Length);
        return new(CodePacking.Pack(text,0),CodePacking.Pack(text,8));
    }
    public int CompareTo(Code16 other){
        var high=this.High.CompareTo(other.High);
        if(high!=0){
            return high;
        }
        return this.Low.CompareTo(other.Low);
    }
    public override string ToString()=>CodePacking.Unpack(this.High)+CodePacking.Unpack(this.Low);
    static Code16 ICode<Code16>.Parse(string text)=>From(text);
}
/// <summary>24 文字以下の文字の ID(TAX_ID など)。3 つの ulong に上位から詰める。</summary>
[JsonConverter(typeof(CodeJsonConverter<Code24>))]
public readonly record struct Code24(ulong High,ulong Middle,ulong Low):IComparable<Code24>,ICode<Code24>{
    public const int Length=24;
    public static Code24 From(string text){
        CodePacking.Validate(text,Length);
        return new(CodePacking.Pack(text,0),CodePacking.Pack(text,8),CodePacking.Pack(text,16));
    }
    public int CompareTo(Code24 other){
        var high=this.High.CompareTo(other.High);
        if(high!=0){
            return high;
        }
        var middle=this.Middle.CompareTo(other.Middle);
        if(middle!=0){
            return middle;
        }
        return this.Low.CompareTo(other.Low);
    }
    public override string ToString()=>CodePacking.Unpack(this.High)+CodePacking.Unpack(this.Middle)+CodePacking.Unpack(this.Low);
    static Code24 ICode<Code24>.Parse(string text)=>From(text);
}
/// <summary>文字の ID 型に共通の口(JSON 変換用)。</summary>
public interface ICode<TSelf> where TSelf:struct,ICode<TSelf>{
    static abstract TSelf Parse(string text);
}
internal static class CodePacking{
    public static void Validate(string text,int length){
        ArgumentNullException.ThrowIfNull(text);
        if(text.Length>length){
            throw new ArgumentException($"'{text}' is longer than {length} characters.",nameof(text));
        }
        foreach(var character in text){
            if(character==0||character>0x7F){
                throw new ArgumentException($"'{text}' contains a non-ASCII or NUL character.",nameof(text));
            }
        }
    }
    /// <summary>text[offset..offset+8] を上位バイトから詰める。足りない桁は 0。</summary>
    public static ulong Pack(string text,int offset){
        ulong value=0;
        for(var index=0;index<8;index++){
            value<<=8;
            var position=offset+index;
            if(position<text.Length){
                value|=text[position];
            }
        }
        return value;
    }
    public static string Unpack(ulong value){
        var builder=new StringBuilder(8);
        for(var shift=56;shift>=0;shift-=8){
            var character=(char)((value>>shift)&0xFF);
            if(character==0){
                break;
            }
            builder.Append(character);
        }
        return builder.ToString();
    }
}
internal sealed class CodeJsonConverter<TCode>:JsonConverter<TCode> where TCode:struct,ICode<TCode>{
    public override TCode Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options){
        return TCode.Parse(reader.GetString()??"");
    }
    public override void Write(Utf8JsonWriter writer,TCode value,JsonSerializerOptions options){
        writer.WriteStringValue(value.ToString());
    }
}
