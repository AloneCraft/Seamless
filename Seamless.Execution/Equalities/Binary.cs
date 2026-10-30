using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Seamless.Execution.Equalities;

public readonly struct Binary<T>:IStrategy<Binary<T>,T>{
    private readonly ExecutionScope scope;
    public Binary(ExecutionScope scope){ArgumentNullException.ThrowIfNull(scope);this.scope=scope;scope.Codecs.Get<T>();}
    public static bool Equals(ref Binary<T> self,T? left,T? right){
        using var a=self.scope.Encode(left);using var b=self.scope.Encode(right);
        return a.Memory.Span.SequenceEqual(b.Memory.Span);
    }
    public static int GetHashCode(ref Binary<T> self,T value){
        using var encoded=self.scope.Encode(value);Span<byte> hash=stackalloc byte[32];SHA256.HashData(encoded.Memory.Span,hash);return BinaryPrimitives.ReadInt32LittleEndian(hash);
    }
}
