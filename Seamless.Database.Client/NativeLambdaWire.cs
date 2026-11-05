using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Seamless.Database.Client;

/// <summary>旧要求と区別する準備・実行フレーム。実行時は構造を再送しない。</summary>
public static class NativeLambdaWire{
    public const byte Prepare=16,Prepared=17,Execute=18,Error=19;
    private static readonly UTF8Encoding Utf8=new(false,true);
    public static byte[] PrepareRequest(string database,NativeLambdaProgram program)=>Prefix(database,program.Encode());
    public static byte[] ExecuteRequest(string database,NativePreparedLambda handle,Guid transaction,ReadOnlyMemory<byte> arguments){
        var payload=new byte[checked(40+arguments.Length)];handle.Epoch.TryWriteBytes(payload);BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(16),handle.Handle);transaction.TryWriteBytes(payload.AsSpan(24));arguments.Span.CopyTo(payload.AsSpan(40));return Prefix(database,payload);
    }
    public static byte[] Handle(NativePreparedLambda handle){var bytes=new byte[24];handle.Epoch.TryWriteBytes(bytes);BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16),handle.Handle);return bytes;}
    public static NativePreparedLambda ReadHandle(ReadOnlySpan<byte> bytes){if(bytes.Length!=24){throw new InvalidDataException("Invalid prepared lambda handle.");}return new(new Guid(bytes[..16]),BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]));}
    public static (string Database,byte[] Payload) ReadRequest(byte[] bytes){
        if(bytes.Length<2){throw new InvalidDataException("Missing lambda database.");}var length=BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if(length is 0 or >512||bytes.Length<length+2){throw new InvalidDataException("Invalid lambda database length.");}
        return(Utf8.GetString(bytes,2,length),bytes[(length+2)..]);
    }
    public static (NativePreparedLambda Handle,Guid Transaction,byte[] Arguments) ReadExecution(byte[] bytes){
        if(bytes.Length<40){throw new InvalidDataException("Incomplete lambda execution header.");}return(ReadHandle(bytes.AsSpan(0,24)),new Guid(bytes.AsSpan(24,16)),bytes[40..]);
    }
    public static byte[] Failure(Exception error){
        var code="Execution";var before=false;
        if(error is NativeLambdaException lambda){code=lambda.Code;before=lambda.ExecutionNotStarted;}
        var message=error.Message;if(message.Length>4096){message=message[..4096];}
        return JsonSerializer.SerializeToUtf8Bytes(new FailureData(code,message,before));
    }
    public static NativeLambdaException ReadFailure(byte[] bytes){
        if(bytes.Length>32768){throw new InvalidDataException("Invalid lambda failure size.");}var failure=JsonSerializer.Deserialize<FailureData>(bytes)??throw new InvalidDataException("Missing lambda failure.");return new(failure.Code,failure.Message,failure.Before);
    }
    private static byte[] Prefix(string database,byte[] payload){
        ArgumentException.ThrowIfNullOrWhiteSpace(database);var name=Utf8.GetBytes(database);if(name.Length>512){throw new ArgumentException("The database name is too long.",nameof(database));}
        var bytes=new byte[checked(name.Length+2+payload.Length)];BinaryPrimitives.WriteUInt16LittleEndian(bytes,(ushort)name.Length);name.CopyTo(bytes,2);payload.CopyTo(bytes,name.Length+2);return bytes;
    }
    private sealed record FailureData(string Code,string Message,bool Before);
}
