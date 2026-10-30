using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsPacketWriter{
    public static void ValidateMetadata(IReadOnlyList<ColumnMetadata> columns)=>WriteMetadata(Stream.Null,columns);
    public static void ValidateCollation(string? collation)=>_ = ResolveCollation(collation);

    private static void WriteMetadata(Stream stream,IReadOnlyList<ColumnMetadata> columns){
        stream.WriteByte(TdsConstants.TokenColMetaData);WriteUInt16LE(stream,checked((ushort)columns.Count));
        foreach(var column in columns){
            var sql=column.SqlTypeName.ToLowerInvariant();WriteUInt32LE(stream,sql is "timestamp" or "rowversion"?80u:0u);
            ushort flags=0;if(column.IsNullable){flags=1;}if(sql=="xml"||column.ClrType==typeof(string)&&column.Collation is not null&&(ResolveCollation(column.Collation).Info&0x00100000)==0){flags|=2;}WriteUInt16LE(stream,flags);
            var type=WireType(column);stream.WriteByte(type);
            if(type is 0x26 or 0x68 or 0x6D or 0x24 or 0x6F or 0x6E){stream.WriteByte(WireLength(column));}
            else if(type==0x6C){stream.WriteByte(17);stream.WriteByte(column.Precision??38);stream.WriteByte(column.Scale??6);}
            else if(type is 0x29 or 0x2A or 0x2B){stream.WriteByte(column.Scale??7);}
            else if(type is 0xA7 or 0xAF or 0xE7 or 0xEF or 0xA5 or 0xAD){
                var maximum=MaximumWireLength(column,type);WriteUInt16LE(stream,maximum);
                if(type is 0xA7 or 0xAF or 0xE7 or 0xEF){var collation=ResolveCollation(column.Collation);WriteUInt32LE(stream,collation.Info);stream.WriteByte(collation.SortId);}
            }else if(type is 0x22 or 0x23 or 0x63){
                var maximum=int.MaxValue;if(type==0x63){maximum=int.MaxValue-1;}WriteInt32LE(stream,maximum);
                if(type is 0x23 or 0x63){var collation=ResolveCollation(column.Collation);WriteUInt32LE(stream,collation.Info);stream.WriteByte(collation.SortId);}
                WriteLegacyTableName(stream,column);
            }else if(type==0xF0){WriteUdtMetadata(stream,column);}else if(type==0xF1){stream.WriteByte(0);}
            if(column.Name.Length>255){throw new InvalidOperationException("TDS column names must not exceed 255 characters.");}
            WriteStringWithLength8(stream,column.Name);
        }
    }
    private static (uint Info,byte SortId,int CodePage) ResolveCollation(string? name){
        if(name is null||name.Equals("SQL_Latin1_General_CP1_CI_AS",StringComparison.OrdinalIgnoreCase)){return(0x00D00409,52,1252);}
        if(name.Equals("Japanese_CI_AS",StringComparison.OrdinalIgnoreCase)){return(0x00D00411,0,932);}
        if(name.Equals("Latin1_General_100_CI_AS",StringComparison.OrdinalIgnoreCase)){return(0x20D00409,0,1252);}
        if(name.Equals("Latin1_General_BIN2",StringComparison.OrdinalIgnoreCase)){return(0x02000409,0,1252);}
        if(name.StartsWith("tds:",StringComparison.Ordinal)&&name.Length==15&&uint.TryParse(name.AsSpan(4,8),global::System.Globalization.NumberStyles.HexNumber,global::System.Globalization.CultureInfo.InvariantCulture,out var info)&&name[12]==':'&&byte.TryParse(name.AsSpan(13,2),global::System.Globalization.NumberStyles.HexNumber,global::System.Globalization.CultureInfo.InvariantCulture,out var sort)){return(info,sort,IncomingCodePage(info,sort));}
        throw new NotSupportedException($"TDS collation '{name}' is not supported.");
    }
    internal static (string Name,int CodePage) ResolveIncomingCollation(ReadOnlySpan<byte> bytes){
        if(bytes.Length!=5){throw new InvalidDataException("Invalid RPC collation length.");}
        var info=BinaryPrimitives.ReadUInt32LittleEndian(bytes);var sort=bytes[4];var codePage=IncomingCodePage(info,sort);
        string name;if(info==0x00D00409&&sort==52){name="SQL_Latin1_General_CP1_CI_AS";}else if(info==0x00D00411&&sort==0){name="Japanese_CI_AS";}else if(info==0x20D00409&&sort==0){name="Latin1_General_100_CI_AS";}else if(info==0x02000409&&sort==0){name="Latin1_General_BIN2";}else{name=$"tds:{info:X8}:{sort:X2}";}
        return(name,codePage);
    }
    private static int IncomingCodePage(uint info,byte sort){
        var lcid=info&0x000FFFFF;var version=info>>28;if((info&0x08000000)!=0||version>3||(info&0x03000000)==0x03000000||sort!=0&&(info&0x04000000)!=0){throw new InvalidDataException("Invalid RPC collation flags.");}
        if(sort==52&&lcid==1033){return 1252;}if(sort!=0){throw new NotSupportedException("The RPC collation sort ID is not supported.");}
        if((info&0x04000000)!=0){if(lcid is 1033 or 1041){return 65001;}throw new NotSupportedException("The RPC UTF-8 collation is not supported.");}
        if(lcid==1041){return 932;}if(lcid==1033){return 1252;}throw new NotSupportedException("The RPC collation locale is not supported.");
    }
    private static void WriteUdtMetadata(Stream stream,ColumnMetadata column){
        var name=column.SqlTypeName.ToLowerInvariant();var type=name switch{"hierarchyid"=>"SqlHierarchyId","geometry"=>"SqlGeometry","geography"=>"SqlGeography",_=>throw new NotSupportedException("Unsupported UDT.")};
        WriteUInt16LE(stream,name=="hierarchyid"?(ushort)892:ushort.MaxValue);
        WriteStringWithLength8(stream,column.SourceDatabase??throw new InvalidOperationException("UDT source database is unavailable."));WriteStringWithLength8(stream,"sys");WriteStringWithLength8(stream,name);
        WriteStringWithLength(stream,"Microsoft.SqlServer.Types."+type+", Microsoft.SqlServer.Types, Version=11.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91");
    }
    private static void WriteLegacyTableName(Stream stream,ColumnMetadata column){
        if(column.SourceDatabase is null||column.SourceSchema is null||column.SourceTable is null){throw new InvalidOperationException("The source table for a legacy LOB column is unavailable.");}
        stream.WriteByte(3);WriteStringWithLength(stream,column.SourceDatabase);WriteStringWithLength(stream,column.SourceSchema);WriteStringWithLength(stream,column.SourceTable);
    }
    private static byte WireType(ColumnMetadata column){
        var type=Nullable.GetUnderlyingType(column.ClrType)??column.ClrType;
        var sql=column.SqlTypeName.ToLowerInvariant();
        var compatible=sql switch{
            "bit"=>type==typeof(bool),"tinyint"=>type==typeof(byte),"smallint"=>type==typeof(short),"int"=>type==typeof(int),"bigint"=>type==typeof(long),
            "decimal" or "numeric" or "money" or "smallmoney"=>type==typeof(decimal),"real"=>type==typeof(float),"float"=>type==typeof(double),
            "char" or "varchar" or "text" or "nchar" or "nvarchar" or "ntext" or "sysname" or "xml"=>type==typeof(string),
            "binary" or "varbinary" or "image" or "timestamp" or "rowversion" or "hierarchyid" or "geometry" or "geography"=>type==typeof(byte[]),
            "uniqueidentifier"=>type==typeof(Guid),"date"=>type==typeof(DateOnly),"time"=>type==typeof(TimeOnly)||type==typeof(TimeSpan),
            "datetime" or "smalldatetime" or "datetime2"=>type==typeof(DateTime),"datetimeoffset"=>type==typeof(DateTimeOffset),_=>false};
        if(!compatible){throw new NotSupportedException($"TDS type '{column.SqlTypeName}'/'{type}' is not supported.");}
        if(sql=="xml"){return 0xF1;}
        if(sql=="text"){return 0x23;}if(sql=="ntext"){return 0x63;}if(sql=="image"){return 0x22;}
        if(sql is "hierarchyid" or "geometry" or "geography"){return 0xF0;}
        if(sql is "money" or "smallmoney"){
            if(column.IsNullable){return 0x6E;}
            if(sql=="smallmoney"){return 0x7A;}return 0x3C;
        }
        if(sql is "datetime" or "smalldatetime"){
            if(column.IsNullable){return 0x6F;}
            if(sql=="smalldatetime"){return 0x3A;}return 0x3D;
        }
        if(type==typeof(decimal)){return 0x6C;}
        if(type==typeof(Guid)){return 0x24;}
        if(type==typeof(DateOnly)){return 0x28;}
        if(type==typeof(TimeOnly)||type==typeof(TimeSpan)){return 0x29;}
        if(type==typeof(DateTimeOffset)){return 0x2B;}
        if(type==typeof(DateTime)){return 0x2A;}
        if(type==typeof(byte[])){if(sql is "binary" or "timestamp" or "rowversion"){return 0xAD;}return 0xA5;}
        if(type==typeof(string)){
            if(sql is "varchar" or "text"){return 0xA7;}if(sql=="char"){return 0xAF;}if(sql=="nchar"){return 0xEF;}return 0xE7;
        }
        if(type==typeof(bool)){if(column.IsNullable){return 0x68;}return 0x32;}
        if(type==typeof(float)){if(column.IsNullable){return 0x6D;}return 0x3B;}
        if(type==typeof(double)){if(column.IsNullable){return 0x6D;}return 0x3E;}
        if(type==typeof(int)){if(column.IsNullable){return 0x26;}return 0x38;}
        if(type==typeof(long)){if(column.IsNullable){return 0x26;}return 0x7F;}
        if(type==typeof(short)){if(column.IsNullable){return 0x26;}return 0x34;}
        if(type==typeof(byte)){if(column.IsNullable){return 0x26;}return 0x30;}
        throw new NotSupportedException($"TDS type '{column.SqlTypeName}'/'{type}' is not supported.");
    }
    private static byte WireLength(ColumnMetadata column){
        var type=Nullable.GetUnderlyingType(column.ClrType)??column.ClrType;
        if(type==typeof(Guid)){return 16;}
        if(column.SqlTypeName.Equals("smalldatetime",StringComparison.OrdinalIgnoreCase)||column.SqlTypeName.Equals("smallmoney",StringComparison.OrdinalIgnoreCase)){return 4;}
        if(type==typeof(long)||type==typeof(double)||type==typeof(DateTime)||type==typeof(decimal)){return 8;}
        if(type==typeof(short)){return 2;}if(type==typeof(byte)||type==typeof(bool)){return 1;}return 4;
    }
    private static ushort MaximumWireLength(ColumnMetadata column,byte type){
        if(column.MaximumLength is null||column.MaximumLength<0){return ushort.MaxValue;}
        var length=column.MaximumLength.Value;if(type is 0xE7 or 0xEF){length=checked(length*2);}
        if(length>8000){return ushort.MaxValue;}return checked((ushort)length);
    }
    private static void WriteValue(Stream stream,ColumnMetadata column,object? value,List<(int Start,int Length)> chunks){
        var type=WireType(column);
        if(value is null||value is DBNull){
            if(!column.IsNullable){throw new InvalidOperationException($"NULL supplied for non-nullable column '{column.Name}'.");}
            if(type is 0x22 or 0x23 or 0x63){stream.WriteByte(0);return;}
            if(type is 0xF0 or 0xF1||(type is 0xA7 or 0xAF or 0xE7 or 0xEF or 0xA5 or 0xAD)&&MaximumWireLength(column,type)==ushort.MaxValue){WriteInt64LE(stream,-1);}
            else if(type is 0xA7 or 0xAF or 0xE7 or 0xEF or 0xA5 or 0xAD){WriteUInt16LE(stream,ushort.MaxValue);}
            else{stream.WriteByte(0);}return;
        }
        if(type is 0x22 or 0x23 or 0x63){
            byte[] bytes;if(value is byte[] binary){bytes=binary;}else if(type==0x23){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);bytes=Encoding.GetEncoding(ResolveCollation(column.Collation).CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetBytes((string)value);}else{bytes=Encoding.Unicode.GetBytes((string)value);}
            // 旧LOBのポインタは読取専用のため不透明な固定値を送る。
            stream.WriteByte(16);stream.Write(new byte[16]);stream.Write(new byte[8]);WriteInt32LE(stream,bytes.Length);stream.Write(bytes);return;
        }
        if(type is 0xA7 or 0xAF or 0xE7 or 0xEF or 0xA5 or 0xAD or 0xF0 or 0xF1){
            byte[] bytes;
            if(value is byte[] binary){bytes=binary;}
            else if(type is 0xA7 or 0xAF){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);bytes=Encoding.GetEncoding(ResolveCollation(column.Collation).CodePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetBytes((string)value);}
            else{bytes=Encoding.Unicode.GetBytes((string)value);}
            if(type is 0xF0 or 0xF1||MaximumWireLength(column,type)==ushort.MaxValue){
                WriteInt64LE(stream,bytes.Length);
                if(type is 0xE7 or 0xEF){
                    for(var offset=0;offset<bytes.Length;){var size=Math.Min(PacketSize-12,bytes.Length-offset);var start=checked((int)stream.Position);WriteInt32LE(stream,size);stream.Write(bytes,offset,size);chunks.Add((start,size+4));offset+=size;}
                }else if(bytes.Length>0){WriteInt32LE(stream,bytes.Length);stream.Write(bytes);}
                WriteInt32LE(stream,0);
            }
            else{if(bytes.Length>MaximumWireLength(column,type)){throw new InvalidOperationException("A value exceeds the declared SQL length.");}WriteUInt16LE(stream,checked((ushort)bytes.Length));stream.Write(bytes);}return;
        }
        if(type==0x6C){
            var number=Convert.ToDecimal(value,global::System.Globalization.CultureInfo.InvariantCulture);var bits=decimal.GetBits(number);
            var magnitude=(BigInteger)(uint)bits[0]+((BigInteger)(uint)bits[1]<<32)+((BigInteger)(uint)bits[2]<<64);
            var originalScale=(bits[3]>>16)&0x7F;var scale=column.Scale??6;
            if(originalScale<scale){magnitude*=BigInteger.Pow(10,scale-originalScale);}else if(originalScale>scale){var divisor=BigInteger.Pow(10,originalScale-scale);if(magnitude%divisor!=0){throw new InvalidOperationException("Decimal scale conversion would lose precision.");}magnitude/=divisor;}
            if(magnitude>=BigInteger.Pow(10,column.Precision??38)){throw new OverflowException("Decimal exceeds SQL precision.");}
            stream.WriteByte(17);byte sign=1;if(number<0){sign=0;}stream.WriteByte(sign);
            Span<byte> encoded=stackalloc byte[16];encoded.Clear();if(!magnitude.TryWriteBytes(encoded,out _,true,false)){throw new OverflowException("Decimal payload exceeds 128 bits.");}stream.Write(encoded);return;
        }
        if(type==0x24){stream.WriteByte(16);stream.Write(((Guid)value).ToByteArray());return;}
        if(type is 0x6E or 0x3C or 0x7A){
            var scaled=checked((long)(Convert.ToDecimal(value)*10000m));var size=WireLength(column);if(type==0x6E){stream.WriteByte(size);}if(size==4){WriteInt32LE(stream,checked((int)scaled));}else{WriteInt32LE(stream,(int)(scaled>>32));WriteUInt32LE(stream,(uint)scaled);}return;
        }
        if(type is 0x6F or 0x3D or 0x3A){
            var date=(DateTime)value;var size=WireLength(column);if(type==0x6F){stream.WriteByte(size);}var days=(int)(date.Date-new DateTime(1900,1,1)).TotalDays;
            if(size==4){WriteUInt16LE(stream,checked((ushort)days));WriteUInt16LE(stream,checked((ushort)date.TimeOfDay.TotalMinutes));}
            else{WriteInt32LE(stream,days);WriteInt32LE(stream,checked((int)Math.Round(date.TimeOfDay.TotalSeconds*300)));}return;
        }
        if(type is 0x28 or 0x29 or 0x2A or 0x2B){
            DateTime date=default;TimeSpan time=default;short offset=0;
            if(value is DateTimeOffset dto){date=dto.UtcDateTime;time=date.TimeOfDay;offset=checked((short)dto.Offset.TotalMinutes);}
            else if(value is DateTime dt){date=dt;time=dt.TimeOfDay;}
            else if(value is DateOnly day){date=day.ToDateTime(TimeOnly.MinValue);}
            else if(value is TimeOnly clock){time=clock.ToTimeSpan();}
            else if(value is TimeSpan duration){time=duration;}
            var scale=column.Scale??7;var timeLength=5;if(scale<=2){timeLength=3;}else if(scale<=4){timeLength=4;}
            var length=0;if(type!=0x28){length+=timeLength;}if(type!=0x29){length+=3;}if(type==0x2B){length+=2;}stream.WriteByte((byte)length);
            if(type!=0x28){var ticks=time.Ticks/(long)Math.Pow(10,7-scale);for(var i=0;i<timeLength;i++){stream.WriteByte((byte)(ticks>>(8*i)));}}
            if(type!=0x29){var days=(date.Date-DateTime.MinValue).Days;for(var i=0;i<3;i++){stream.WriteByte((byte)(days>>(8*i)));}}
            if(type==0x2B){WriteInt16LE(stream,offset);}return;
        }
        var clr=Nullable.GetUnderlyingType(column.ClrType)??column.ClrType;
        if(type is 0x26 or 0x68 or 0x6D){stream.WriteByte(WireLength(column));}
        if(clr==typeof(int)){WriteInt32LE(stream,(int)value);}else if(clr==typeof(long)){WriteInt64LE(stream,(long)value);}else if(clr==typeof(short)){WriteInt16LE(stream,(short)value);}else if(clr==typeof(byte)){stream.WriteByte((byte)value);}
        else if(clr==typeof(bool)){byte bit=0;if((bool)value){bit=1;}stream.WriteByte(bit);}
        else if(clr==typeof(float)){Span<byte> bytes=stackalloc byte[4];BinaryPrimitives.WriteSingleLittleEndian(bytes,(float)value);stream.Write(bytes);}
        else if(clr==typeof(double)){Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteDoubleLittleEndian(bytes,(double)value);stream.Write(bytes);}
        else{throw new NotSupportedException($"Value type '{clr}' is not supported.");}
    }
}
