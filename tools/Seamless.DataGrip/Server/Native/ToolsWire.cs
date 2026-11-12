using System.Globalization;
using System.Text;

namespace DataGrip.Stage1.Server;

/// <summary>Java と .NET が共有する little-endian の型付き値。CLR 型名を通信契約にしない。</summary>
internal static class ToolsWire{
    internal const int MaximumFrame=8*1024*1024;
    internal static long MaximumValueBytes(object? value)=>value switch{null or DBNull=>1,byte[] bytes=>5L+bytes.Length,string text=>5L+Encoding.UTF8.GetByteCount(text),_=>128};
    internal static void Text(BinaryWriter writer,string? text){if(text is null){writer.Write(-1);return;}var bytes=Encoding.UTF8.GetBytes(text);writer.Write(bytes.Length);writer.Write(bytes);}
    internal static string? Text(BinaryReader reader){var length=reader.ReadInt32();if(length==-1){return null;}if(length<0||length>MaximumFrame||length>reader.BaseStream.Length-reader.BaseStream.Position){throw new InvalidDataException("Invalid native text length.");}return new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));}
    internal static void Value(BinaryWriter writer,object? value){
        switch(value){
            case null:case DBNull:writer.Write((byte)0);break;
            case bool flag:writer.Write((byte)1);writer.Write(flag);break;
            case byte or short or int:writer.Write((byte)2);writer.Write(Convert.ToInt32(value,CultureInfo.InvariantCulture));break;
            case long number:writer.Write((byte)3);writer.Write(number);break;
            case float number:writer.Write((byte)4);writer.Write(number);break;
            case double number:writer.Write((byte)5);writer.Write(number);break;
            case decimal number:writer.Write((byte)6);Text(writer,number.ToString(CultureInfo.InvariantCulture));break;
            case byte[] bytes:writer.Write((byte)8);writer.Write(bytes.Length);writer.Write(bytes);break;
            case DateOnly date:writer.Write((byte)9);Text(writer,date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture));break;
            case TimeOnly time:writer.Write((byte)10);Text(writer,time.ToString("HH:mm:ss.fffffff",CultureInfo.InvariantCulture));break;
            case TimeSpan time:writer.Write((byte)10);Text(writer,TimeOnly.FromTimeSpan(time).ToString("HH:mm:ss.fffffff",CultureInfo.InvariantCulture));break;
            case DateTime date:writer.Write((byte)11);Text(writer,date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff",CultureInfo.InvariantCulture));break;
            case DateTimeOffset date:writer.Write((byte)12);Text(writer,date.ToString("O",CultureInfo.InvariantCulture));break;
            case Guid guid:writer.Write((byte)13);Text(writer,guid.ToString("D"));break;
            case string text:writer.Write((byte)7);Text(writer,text);break;
            default:throw new NotSupportedException("No native tools value codec for "+value.GetType().FullName);
        }
    }
    internal static object? Value(BinaryReader reader)=>reader.ReadByte() switch{
        0=>null,1=>reader.ReadBoolean(),2=>reader.ReadInt32(),3=>reader.ReadInt64(),4=>reader.ReadSingle(),5=>reader.ReadDouble(),6=>decimal.Parse(Text(reader)!,CultureInfo.InvariantCulture),7=>Text(reader),8=>Bytes(reader),9=>DateOnly.ParseExact(Text(reader)!,"yyyy-MM-dd",CultureInfo.InvariantCulture),10=>TimeOnly.Parse(Text(reader)!,CultureInfo.InvariantCulture),11=>DateTime.Parse(Text(reader)!,CultureInfo.InvariantCulture),12=>DateTimeOffset.Parse(Text(reader)!,CultureInfo.InvariantCulture),13=>Guid.Parse(Text(reader)!),_=>throw new InvalidDataException("Unknown native tools value tag.")};
    private static byte[] Bytes(BinaryReader reader){var length=reader.ReadInt32();if(length<0||length>MaximumFrame||length>reader.BaseStream.Length-reader.BaseStream.Position){throw new InvalidDataException("Invalid native binary length.");}return reader.ReadBytes(length);}
}
