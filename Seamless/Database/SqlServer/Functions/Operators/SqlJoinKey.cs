namespace Seamless.Database.SqlServer.Functions;
/// <summary>
/// 結合の等しさのキー。複数の列で結ぶときに使う。どれかが NULL なら NULL を返し、どの行とも結ばない(SQL の = と同じ)。
/// </summary>
public static class SqlJoinKey{
    /// <summary>列の値の組をキーにする。NULL を含めば NULL。</summary>
    public static object? Of(params object?[] parts){
        foreach(var part in parts){
            if(part is null){
                return null;
            }
        }
        return new Composite(parts);
    }
    private sealed class Composite(object?[] parts){
        private readonly object?[] parts=parts;
        public override bool Equals(object? obj)=>obj is Composite other&&this.parts.SequenceEqual(other.parts);
        public override int GetHashCode(){
            var hash=new HashCode();
            foreach(var part in this.parts){
                hash.Add(part);
            }
            return hash.ToHashCode();
        }
    }
}
