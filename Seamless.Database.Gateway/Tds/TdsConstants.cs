namespace Seamless.Database.Gateway.Tds;

public static class TdsConstants{
    public const int DefaultPort=14333;
    public const int HeaderSize=8;

    // Packet Types
    public const byte PacketTypeSqlBatch=0x01;
    public const byte PacketTypePreTds7Login=0x02;
    public const byte PacketTypeTabularResult=0x04;
    public const byte PacketTypeAttention=0x06;
    public const byte PacketTypeTransactionManager=0x0E;
    public const byte PacketTypePreLogin=0x12;

    // Status Flags
    public const byte StatusNormal=0x00;
    public const byte StatusEndOfMessage=0x01;
    public const byte StatusIgnoreEvent=0x02;
    public const byte StatusResetConnection=0x08;

    // Tokens
    public const byte TokenColMetaData=0x81;
    public const byte TokenError=0xAA;
    public const byte TokenInfo=0xAB;
    public const byte TokenLoginAck=0xAD;
    public const byte TokenRow=0xD1;
    public const byte TokenNbcRow=0xD2;
    public const byte TokenEnvChange=0xE3;
    public const byte TokenDone=0xFD;
    public const byte TokenDoneProc=0xFE;
    public const byte TokenDoneInProc=0xFF;

    // Done Status Flags
    public const ushort DoneFinal=0x0000;
    public const ushort DoneMore=0x0001;
    public const ushort DoneError=0x0002;
    public const ushort DoneCount=0x0010;

    // EnvChange Types
    public const byte EnvChangeDatabase=0x01;
    public const byte EnvChangePacketSize=0x04;

    // Data Types (Fixed & Nullable)
    public const byte TypeInt1=0x30;
    public const byte TypeBit=0x32;
    public const byte TypeInt2=0x34;
    public const byte TypeInt4=0x38;
    public const byte TypeInt8=0x7F;
    public const byte TypeFloat4=0x3B;
    public const byte TypeFloat8=0x3E;
    public const byte TypeDecimal=0x6C;
    public const byte TypeBigVarChar=0xA7;
    public const byte TypeBigNVarChar=0xE7;
    public const byte TypeBigVarBinary=0xA5;
    public const byte TypeDateTime2=0x2A;

    // Nullable BYTELEN Types (MS-TDS)
    public const byte TypeIntN=0x26;
    public const byte TypeBitN=0x68;
    public const byte TypeFltN=0x6D;
    public const byte TypeDateTimeN=0x6F;

    // TDS Versions
    public const uint TdsVersion74=0x74000004; // SQL Server 2012+
}
