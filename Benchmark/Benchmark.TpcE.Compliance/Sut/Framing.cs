using System.Buffers.Binary;
namespace Benchmark.TpcE.Compliance;
/// <summary>運転側と SUT の通信の枠: [長さ int32 リトルエンディアン][UTF-8 の JSON]。</summary>
internal static class Framing{
    public const int MaximumLength=4*1024*1024;
    /// <summary>1 件読む。相手が閉じていたら null。</summary>
    public static byte[]? Read(Stream stream){
        Span<byte> header=stackalloc byte[4];
        if(!ReadExactly(stream,header)){
            return null;
        }
        var length=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(length<0||length>MaximumLength){
            throw new InvalidDataException($"Bad frame length {length}.");
        }
        var body=new byte[length];
        if(!ReadExactly(stream,body)){
            return null;
        }
        return body;
    }
    public static void Write(Stream stream,ReadOnlySpan<byte> body){
        Span<byte> header=stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header,body.Length);
        stream.Write(header);
        stream.Write(body);
        stream.Flush();
    }
    private static bool ReadExactly(Stream stream,Span<byte> buffer){
        var total=0;
        while(total<buffer.Length){
            var read=stream.Read(buffer[total..]);
            if(read==0){
                return false;
            }
            total+=read;
        }
        return true;
    }
}
