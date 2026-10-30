using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Text;
using Seamless.Database.Gateway.Hosting;

namespace Seamless.Database.Gateway.Tds;

public sealed class TdsPacketWriter{
    private readonly PipeWriter writer;
    private byte packetId=1;

    public TdsPacketWriter(PipeWriter writer){
        this.writer=writer;
    }

    public async ValueTask SendPreLoginResponseAsync(CancellationToken cancellationToken=default){
        // PreLogin オプション構成:
        // Version (Token: 0x00, Offset: 21, Length: 6) -> 16.0.1000.6
        // Encryption (Token: 0x01, Offset: 27, Length: 1) -> 0x02 (Not supported / Encrypt off)
        // InstOpt (Token: 0x02, Offset: 28, Length: 1) -> 0x00
        // ThreadId (Token: 0x03, Offset: 29, Length: 4) -> 0x00000000
        // Mars (Token: 0x04, Offset: 33, Length: 1) -> 0x00 (Mars off)
        // Terminator (0xFF)
        using var stream=new MemoryStream();

        // オプショントークンヘッダ (各5バイト: token 1B, offset 2B BE, length 2B BE)
        WriteOptionHeader(stream,0x00,26,6); // Version
        WriteOptionHeader(stream,0x01,32,1); // Encryption
        WriteOptionHeader(stream,0x02,33,1); // InstOpt
        WriteOptionHeader(stream,0x03,34,4); // ThreadId
        WriteOptionHeader(stream,0x04,38,1); // Mars
        stream.WriteByte(0xFF);              // Terminator

        // ペイロードデータ
        // Version: 16.0.1000, sub-build 6
        stream.WriteByte(0x10); stream.WriteByte(0x00);
        stream.WriteByte(0x03); stream.WriteByte(0xE8);
        stream.WriteByte(0x00); stream.WriteByte(0x06);

        // Encryption: 0x02 (Certificate Not Supported)
        stream.WriteByte(0x02);

        // InstOpt: 0x00
        stream.WriteByte(0x00);

        // ThreadId: 0
        stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00); stream.WriteByte(0x00);

        // Mars: 0x00
        stream.WriteByte(0x00);

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendLoginAckAsync(string databaseName,CancellationToken cancellationToken=default){
        using var stream=new MemoryStream();

        // 1. LoginAck Token (0xAD)
        stream.WriteByte(TdsConstants.TokenLoginAck);
        var ackPayload=new MemoryStream();
        ackPayload.WriteByte(0x01); // Interface: SQL_DFLT
        WriteInt32BE(ackPayload,(int)TdsConstants.TdsVersion74); // TDS Version 7.4
        WriteStringWithLength8(ackPayload,"SeamlessDB"); // B_VARCHAR: 1 byte char count + UTF-16LE
        ackPayload.WriteByte(16); // Major: 16
        ackPayload.WriteByte(0);  // Minor: 0
        WriteUInt16BE(ackPayload,1000); // Build: 1000

        var ackBytes=ackPayload.ToArray();
        WriteUInt16LE(stream,(ushort)ackBytes.Length);
        stream.Write(ackBytes);

        // 2. EnvChange: Database changed
        WriteEnvChange(stream,TdsConstants.EnvChangeDatabase,databaseName,"master");

        // 3. Done Token (0xFD)
        WriteDone(stream,TdsConstants.DoneFinal,0);

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendQueryResultAsync(QueryResult result,CancellationToken cancellationToken=default){
        using var stream=new MemoryStream();

        if(result.Columns.Count>0){
            // 1. ColMetaData Token (0x81)
            stream.WriteByte(TdsConstants.TokenColMetaData);
            WriteUInt16LE(stream,(ushort)result.Columns.Count);

            foreach(var col in result.Columns){
                WriteUInt32LE(stream,0); // UserType
                WriteUInt16LE(stream,0); // Flags: Nullable

                var tdsType=GetTdsType(col.ClrType);
                stream.WriteByte(tdsType);

                if(tdsType==TdsConstants.TypeBigNVarChar){
                    WriteUInt16LE(stream,8000); // Max length
                    // Collation (5 bytes)
                    stream.Write(new byte[]{0x09,0x04,0xD0,0x00,0x34});
                }else if(tdsType==TdsConstants.TypeBigVarChar){
                    WriteUInt16LE(stream,8000);
                    stream.Write(new byte[]{0x09,0x04,0xD0,0x00,0x34});
                }else if(tdsType==TdsConstants.TypeBigVarBinary){
                    WriteUInt16LE(stream,8000);
                }else if(tdsType==TdsConstants.TypeDateTime2){
                    stream.WriteByte(7); // scale: 7
                }else{
                }

                // ColumnName (Length in characters 1 byte, UTF-16LE)
                stream.WriteByte((byte)col.Name.Length);
                stream.Write(Encoding.Unicode.GetBytes(col.Name));
            }

            // 2. Row Tokens (0xD1)
            foreach(var row in result.Rows){
                stream.WriteByte(TdsConstants.TokenRow);
                for(var i=0;i<result.Columns.Count;i++){
                    var col=result.Columns[i];
                    var val=row[i];
                    WriteColumnValue(stream,col.ClrType,val);
                }
            }

            // 3. Done Token (0xFD)
            WriteDone(stream,TdsConstants.DoneFinal|TdsConstants.DoneCount,result.Rows.Count);
        }else{
            // DML または SET 文
            WriteDone(stream,TdsConstants.DoneFinal|TdsConstants.DoneCount,result.RowsAffected);
        }

        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    public async ValueTask SendErrorAsync(string message,CancellationToken cancellationToken=default){
        using var stream=new MemoryStream();

        stream.WriteByte(TdsConstants.TokenError);
        var errPayload=new MemoryStream();
        WriteInt32LE(errPayload,50000); // Error Number
        errPayload.WriteByte(16);       // State
        errPayload.WriteByte(16);       // Class (Severity)
        WriteStringWithLength(errPayload,message);
        WriteStringWithLength(errPayload,"SeamlessDB");
        WriteStringWithLength(errPayload,"");
        WriteInt32LE(errPayload,1);     // LineNumber

        var errBytes=errPayload.ToArray();
        WriteUInt16LE(stream,(ushort)errBytes.Length);
        stream.Write(errBytes);

        WriteDone(stream,TdsConstants.DoneFinal|TdsConstants.DoneError,0);
        await this.WritePacketAsync(TdsConstants.PacketTypeTabularResult,stream.ToArray(),cancellationToken);
    }

    private async ValueTask WritePacketAsync(byte packetType,byte[]payload,CancellationToken cancellationToken){
        var length=payload.Length+TdsConstants.HeaderSize;
        var header=new byte[TdsConstants.HeaderSize];
        header[0]=packetType;
        header[1]=TdsConstants.StatusEndOfMessage;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2,2),(ushort)length);
        header[4]=0; header[5]=0; // ChannelId
        header[6]=this.packetId++;
        header[7]=0; // Window

        await this.writer.WriteAsync(header,cancellationToken);
        if(payload.Length>0){
            await this.writer.WriteAsync(payload,cancellationToken);
        }else{
        }
        await this.writer.FlushAsync(cancellationToken);
    }

    private static void WriteOptionHeader(Stream stream,byte token,ushort offset,ushort length){
        stream.WriteByte(token);
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf,offset);
        stream.Write(buf);
        BinaryPrimitives.WriteUInt16BigEndian(buf,length);
        stream.Write(buf);
    }

    private static void WriteEnvChange(Stream stream,byte type,string newVal,string oldVal){
        stream.WriteByte(TdsConstants.TokenEnvChange);
        var payload=new MemoryStream();
        payload.WriteByte(type);
        WriteStringWithLength8(payload,newVal);
        WriteStringWithLength8(payload,oldVal);

        var bytes=payload.ToArray();
        WriteUInt16LE(stream,(ushort)bytes.Length);
        stream.Write(bytes);
    }

    private static void WriteDone(Stream stream,ushort status,int rowCount){
        stream.WriteByte(TdsConstants.TokenDone);
        WriteUInt16LE(stream,status);
        WriteUInt16LE(stream,0); // CurCmd
        WriteInt64LE(stream,rowCount);
    }

    private static void WriteColumnValue(Stream stream,Type clrType,object? val){
        var underlying=Nullable.GetUnderlyingType(clrType)??clrType;
        if(val is null){
            if(underlying==typeof(string)||underlying==typeof(byte[])){
                WriteUInt16LE(stream,0xFFFF); // Null length
            }else{
                stream.WriteByte(0xFF); // Null length
            }
            return;
        }else{
        }

        if(underlying==typeof(int)){
            WriteInt32LE(stream,(int)val);
        }else if(underlying==typeof(long)){
            WriteInt64LE(stream,(long)val);
        }else if(underlying==typeof(short)){
            WriteInt16LE(stream,(short)val);
        }else if(underlying==typeof(byte)){
            stream.WriteByte((byte)val);
        }else if(underlying==typeof(bool)){
            stream.WriteByte((bool)val?(byte)1:(byte)0);
        }else if(underlying==typeof(string)){
            var str=(string)val;
            var bytes=Encoding.Unicode.GetBytes(str);
            WriteUInt16LE(stream,(ushort)bytes.Length);
            stream.Write(bytes);
        }else if(underlying==typeof(DateTime)){
            var dt=(DateTime)val;
            var dateVal=(int)(dt.Date-new DateTime(1,1,1)).TotalDays;
            var timeVal=(long)(dt.TimeOfDay.TotalSeconds*10000000L);
            stream.WriteByte(8); // Length
            var timeBytes=new byte[5];
            timeBytes[0]=(byte)(timeVal&0xFF);
            timeBytes[1]=(byte)((timeVal>>8)&0xFF);
            timeBytes[2]=(byte)((timeVal>>16)&0xFF);
            timeBytes[3]=(byte)((timeVal>>24)&0xFF);
            timeBytes[4]=(byte)((timeVal>>32)&0xFF);
            stream.Write(timeBytes);
            var dateBytes=new byte[3];
            dateBytes[0]=(byte)(dateVal&0xFF);
            dateBytes[1]=(byte)((dateVal>>8)&0xFF);
            dateBytes[2]=(byte)((dateVal>>16)&0xFF);
            stream.Write(dateBytes);
        }else if(underlying==typeof(double)){
            var buf=new byte[8];
            BinaryPrimitives.WriteDoubleLittleEndian(buf,(double)val);
            stream.Write(buf);
        }else if(underlying==typeof(float)){
            var buf=new byte[4];
            BinaryPrimitives.WriteSingleLittleEndian(buf,(float)val);
            stream.Write(buf);
        }else{
            var str=val.ToString()??string.Empty;
            var bytes=Encoding.Unicode.GetBytes(str);
            WriteUInt16LE(stream,(ushort)bytes.Length);
            stream.Write(bytes);
        }
    }

    private static byte GetTdsType(Type clrType){
        var underlying=Nullable.GetUnderlyingType(clrType)??clrType;
        if(underlying==typeof(int)){
            return TdsConstants.TypeInt4;
        }else if(underlying==typeof(long)){
            return TdsConstants.TypeInt8;
        }else if(underlying==typeof(short)){
            return TdsConstants.TypeInt2;
        }else if(underlying==typeof(byte)){
            return TdsConstants.TypeInt1;
        }else if(underlying==typeof(bool)){
            return TdsConstants.TypeBit;
        }else if(underlying==typeof(string)){
            return TdsConstants.TypeBigNVarChar;
        }else if(underlying==typeof(DateTime)){
            return TdsConstants.TypeDateTime2;
        }else if(underlying==typeof(double)){
            return TdsConstants.TypeFloat8;
        }else if(underlying==typeof(float)){
            return TdsConstants.TypeFloat4;
        }else if(underlying==typeof(byte[])){
            return TdsConstants.TypeBigVarBinary;
        }else{
            return TdsConstants.TypeBigNVarChar;
        }
    }

    private static void WriteStringWithLength(Stream stream,string text){
        WriteUInt16LE(stream,(ushort)text.Length);
        stream.Write(Encoding.Unicode.GetBytes(text));
    }

    private static void WriteStringWithLength8(Stream stream,string text){
        stream.WriteByte((byte)text.Length);
        stream.Write(Encoding.Unicode.GetBytes(text));
    }

    private static void WriteUInt16LE(Stream stream,ushort value){
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt16LE(Stream stream,short value){
        var buf=new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt32LE(Stream stream,int value){
        var buf=new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteUInt16BE(Stream stream,ushort value){
        var buf=new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteUInt32LE(Stream stream,uint value){
        var buf=new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt32BE(Stream stream,int value){
        var buf=new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf,value);
        stream.Write(buf);
    }

    private static void WriteInt64LE(Stream stream,long value){
        var buf=new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf,value);
        stream.Write(buf);
    }
}
