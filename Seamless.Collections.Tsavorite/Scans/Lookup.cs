namespace Seamless.Collections.Tsavorite.Scans;

/// <summary>最新レコードのアドレスを照合し、古い版と削除済みのキーを除いて逐次走査する。</summary>
internal static class Lookup{
    // ディスク上の行の確認読みを、この行数までまとめて出してから完了をまとめて待つ。
    private const int LivenessBatch=1024;
    // 確認読みのキーを写す固定領域の1かたまりの大きさ。足りなければ足す。
    private const int KeyArenaBytes=64*1024;

    internal static bool Next<TStoreFunctions,TAllocator,TScanFunctions>(
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
        // ネイティブの条件付き走査ではディスク上の衝突連鎖で生存キーが欠ける。
        // 通常のキー検索の完了アドレスで版を判定する(行のアドレスと一致すれば、その行が最新の版)。
        Dictionary<long,RecordMetadata>? live=null;List<byte[]>? arenas=null;
        try{
            while(true){
                var head=log.HeadAddress;
                if(cursor>=head){return InMemory(session,log,ref functions,ref cursor,untilAddress,ref accepted);}
                // ディスク上の範囲。確認読みは保留(ディスク読み)になるので、1件ずつ待たずに区切りの分を出し切り、まとめて完了を待つ。
                var limit=Math.Min(untilAddress,head);
                live??=new();arenas??=[];
                var batchEnd=Probe(session,log,cursor,limit,live,arenas);
                // 同じ範囲を読み直し、最新の版の行だけを渡す。
                using(var scan=log.Scan(cursor,batchEnd)){
                    while(scan.GetNext()){
                        cursor=scan.NextAddress;
                        if(scan.Info.Tombstone||!live.TryGetValue(scan.CurrentAddress,out var metadata)){continue;}
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
                }
                cursor=Math.Max(cursor,batchEnd);
                if(cursor>=untilAddress){
                    functions.OnStop(false,accepted);
                    return false;
                }
            }
        }catch(Exception exception){
            functions.OnException(exception,accepted);
            throw;
        }
    }

    // [start,limit) の先頭から最大 LivenessBatch 行について確認読みを出し、最新の版と分かったアドレスを live に入れる。
    // 確認した範囲の終わりのアドレスを返す。
    private static long Probe<TStoreFunctions,TAllocator>(
        ClientSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,ContainsDelete,TStoreFunctions,TAllocator> session,
        LogAccessor<TStoreFunctions,TAllocator> log,long start,long limit,Dictionary<long,RecordMetadata> live,List<byte[]> arenas)
        where TStoreFunctions:IStoreFunctions
        where TAllocator:IAllocator<TStoreFunctions>{
        live.Clear();
        var end=limit;var rows=0;var pending=false;var arena=0;var used=0;
        using(var probe=log.Scan(start,limit)){
            while(probe.GetNext()){
                if(!probe.Info.Tombstone){
                    // FixedSpanByteKey は「固定済み」と名乗るので、Tsavorite は保留の読みでキーを複製せず指し先だけを持つ
                    // (ConditionallyHoistedKey の Pinned)。走査の読み込み領域は次の行で上書きされるので、
                    // 完了を待つまで動かない固定領域へキーを写してから読みを出す。完了を待った後は同じ領域を使い回す。
                    var key=probe.Key;
                    if(arena==arenas.Count||used+key.Length>arenas[arena].Length){
                        if(arena<arenas.Count&&used>0){arena++;}
                        if(arena==arenas.Count||arenas[arena].Length<key.Length){arenas.Insert(arena,GC.AllocateUninitializedArray<byte>(Math.Max(KeyArenaBytes,key.Length),pinned:true));}
                        used=0;
                    }
                    var copy=arenas[arena].AsSpan(used,key.Length);key.CopyTo(copy);used+=key.Length;
                    var input=default(EqualityEmpty);
                    var output=false;
                    var options=default(ReadOptions);
                    var status=session.BasicContext.Read((FixedSpanByteKey)(ReadOnlySpan<byte>)copy,ref input,ref output,ref options,out var metadata);
                    if(status.IsPending){pending=true;}
                    else{
                        if(!status.IsCompletedSuccessfully){throw new IOException($"The liveness read failed: {status}.");}
                        if(status.Found){live.TryAdd(metadata.Address,metadata);}
                    }
                }
                if(++rows>=LivenessBatch){end=probe.NextAddress;break;}
            }
        }
        if(pending){
            using var cpu=global::Seamless.Execution.CpuScheduler.SuspendForIo();
            session.BasicContext.CompletePendingWithOutputs(out var completed,wait:true);
            try{
                while(completed.Next()){
                    var status=completed.Current.Status;
                    if(!status.IsCompletedSuccessfully){throw new IOException($"The liveness read failed: {status}.");}
                    if(status.Found){live.TryAdd(completed.Current.RecordMetadata.Address,completed.Current.RecordMetadata);}
                }
            }finally{completed.Dispose();}
        }
        return end;
    }

    // メモリ上の範囲。確認読みはほぼ待たずに済むので、1回の走査で1件ずつ確かめる(ディスクに落ちていた行だけ、その場で待つ)。
    private static unsafe bool InMemory<TStoreFunctions,TAllocator,TScanFunctions>(
        ClientSession<FixedSpanByteKey,EqualityEmpty,bool,EqualityEmpty,ContainsDelete,TStoreFunctions,TAllocator> session,
        LogAccessor<TStoreFunctions,TAllocator> log,ref TScanFunctions functions,ref long cursor,long untilAddress,ref long accepted)
        where TStoreFunctions:IStoreFunctions
        where TAllocator:IAllocator<TStoreFunctions>
        where TScanFunctions:IScanIteratorFunctions{
        using var scan=log.Scan(cursor,untilAddress);
        while(scan.GetNext()){
            cursor=scan.NextAddress;
            if(scan.Info.Tombstone){continue;}
            var input=default(EqualityEmpty);
            var output=false;
            var options=default(ReadOptions);
            RecordMetadata metadata;
            Status status;
            // 保留になった読みはキーの指し先を持ち続けるので、完了を待つまでキーを固定する。
            var key=scan.Key;
            fixed(byte* pointer=key){
                status=session.BasicContext.Read((FixedSpanByteKey)key,ref input,ref output,ref options,out metadata);
                if(status.IsPending){
                    using var cpu=global::Seamless.Execution.CpuScheduler.SuspendForIo();
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
    }
}
