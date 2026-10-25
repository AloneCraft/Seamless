namespace Seamless.Collections.Tsavorite.Scans;

/// <summary>最新レコードのアドレスを照合し、古い版と削除済みのキーを除いて逐次走査する。</summary>
internal static class Lookup{
    internal static unsafe bool Next<TStoreFunctions,TAllocator,TScanFunctions>(
        TsavoriteKV<TStoreFunctions,TAllocator> store,ref TScanFunctions functions,ref long cursor,long untilAddress=-1)
        where TStoreFunctions:IStoreFunctions
        where TAllocator:IAllocator<TStoreFunctions>
        where TScanFunctions:IScanIteratorFunctions{
        var log=store.Log;
        if(untilAddress<0){untilAddress=log.TailAddress;}
        cursor=Math.Max(cursor,log.BeginAddress);
        if(!functions.OnStart(cursor,untilAddress)){return false;}
        long accepted=0;
        using var session=store.NewSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,ContainsDelete>(new());
        using var scan=log.Scan(cursor,untilAddress);
        try{
            while(scan.GetNext()){
                cursor=scan.NextAddress;
                if(scan.Info.Tombstone){continue;}
                var input=default(EqualityEmpty);
                var output=false;
                var options=default(ReadOptions);
                RecordMetadata metadata;
                Status status;
                // ネイティブの条件付き走査ではディスク上の衝突連鎖で生存キーが欠ける。
                // 通常のキー検索の完了アドレスで版を判定する。キーはI/O完了まで固定する。
                var key=scan.Key;
                fixed(byte* pointer=key){
                    status=session.BasicContext.Read((FixedSpanByteKey)key,ref input,ref output,ref options,out metadata);
                    if(status.IsPending){
                        session.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);
                        try{
                            if(!completed.Next()){throw new InvalidDataException("The liveness read completed without a result.");}
                            status=completed.Current.Status;
                            metadata=completed.Current.RecordMetadata;
                        }finally{completed.Dispose();}
                    }
                }
                if(!status.IsCompletedSuccessfully){throw new IOException($"The liveness read failed: {status}.");}
                if(!status.Found||metadata.Address!=scan.CurrentAddress){continue;}
                if(!functions.Reader(in scan,metadata,accepted,out var result)){
                    functions.OnStop(false,accepted);
                    return false;
                }
                if((result&CursorRecordResult.Accept)!=0){accepted++;}
                if((result&CursorRecordResult.RetryLastRecord)!=0){cursor=scan.CurrentAddress;}
                if((result&CursorRecordResult.EndBatch)!=0){
                    functions.OnStop(true,accepted);
                    return true;
                }
            }
            functions.OnStop(false,accepted);
            return false;
        }catch(Exception exception){
            functions.OnException(exception,accepted);
            throw;
        }
    }
}
