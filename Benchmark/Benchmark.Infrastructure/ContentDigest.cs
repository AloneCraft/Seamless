using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Benchmark.Infrastructure;

/// <summary>固定メモリの複数指標。4096件以下では全内容も保持する。ハッシュ一致は完全一致の証明ではない。</summary>
public sealed class ContentDigest:IDisposable{
    private const int ExactLimit=4096;
    private readonly IncrementalHash ordered=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private List<long>? exact=[];
    private long count;
    private ulong sum,hash1,hash2;
    public void Add(long value){
        this.count++;
        var bits=unchecked((ulong)value);
        unchecked{this.sum+=bits;this.hash1+=Mix(bits);this.hash2+=Mix(bits^0xD6E8FEB86659FD93UL);}
        Span<byte> bytes=stackalloc byte[8];BinaryPrimitives.WriteInt64LittleEndian(bytes,value);this.ordered.AppendData(bytes);
        if(this.count<=ExactLimit){this.exact!.Add(value);}else{this.exact=null;}
    }
    public string Fingerprint(bool ordered=false){
        var order="";if(ordered){order=Convert.ToHexString(this.ordered.GetCurrentHash());}
        return Infrastructure.Fingerprint.OfJSON(new{Version="content-v2",Count=this.count,Sum=this.sum,Hash1=this.hash1,Hash2=this.hash2,Order=order});
    }
    public void AssertMatches(ContentDigest expected,bool ordered=false){
        if(this.Fingerprint(ordered)!=expected.Fingerprint(ordered)){throw new InvalidDataException("Streaming content mismatch.");}
        if(this.exact is not null&&expected.exact is not null){
            IEnumerable<long> actualValues=this.exact,expectedValues=expected.exact;
            if(!ordered){actualValues=actualValues.Order();expectedValues=expectedValues.Order();}
            if(!actualValues.SequenceEqual(expectedValues)){throw new InvalidDataException("Exact content mismatch.");}
        }
    }
    private static ulong Mix(ulong value){
        unchecked{value+=0x9E3779B97F4A7C15UL;value=(value^(value>>30))*0xBF58476D1CE4E5B9UL;value=(value^(value>>27))*0x94D049BB133111EBUL;return value^(value>>31);}
    }
    public void Dispose()=>this.ordered.Dispose();
}
