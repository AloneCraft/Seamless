using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Seamless.Database.Client;

/// <summary>ネイティブ接続の推奨値を交換する版付きフレーム。</summary>
public static class NativeConnectionPolicyWire{
    public const byte Discover=10,Recommendation=11,Version=1;
    private const byte ProfileDiscoveryVersion=2;
    public const int MaximumProfileLength=64;
    public const int MaximumDiscoveryBytes=MaximumProfileLength+1;
    public const int RecommendationBytes=6;

    public static void ValidateProfile(string profile){
        ArgumentNullException.ThrowIfNull(profile);
        if(!ValidProfile(profile)){throw new ArgumentException("A profile must be 1 through 64 ASCII letters, digits, dots, underscores or hyphens.",nameof(profile));}
    }

    private static bool ValidProfile(ReadOnlySpan<char> profile){
        if(profile.Length is 0 or >MaximumProfileLength){return false;}
        foreach(var character in profile){
            if(!char.IsAsciiLetterOrDigit(character)&&character is not ('.' or '_' or '-')){return false;}
        }
        return true;
    }

    public static byte[] WriteDiscovery(string? profile){
        if(profile is null){return [Version];}
        ValidateProfile(profile);
        var bytes=new byte[profile.Length+1];
        bytes[0]=ProfileDiscoveryVersion;
        Encoding.ASCII.GetBytes(profile,bytes.AsSpan(1));
        return bytes;
    }

    public static string? ReadDiscovery(ReadOnlySequence<byte> payload){
        var reader=new SequenceReader<byte>(payload);
        if(!reader.TryRead(out var version)){throw new InvalidDataException("Missing native connection policy request version.");}
        if(version==Version&&reader.Remaining==0){return null;}
        if(version!=ProfileDiscoveryVersion||reader.Remaining is <=0 or >MaximumProfileLength){
            throw new InvalidDataException("Invalid native connection policy request.");
        }
        Span<byte> bytes=stackalloc byte[(int)reader.Remaining];
        if(!reader.TryCopyTo(bytes)){throw new InvalidDataException("Incomplete native connection policy profile.");}
        var profile=Encoding.ASCII.GetString(bytes);
        if(!ValidProfile(profile)){throw new InvalidDataException("Invalid native connection policy profile.");}
        return profile;
    }

    public static byte[] WriteRecommendation(NativeConnectionPolicy? policy){
        var response=new byte[RecommendationBytes];
        response[0]=Version;
        if(policy is null){return response;}
        response[1]=policy.Lifetime switch{
            NativeConnectionLifetime.KeepAlive=>1,
            NativeConnectionLifetime.ReleaseWhenIdle=>2,
            NativeConnectionLifetime.ReleaseAfterOperation=>3,
            _=>throw new InvalidDataException("Unknown native connection policy.")
        };
        if(policy.IdleTimeout is{} idle){BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(2),checked((uint)Math.Ceiling(idle.TotalMilliseconds)));}
        return response;
    }

    public static NativeConnectionPolicy? ReadRecommendation(ReadOnlySpan<byte> response){
        if(response.Length!=RecommendationBytes||response[0]!=Version){throw new InvalidDataException("Invalid native connection policy response.");}
        var idle=BinaryPrimitives.ReadUInt32LittleEndian(response[2..]);
        return response[1] switch{
            0 when idle==0=>null,
            1 when idle==0=>new(NativeConnectionLifetime.KeepAlive),
            2 when idle is >0 and <uint.MaxValue=>new(NativeConnectionLifetime.ReleaseWhenIdle,TimeSpan.FromMilliseconds(idle)),
            3 when idle==0=>new(NativeConnectionLifetime.ReleaseAfterOperation),
            _=>throw new InvalidDataException("Invalid native connection policy value.")
        };
    }
}
