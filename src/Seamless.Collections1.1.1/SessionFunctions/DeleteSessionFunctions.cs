namespace Seamless.Collections;
using Tsavorite.core;
/*
Upsert
    GetUpsertValueLength//サイズ見積もり
    if(FoundMutable){
        ConcurrentWriter
    }else{
        SingleWriter
        PostSingleWriter
    }
    PostUpsertOperation//事後処理
Delete
    GetUpsertValueLength//サイズ見積もり
    if(FoundMutable){
       InPlaceDeleter
    }else{
       InitialDeleter
       PostInitialDeleter
    }
    PostDeleteOperation
RMW
    if(FoundMutable){
        GetRMWModifiedValueLength//サイズ見積もり
        InPlaceUpdater//ExpireAndStopすると論理削除
    }else if(FoundReadOnly || FoundDisk){
        NeedCopyUpdate//コピー更新を許可するか
        GetRMWModifiedValueLength
        CopyUpdater   //コピー更新時に呼ばれる。ここでも ExpireAndStop を設定する。
        PostCopyUpdater
    }else{
        GetRMWInitialValueLength//サイズ見積もり
        NeedInitialUpdate     //ここでfalseを返すとRMWはNotFoundを返す
        InitialUpdater        //RMW の未存在分岐で呼ばれる(この実装では削除対象なしとして false)。
        PostInitialUpdater    //InitialUpdater 直後に呼ばれる。
    }
    PostRMWOperation//RMW 確定後に呼ばれる。
RMW Async
    RMWCompletionCallback //RMW の非同期I/O完了時に呼ばれる。
    ReadCompletionCallback//Read の非同期I/O完了時に呼ばれる。
出力のヒープ化
    ConvertOutputToHeap
*/
public sealed class DeleteSessionFunctions<TKey>:SSessionFunctions {
	//↓Reader
    public bool SingleReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo) => true;
    public bool ConcurrentReader(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory dst,ref ReadInfo readInfo,ref RecordInfo recordInfo) => true;


    //↓InitialWriter
    public bool SingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }
    
    
    
    
    
    
    
    
    //↓InitialWriter
    public bool ConcurrentWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,ref RecordInfo recordInfo) {
        src.CopyTo(ref dst);
        return true;
    }







    //↓PostInitialWriter
    public void PostSingleWriter(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref SpanByte dst,ref SpanByteAndMemory output,ref UpsertInfo upsertInfo,WriteReason reason) { }


    //↓PostUpserOperation
    void SSessionFunctions.PostUpsertOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref SpanByte src,ref UpsertInfo upsertInfo,TEpochAccessor epoch) { }


    // キーが存在しない場合は新規レコードを作らない → Status.NotFound
    public bool NeedInitialUpdate(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
    	return false;
	}
    public bool InitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        return false;
    }
    // キーが存在する場合は Tombstone を挿入して削除 → Status.Found
    public bool InPlaceUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    public void PostInitialUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo) { }


    public bool NeedCopyUpdate(ref SpanByte key,ref SpanByte input,ref SpanByte value,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
    	return true;
    }
    public bool CopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo,ref RecordInfo recordInfo) {
        rmwInfo.Action=RMWAction.ExpireAndStop;
        return false;
    }
    public bool PostCopyUpdater(ref SpanByte key,ref SpanByte input,ref SpanByte oldValue,ref SpanByte newValue,ref SpanByteAndMemory output,ref RMWInfo rmwInfo){
        throw new NotSupportedException();
    }
    void SSessionFunctions.PostRMWOperation<TEpochAccessor>(ref SpanByte key,ref SpanByte input,ref RMWInfo rmwInfo,TEpochAccessor epoch) { 
    }
    public void RMWCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { 
        throw new NotSupportedException();
    }

	//Delete
    public bool SingleDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
        throw new NotSupportedException();
	}
    public void PostSingleDeleter(ref SpanByte key,ref DeleteInfo deleteInfo) { 
    	throw new NotSupportedException(); 
    }
    public bool ConcurrentDeleter(ref SpanByte key,ref SpanByte value,ref DeleteInfo deleteInfo,ref RecordInfo recordInfo){
    	return true;
    }
    void SSessionFunctions.PostDeleteOperation<TEpochAccessor>(ref SpanByte key,ref DeleteInfo deleteInfo,TEpochAccessor epoch) { 
        throw new NotSupportedException();
    }
    //


    public void ReadCompletionCallback(ref SpanByte key,ref SpanByte input,ref SpanByteAndMemory output,Empty ctx,Status status,RecordMetadata recordMetadata) { }


    public int GetRMWModifiedValueLength(ref SpanByte value,ref SpanByte input) => value.Length;
    //
    //
    public int GetRMWInitialValueLength(ref SpanByte input) => input.Length+4;
    //


    public int GetUpsertValueLength(ref SpanByte value,ref SpanByte input) => value.Length+4;
    //
    //
    //
    //
    //
    //
    //


    public void ConvertOutputToHeap(ref SpanByte input,ref SpanByteAndMemory output) { }
}