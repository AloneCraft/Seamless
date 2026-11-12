using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Seamless.Database.Client;

public enum NativeAuthenticationError:byte{InvalidRequest=1,Rejected=2,Unavailable=3,Busy=4}

/// <summary>既存のネイティブ要求と独立した有界の認証交換。入力から型や実行コードを解決しない。</summary>
public static class NativeAuthenticationWire{
    public const byte Authenticate=6,Authenticated=7,Error=8,Reauthenticate=9;
    public const int MaximumTokenBytes=65536;
    public const int MaximumRequestBytes=MaximumTokenBytes+6;
    public const int MaximumAcknowledgementBytes=4096+21;
    private const byte Version=1,Bearer=1;
    private static readonly UTF8Encoding Utf8=new(false,true);

    public static byte[] Request(string token){
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if(token.Length>MaximumTokenBytes||token.Any(static value=>char.IsWhiteSpace(value)||char.IsControl(value))){throw new ArgumentException("Invalid authentication token size or characters.",nameof(token));}
        var length=Utf8.GetByteCount(token);if(length>MaximumTokenBytes){throw new ArgumentException("The authentication token is too large.",nameof(token));}
        var bytes=new byte[length+6];bytes[0]=Version;bytes[1]=Bearer;BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(2),length);Utf8.GetBytes(token.AsSpan(),bytes.AsSpan(6));return bytes;
    }
    public static string ReadRequest(ReadOnlySequence<byte> bytes){
        if(bytes.Length<6||bytes.Length>MaximumRequestBytes){throw new InvalidDataException("Invalid native authentication request size.");}
        var reader=new SequenceReader<byte>(bytes);reader.TryRead(out var version);reader.TryRead(out var method);reader.TryReadLittleEndian(out int length);
        if(version!=Version||method!=Bearer||length is <=0 or >MaximumTokenBytes||reader.Remaining!=length){throw new InvalidDataException("Invalid native authentication request header.");}
        var token=Text(bytes.Slice(reader.Position));
        if(string.IsNullOrWhiteSpace(token)||token.Any(static value=>char.IsWhiteSpace(value)||char.IsControl(value))){throw new InvalidDataException("Invalid native authentication token text.");}return token;
    }
    public static byte[] Acknowledgement(Guid authorityId,string subject){
        if(authorityId==Guid.Empty){throw new ArgumentException("An authentication authority is required.",nameof(authorityId));}
        if(!ValidSubject(subject)){throw new ArgumentException("Invalid authentication subject.",nameof(subject));}
        var length=Utf8.GetByteCount(subject);if(length>4096){throw new ArgumentException("The authentication subject is too large.",nameof(subject));}
        var bytes=new byte[length+21];bytes[0]=Version;authorityId.TryWriteBytes(bytes.AsSpan(1,16));BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(17),length);Utf8.GetBytes(subject.AsSpan(),bytes.AsSpan(21));return bytes;
    }
    public static (Guid AuthorityId,string Subject) ReadAcknowledgement(ReadOnlySequence<byte> bytes){
        if(bytes.Length<21||bytes.Length>MaximumAcknowledgementBytes){throw new InvalidDataException("Invalid native authentication acknowledgement size.");}
        Span<byte> header=stackalloc byte[21];bytes.Slice(0,21).CopyTo(header);var authority=new Guid(header.Slice(1,16));var length=BinaryPrimitives.ReadInt32LittleEndian(header[17..]);
        if(header[0]!=Version||authority==Guid.Empty||length is <=0 or >4096||bytes.Length!=21L+length){throw new InvalidDataException("Invalid native authentication acknowledgement header.");}
        var subject=Text(bytes.Slice(21));if(!ValidSubject(subject)){throw new InvalidDataException("Invalid native authentication subject.");}return(authority,subject);
    }
    public static byte[] Failure(NativeAuthenticationError error){if(error is <NativeAuthenticationError.InvalidRequest or >NativeAuthenticationError.Busy){throw new ArgumentOutOfRangeException(nameof(error));}return[Version,(byte)error];}
    public static NativeAuthenticationError ReadFailure(ReadOnlySpan<byte> bytes){
        if(bytes.Length!=2||bytes[0]!=Version||bytes[1] is <(byte)NativeAuthenticationError.InvalidRequest or >(byte)NativeAuthenticationError.Busy){throw new InvalidDataException("Invalid native authentication failure.");}return(NativeAuthenticationError)bytes[1];
    }
    private static bool ValidSubject(string? subject)=>!string.IsNullOrWhiteSpace(subject)&&subject.Length<=1024&&!subject.Any(char.IsControl);
    private static string Text(ReadOnlySequence<byte> bytes){
        try{if(bytes.IsSingleSegment){return Utf8.GetString(bytes.FirstSpan);}return Utf8.GetString(bytes.ToArray());}
        catch(DecoderFallbackException){throw new InvalidDataException("Invalid native authentication UTF-8.");}
    }
}
