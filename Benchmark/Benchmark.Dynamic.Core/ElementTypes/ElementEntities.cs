namespace Benchmark.Dynamic.ElementTypes;

/// <summary>
/// 複数の項目を持つ参照型の要素。<c>record class</c> なので値による等しさを持つ(参照の等しさに頼る近道を見つけられる)。
/// 比較できる型(<see cref="IComparable{T}"/>)ではないので、Min/Max/OrderBy の恒等キーには使わない。
/// </summary>
public sealed record Order(int Id,int CustomerId,decimal Price);

/// <summary>
/// ハッシュコードをわざと衝突させる(4通りしかない)キー。Distinct・Union・GroupBy・Join のハッシュ表が、
/// ハッシュが同じでも等しくない要素を正しく扱えるかを確かめる。
/// </summary>
public readonly record struct CollidingKey(int Value):IComparable<CollidingKey>{
    public override int GetHashCode()=>this.Value&3;

    public int CompareTo(CollidingKey other)=>this.Value.CompareTo(other.Value);
}
