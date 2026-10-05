namespace Seamless.Collections.Tsavorite;
/// <summary>詰め直し(<see cref="CollectionStore{TKey,TValue,TStoreFunctions,TAllocator}.Compact"/>)の前後のログの位置。</summary>
/// <param name="BeginAddressBefore">詰め直す前の先頭アドレス。</param>
/// <param name="BeginAddressAfter">詰め直したあとの先頭アドレス。</param>
/// <param name="TailAddressBefore">詰め直す前の末尾アドレス。</param>
/// <param name="TailAddressAfter">詰め直したあとの末尾アドレス。生きているレコードの写しが末尾に書かれるので、増える。</param>
public readonly record struct CompactionResult(long BeginAddressBefore,long BeginAddressAfter,long TailAddressBefore,long TailAddressAfter) {
    /// <summary>詰め直す前の、先頭から末尾までの大きさ。</summary>
    public long LogBytesBefore => this.TailAddressBefore-this.BeginAddressBefore;
    /// <summary>詰め直したあとの、先頭から末尾までの大きさ。</summary>
    public long LogBytesAfter => this.TailAddressAfter-this.BeginAddressAfter;
    /// <summary>空きへ戻った大きさ。</summary>
    public long ReclaimedBytes => this.LogBytesBefore-this.LogBytesAfter;
}
