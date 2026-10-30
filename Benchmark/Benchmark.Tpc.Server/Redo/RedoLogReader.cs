using Benchmark.Tpc.Core;
namespace Benchmark.Tpc.Server;
/// <summary>再実行ログの読み手と、復旧時の再適用。</summary>
public static class RedoLogReader{
    /// <summary>有効な記録を順に返す。切れた、または壊れた記録で止め、そこまでの長さを <paramref name="validLength"/> に返す。</summary>
    public static IEnumerable<RedoTransaction> Read(string path,Action<long> validLength){
        if(!File.Exists(path)){
            validLength(0);
            yield break;
        }
        byte[] data;
        using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
            data=new byte[file.Length];
            file.ReadExactly(data);
        }
        var offset=0;
        while(RedoLogFormat.TryDecode(data.AsSpan(offset),out var transaction,out var consumed)){
            offset+=consumed;
            yield return transaction!;
        }
        validLength(offset);
    }
    /// <summary>通し番号が <paramref name="afterSequence"/> より後の記録だけを、ログの順に当てる。索引も更新される。</summary>
    public static RedoRecovery Replay(TpcDatabaseBase database,string path,long afterSequence){
        var tables=new Dictionary<int,ITableView>();
        foreach(var table in database.Tables){
            tables.Add(table.TableId,table);
        }
        long valid=0;
        var last=afterSequence;
        long applied=0;
        foreach(var transaction in Read(path,length=>valid=length)){
            if(transaction.Sequence<=afterSequence){
                continue;
            }
            foreach(var entry in transaction.Entries){
                var table=tables[entry.TableId];
                var key=table.DecodeKey(entry.Key);
                object? row=null;
                if(entry.Row is not null){
                    row=table.DecodeRow(entry.Row);
                }
                table.ApplyRedoBoxed(entry.Kind,key,row);
            }
            last=transaction.Sequence;
            applied++;
        }
        return new RedoRecovery(valid,last,applied);
    }
}
/// <summary>再適用の結果。<see cref="ValidLength"/> までが有効なログで、書き手はここから足す。</summary>
public readonly record struct RedoRecovery(long ValidLength,long LastSequence,long AppliedTransactions);
