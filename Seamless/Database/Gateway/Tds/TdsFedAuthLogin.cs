using System.Buffers.Binary;
using System.Text;
using Seamless.Database.Query;

namespace Seamless.Database.Gateway.Tds;

internal static class TdsFedAuthLogin{
    private const int FixedLoginLength=94;
    internal const int MaximumLoginBytes=131071;

    internal static bool IsSupportedPreLogin(ReadOnlySpan<byte> payload){
        if(payload.Length<12||payload[0]!=0||BinaryPrimitives.ReadUInt16BigEndian(payload[3..])!=6){return false;}
        var end=0;
        while(end<payload.Length&&payload[end]!=0xff){
            if(payload.Length-end<5||payload[end]==0x07){return false;}
            end+=5;
        }
        if(end>=payload.Length){return false;}
        for(var i=0;i<end;i+=5){
            var offset=BinaryPrimitives.ReadUInt16BigEndian(payload[(i+1)..]);
            var length=BinaryPrimitives.ReadUInt16BigEndian(payload[(i+3)..]);
            if(offset<=end||offset>payload.Length||length>payload.Length-offset){return false;}
        }
        return true;
    }

    internal static string ReadToken(ReadOnlySpan<byte> login){
        if(login.Length is <FixedLoginLength or >MaximumLoginBytes||BinaryPrimitives.ReadInt32LittleEndian(login)!=login.Length
            ||(login[27]&0x11)!=0x10||(login[25]&0x80)!=0
            ||ReadUInt16(login,42)!=0||ReadUInt16(login,46)!=0||ReadUInt16(login,80)!=0||ReadUInt16(login,84)!=0||ReadUInt16(login,88)!=0){throw Rejected();}
        var extensionOffset=ReadUInt16(login,56);
        var extensionLength=ReadUInt16(login,58);
        if(extensionOffset<FixedLoginLength||extensionLength<4||extensionLength>login.Length-extensionOffset){throw Rejected();}
        var firstFeature=BinaryPrimitives.ReadInt32LittleEndian(login[extensionOffset..]);
        if(firstFeature<extensionOffset+extensionLength||firstFeature>=login.Length){throw Rejected();}
        var features=login[firstFeature..];
        ReadOnlySpan<byte> token=default;
        while(!features.IsEmpty){
            var id=features[0];
            if(id==0xff){
                if(features.Length!=1||token.IsEmpty){throw Rejected();}
                return Encoding.Unicode.GetString(token);
            }
            if(features.Length<5){throw Rejected();}
            var length=BinaryPrimitives.ReadInt32LittleEndian(features[1..]);
            features=features[5..];
            if(length<0||length>features.Length){throw Rejected();}
            if(id==0x02){
                var data=features[..length];
                // 明示アクセストークンとFedAuthRequiredのエコー。サーバーのnonceは送信しない。
                if(!token.IsEmpty||length<7||data[0]!=0x03){throw Rejected();}
                var bytes=BinaryPrimitives.ReadInt32LittleEndian(data[1..]);
                if(bytes!=length-5||(bytes&1)!=0){throw Rejected();}
                token=data[5..];
                for(var i=0;i<token.Length;i+=2){
                    if(token[i+1]!=0||token[i] is <=32 or >=127){throw Rejected();}
                }
            }
            features=features[length..];
        }
        throw Rejected();
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    internal static SqlQueryException Rejected()=>new("Login failed.",18456);
}
