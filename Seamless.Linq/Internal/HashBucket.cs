namespace Seamless.Linq;

/*
バケット番号の計算をハッシュ表 3 種で共有する。

  - LookupBuilder / Lookup (ToLookup.cs)   Join / GroupJoin / GroupBy / ToLookup
  - HashSetSlim                            Distinct / Union / Except / Intersect
  - DictionarySlim                         CountBy / AggregateBy / ToDictionary

どれも bucketsLength が 2 の冪で、バケット番号には下位ビットしか使わない。
int の既定ハッシュは恒等なので、(v&mask)==0 のような述語で絞られたキー集合は
下位 k ビットが定数になり、到達できるバケットが 1/2^k へ減って鎖が線形に伸びる。
述語プッシュダウンは述語をキー生成の前へ動かす変換なので、この形を踏みやすい。

奇数を掛けるだけでは bit0 が変わらず下位ビットのパターンが保存されてしまうため、
上位ビットを xor で折り返して低位へ拡散させる。

これは事故（自然に起きるビット偏り）への対策であって、攻撃対策ではない。
混合関数は公開されているので、これを狙った衝突キーは総当たりで作れる。
攻撃耐性が要るなら乱数シードか衝突閾値フォールバックが別途必要になる。
*/
internal static class HashBucket {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index(uint hashCode,int bucketsLength) {
        var mixed = hashCode*2654435769u;
        return (int)((mixed^(mixed>>16))&(uint)(bucketsLength-1));
    }
}
