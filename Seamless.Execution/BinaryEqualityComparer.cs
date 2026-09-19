using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Seamless.Execution;
public sealed class BinaryEqualityComparer<T>:IEqualityComparer<T>{
    private readonly ExecutionScope scope;
    public BinaryEqualityComparer(ExecutionScope scope){
        ArgumentNullException.ThrowIfNull(scope);
        this.scope=scope;
        scope.Codecs.Get<T>();
    }
    public bool Equals(T? x,T? y){
        using var left=this.scope.Encode(x);
        using var right=this.scope.Encode(y);
        return left.Memory.Span.SequenceEqual(right.Memory.Span);
    }
    public int GetHashCode(T value){
        using var encoded=this.scope.Encode(value);
        Span<byte> hash=stackalloc byte[32];
        SHA256.HashData(encoded.Memory.Span,hash);
        return BinaryPrimitives.ReadInt32LittleEndian(hash);
    }
}
