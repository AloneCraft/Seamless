using System.Data.SqlTypes;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Tds;

public sealed partial class TdsPacketWriter{
    // MS-TDS 2.2.5.5.4: base type, property bytes, properties, then its unprefixed value.
    private static void WriteVariant(Stream stream,object? value){
        if(value is null or DBNull||value is INullable{IsNull:true}){WriteInt32LE(stream,0);return;}
        if(value is decimal number){value=new SqlDecimal(number);}
        if(value is SqlDecimal numeric){
            WriteInt32LE(stream,21);stream.WriteByte(0x6C);stream.WriteByte(2);stream.WriteByte(numeric.Precision);stream.WriteByte(numeric.Scale);byte sign=0;if(numeric.IsPositive){sign=1;}stream.WriteByte(sign);foreach(var part in numeric.Data){WriteInt32LE(stream,part);}return;
        }
        var sql=value switch{bool=>"bit",byte=>"tinyint",short=>"smallint",int=>"int",long=>"bigint",float=>"real",double=>"float",Guid=>"uniqueidentifier",DateOnly=>"date",TimeOnly or TimeSpan=>"time",DateTime=>"datetime2",DateTimeOffset=>"datetimeoffset",string=>"nvarchar",byte[]=>"varbinary",_=>throw new NotSupportedException("Unsupported sql_variant base value: "+value.GetType())};
        int? length=null;if(value is string text){length=text.Length;if(length>4000){throw new InvalidOperationException("The sql_variant string exceeds 8000 bytes.");}}else if(value is byte[] binary){length=binary.Length;if(length>8000){throw new InvalidOperationException("The sql_variant binary exceeds 8000 bytes.");}}
        var column=new ColumnMetadata("",value.GetType(),sql,false,false,0,length,scale:7);var type=WireType(column);using var payload=new MemoryStream();using var properties=new MemoryStream();WriteValue(payload,column,value,[]);var prefix=0;
        if(type is 0x24 or 0x28 or 0x29 or 0x2A or 0x2B){prefix=1;}
        if(type is 0x29 or 0x2A or 0x2B){properties.WriteByte(7);}
        if(type is 0xA5 or 0xE7){prefix=2;if(type==0xE7){var collation=ResolveCollation(null);WriteUInt32LE(properties,collation.Info);properties.WriteByte(collation.SortId);}WriteUInt16LE(properties,MaximumWireLength(column,type));}
        WriteInt32LE(stream,checked(2+(int)properties.Length+(int)payload.Length-prefix));stream.WriteByte(type);stream.WriteByte(checked((byte)properties.Length));properties.Position=0;properties.CopyTo(stream);payload.Position=prefix;payload.CopyTo(stream);
    }
}
