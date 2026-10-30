using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tds;

public sealed record TdsRpcParameter(string Name,SqlType Type,object? Value,bool Output);
internal sealed record TdsRpcRequest(string Procedure,IReadOnlyList<TdsRpcParameter> Parameters){
    public static TdsRpcRequest Parse(byte[] payload){
        var reader=new ValueReader(payload,HeadersLength(payload));
        var chars=reader.UInt16();string procedure;
        if(chars==ushort.MaxValue){procedure=reader.UInt16() switch{10=>"sp_executesql",11=>"sp_prepare",12=>"sp_execute",13=>"sp_prepexec",15=>"sp_unprepare",_=>throw new NotSupportedException("The requested system RPC is not supported.")};}
        else{procedure=reader.Unicode(chars);}
        _=reader.UInt16();var parameters=new List<TdsRpcParameter>();
        while(!reader.End){
            var name=reader.Unicode(reader.Byte());var flags=reader.Byte();var value=reader.Value();parameters.Add(new(name,value.Type,value.Value,(flags&1)!=0));
        }
        return new(procedure.ToLowerInvariant(),parameters);
    }
    internal static int HeadersLength(byte[] payload){
        if(payload.Length<4){return 0;}var length=BinaryPrimitives.ReadInt32LittleEndian(payload);
        if(length>=4&&length<=payload.Length&&length<=1024){return length;}return 0;
    }
    private sealed class ValueReader(byte[] bytes,int position){
        private int position=position;
        public bool End=>this.position==bytes.Length;
        private ReadOnlySpan<byte> Read(int count){if(count<0||count>bytes.Length-this.position){throw new InvalidDataException("Truncated RPC value.");}var result=bytes.AsSpan(this.position,count);this.position+=count;return result;}
        public byte Byte()=>this.Read(1)[0];
        public ushort UInt16()=>BinaryPrimitives.ReadUInt16LittleEndian(this.Read(2));
        public string Unicode(int characters)=>Encoding.Unicode.GetString(this.Read(checked(characters*2)));
        public (SqlType Type,object? Value) Value(){
            var code=this.Byte();
            if(code is 0x26 or 0x68 or 0x6D or 0x24 or 0x6F or 0x6E){
                var maximum=this.Byte();var length=this.Byte();SqlType type;
                if(code==0x26){type=maximum switch{1=>new("tinyint",typeof(byte)),2=>new("smallint",typeof(short)),4=>SqlType.Int,8=>SqlType.BigInt,_=>throw new InvalidDataException("Invalid INTN size.")};}
                else if(code==0x68){type=SqlType.Bit;}
                else if(code==0x6D){if(maximum==4){type=new("real",typeof(float));}else{type=new("float",typeof(double));}}
                else if(code==0x24){type=new("uniqueidentifier",typeof(Guid));}
                else if(code==0x6E){type=new("money",typeof(decimal),null,19,4);}
                else{type=new("datetime",typeof(DateTime));}
                if(length==0){return(type,null);}if(length!=maximum){throw new InvalidDataException("RPC length disagrees with its declared type.");}var value=this.Read(length);
                if(code==0x26){return(type,length switch{1=>(object)value[0],2=>BinaryPrimitives.ReadInt16LittleEndian(value),4=>BinaryPrimitives.ReadInt32LittleEndian(value),8=>BinaryPrimitives.ReadInt64LittleEndian(value),_=>throw new InvalidDataException()});}
                if(code==0x68){return(type,value[0]!=0);}
                if(code==0x24){return(type,new Guid(value));}
                if(code==0x6D){if(length==4){return(type,BinaryPrimitives.ReadSingleLittleEndian(value));}return(type,BinaryPrimitives.ReadDoubleLittleEndian(value));}
                if(code==0x6E){long amount;if(length==4){amount=BinaryPrimitives.ReadInt32LittleEndian(value);}else{amount=((long)BinaryPrimitives.ReadInt32LittleEndian(value)<<32)|BinaryPrimitives.ReadUInt32LittleEndian(value[4..]);}return(type,amount/10000m);}
                var origin=new DateTime(1900,1,1);if(length==4){return(type,origin.AddDays(BinaryPrimitives.ReadUInt16LittleEndian(value)).AddMinutes(BinaryPrimitives.ReadUInt16LittleEndian(value[2..])));}
                return(type,origin.AddDays(BinaryPrimitives.ReadInt32LittleEndian(value)).AddTicks((long)Math.Round(BinaryPrimitives.ReadUInt32LittleEndian(value[4..])*TimeSpan.TicksPerSecond/300d)));
            }
            if(code is 0x6A or 0x6C){
                _=this.Byte();var precision=this.Byte();var scale=this.Byte();var length=this.Byte();var type=new SqlType("decimal",typeof(decimal),null,precision,scale);if(length==0){return(type,null);}
                var sign=this.Byte();var magnitude=new BigInteger(this.Read(length-1),true,false);var digits=magnitude.ToString(CultureInfo.InvariantCulture).PadLeft(scale+1,'0');
                if(scale>0){digits=digits.Insert(digits.Length-scale,".");}if(sign==0){digits="-"+digits;}return(type,decimal.Parse(digits,CultureInfo.InvariantCulture));
            }
            if(code==0x63){
                var maximum=BinaryPrimitives.ReadInt32LittleEndian(this.Read(4));_=this.Read(5);var length=BinaryPrimitives.ReadInt32LittleEndian(this.Read(4));
                if(maximum<0){throw new InvalidDataException("Invalid RPC ntext metadata length.");}
                var type=new SqlType("ntext",typeof(string),-1);if(length==-1){return(type,null);}
                if(length<0||length>maximum||length>16*1024*1024||(length&1)!=0){throw new InvalidDataException("Invalid RPC ntext size.");}
                return(type,Encoding.Unicode.GetString(this.Read(length)));
            }
            if(code is 0xA7 or 0xAF or 0xE7 or 0xEF or 0xA5 or 0xAD or 0xF1){
                ushort maximum=ushort.MaxValue;byte[] collation=[];
                if(code==0xF1){if(this.Byte()!=0){throw new NotSupportedException("RPC XML schema collections are not supported.");}}
                else{maximum=this.UInt16();if(code is 0xA7 or 0xAF or 0xE7 or 0xEF){collation=this.Read(5).ToArray();}}
                string name=code switch{0xA7=>"varchar",0xAF=>"char",0xEF=>"nchar",0xA5=>"varbinary",0xAD=>"binary",0xF1=>"xml",_=>"nvarchar"};
                Type clr=typeof(string);if(code is 0xA5 or 0xAD){clr=typeof(byte[]);}int? max=maximum;if(maximum==ushort.MaxValue){max=-1;}else if(code is 0xE7 or 0xEF){max=maximum/2;}
                string? collationName=null;var codePage=0;if(code is 0xA7 or 0xAF){var incoming=TdsPacketWriter.ResolveIncomingCollation(collation);collationName=incoming.Name;codePage=incoming.CodePage;}
                var type=new SqlType(name,clr,max,Collation:collationName);byte[]? content;
                if(maximum==ushort.MaxValue){
                    var total=BinaryPrimitives.ReadInt64LittleEndian(this.Read(8));if(total==-1){return(type,null);}if(total>16*1024*1024){throw new InvalidDataException("RPC PLP value exceeds the configured limit.");}
                    using var buffer=new MemoryStream();while(true){var chunk=BinaryPrimitives.ReadInt32LittleEndian(this.Read(4));if(chunk==0){break;}if(chunk<0||buffer.Length+chunk>16*1024*1024){throw new InvalidDataException("Invalid RPC PLP size.");}buffer.Write(this.Read(chunk));}
                    if(total>=0&&total!=buffer.Length){throw new InvalidDataException("RPC PLP length mismatch.");}content=buffer.ToArray();
                }else{var length=this.UInt16();if(length==ushort.MaxValue){return(type,null);}if(length>maximum){throw new InvalidDataException("RPC string length exceeds its metadata.");}content=this.Read(length).ToArray();}
                if(clr==typeof(byte[])){return(type,content);}if(code is 0xA7 or 0xAF){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return(type,Encoding.GetEncoding(codePage,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetString(content));}return(type,Encoding.Unicode.GetString(content));
            }
            if(code is 0x28 or 0x29 or 0x2A or 0x2B){
                byte scale=7;if(code!=0x28){scale=this.Byte();}var length=this.Byte();var type=new SqlType(code switch{0x28=>"date",0x29=>"time",0x2B=>"datetimeoffset",_=>"datetime2"},code switch{0x28=>typeof(DateOnly),0x29=>typeof(TimeOnly),0x2B=>typeof(DateTimeOffset),_=>typeof(DateTime)},null,null,scale);
                if(length==0){return(type,null);}var data=this.Read(length);var timeLength=5;if(scale<=2){timeLength=3;}else if(scale<=4){timeLength=4;}var cursor=0;long ticks=0;
                if(code!=0x28){for(var i=0;i<timeLength;i++){ticks|=(long)data[cursor++]<<(8*i);}ticks*=checked((long)Math.Pow(10,7-scale));}
                if(code==0x29){return(type,new TimeOnly(ticks));}var days=data[cursor]|data[cursor+1]<<8|data[cursor+2]<<16;cursor+=3;var date=DateTime.MinValue.AddDays(days).AddTicks(ticks);
                if(code==0x28){return(type,DateOnly.FromDateTime(date));}if(code==0x2B){var offset=TimeSpan.FromMinutes(BinaryPrimitives.ReadInt16LittleEndian(data[cursor..]));return(type,new DateTimeOffset(DateTime.SpecifyKind(date,DateTimeKind.Utc)).ToOffset(offset));}return(type,date);
            }
            if(code==0x38){return(SqlType.Int,BinaryPrimitives.ReadInt32LittleEndian(this.Read(4)));}if(code==0x7F){return(SqlType.BigInt,BinaryPrimitives.ReadInt64LittleEndian(this.Read(8)));}
            throw new NotSupportedException($"RPC data type 0x{code:X2} is not supported.");
        }
    }
}
