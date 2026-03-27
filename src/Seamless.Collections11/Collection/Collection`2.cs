namespace Seamless.Collections;
using Tsavorite.core;
/// <summary>
/// Tsavorite をバックエンドに使うコレクションの抽象クラス
/// <see cref="IDictionary{TKey,TValue}"/> を実装し、利用者に Tsavorite の詳細を見せない。
/// 静的ポリモーフィズムにより仮想ディスパッチを回避する。
/// </summary>
public abstract class Collection<TKey, TValue>:Collection<TKey,TValue,AddSessionFunctions<TKey,TValue>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>>where TKey : IEquatable<TKey>where TValue : IEquatable<TValue>{
    // RMW による条件付き削除専用セッション。Remove() の実装で使う
    private readonly ClientSession<SpanByte,SpanByte,SpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions<TKey>,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> deleteSession;
    private bool deleteSessionDisposed;
    protected Collection(TsavoriteKV<SpanByte,SpanByte,StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>,SpanByteAllocator<StoreFunctions<SpanByte,SpanByte,SpanByteComparer,SpanByteRecordDisposer>>> Store,long LogMemorySize=0,double MutableFraction=1.0) : base(Store,new AddSessionFunctions<TKey,TValue>(),LogMemorySize,MutableFraction) {
        this.deleteSession=Store.NewSession<SpanByte,SpanByteAndMemory,Empty,DeleteSessionFunctions<TKey>>(new DeleteSessionFunctions<TKey>());
    }
    // キーが存在した場合のみ削除し true を返す。存在しなければ false (Status.Found ベース判定)
    public override bool Remove(TKey Key) {
        var KeyBytes=Serialize(Key);
        var emptyInput=default(SpanByte);
        SpanByteAndMemory output=default;
        Status status;
        unsafe {
            fixed(byte* pKey=KeyBytes) {
                var fixedKey=SpanByte.FromPinnedPointer(pKey,KeyBytes.Length);
                var deleteSession=this.deleteSession;
                status=deleteSession.BasicContext.RMW(ref fixedKey,ref emptyInput,ref output);
                if(status.IsPending) {
                    deleteSession.BasicContext.CompletePendingWithOutputs(out var completedOutputs,wait:true);
                    completedOutputs.Next();
                    status=completedOutputs.Current.Status;
                    completedOutputs.Dispose();
                }
            }
        }
        return status.Found;
    }
    protected override void Dispose(bool disposing) {
        if(!this.deleteSessionDisposed) {
            this.deleteSessionDisposed=true;
            if(disposing) this.deleteSession.Dispose();
        }
        base.Dispose(disposing);
    }
}
