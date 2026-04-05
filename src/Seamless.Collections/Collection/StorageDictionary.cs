namespace Seamless.Collections;
public sealed class StorageDictionary<TKey,TValue>:Dictionary<TKey,TValue>where TKey :IEquatable<TKey>where TValue : IEquatable<TValue>{
    public StorageDictionary(StorageDictionaryStore<TKey,TValue>Store):base(Store){
    }
    private protected override bool WouldOverflowMutable(int KeyLength,int ValueLength){
        // Storage版はディスクベースなので、メモリがいっぱいになる前にコンパクションを実行
        var Log = this.CollectionStore.Store.Log;
        var Used = Log.TailAddress - Log.BeginAddress;
        var MutableBytes = (long)(this.LogMemorySize * this.MutableFraction);
        if(MutableBytes <= 0) {
            MutableBytes = this.LogMemorySize;
        }
        var Required = (long)KeyLength + ValueLength + RecordOverheadBytes;

        // メモリの80%を使用したらコンパクションを実行
        if(Used + Required > MutableBytes * 0.8) {
            this.CollectionStore.CompactWithLookup();
        }

        return false; // Storage版は常にfalseを返す（エビクションで対応）
    }
}
