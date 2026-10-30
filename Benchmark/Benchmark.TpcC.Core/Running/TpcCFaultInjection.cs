using System.Globalization;
namespace Benchmark.TpcC.Core;
/// <summary>
/// 故障注入(検出できるかの確認用)。書式は ENGINE:TXN:KIND(例 PF:500:Output)。ENGINE は組み合わせの略称(シャドーは TFp1)。ライブラリには手を入れず、ハーネス側で壊す。
/// Output=出力の RolledBack を反転する(L1 で検出)、DropWrite=書いた行を生で消す(L3 で検出)、
/// RouteWrong=書いた行を別の区画の同じ表へも書く(区画が 2 つ以上のとき。状態の比較で検出)、
/// SkipUndo=巻き戻した取引で、戻したはずの行を残す(状態の比較で検出)、
/// ReopenTamper=TXN 以降の最初の開き直しの直後に行を 1 つ生で消す(開き直しの比較で検出)。
/// </summary>
public static class TpcCFaultInjection{
    public sealed record Spec(string Engine,long TxnIndex,string Kind);
    private static readonly string[] Kinds=["Output","DropWrite","RouteWrong","SkipUndo","ReopenTamper"];
    public static Spec? Parse(string? text){
        if(string.IsNullOrWhiteSpace(text)){
            return null;
        }
        var parts=text.Split(':');
        if(parts.Length!=3){
            throw new ArgumentException($"--inject-fault must be ENGINE:TXN:KIND, but was {text}.");
        }
        if(Array.IndexOf(Kinds,parts[2])<0){
            throw new ArgumentException($"Unknown fault kind {parts[2]}. Use {string.Join(", ",Kinds)}.");
        }
        var txn=long.Parse(parts[1],CultureInfo.InvariantCulture);
        if(txn<=0||parts[0].ToUpperInvariant() is not ("TL" or "TF" or "PL" or "PF" or "TLP1" or "TFP1" or "PLP1" or "PFP1")){
            throw new ArgumentException("Fault injection needs a known engine and a positive transaction index.");
        }
        return new(parts[0].ToUpperInvariant(),txn,parts[2]);
    }
    internal static TpcCResult AfterTransaction(string? text,TpcCVariantSession session,long txnIndex,TpcCResult result){
        var spec=Parse(text);
        if(spec is null||!string.Equals(spec.Engine,session.ShortName,StringComparison.OrdinalIgnoreCase)||spec.TxnIndex!=txnIndex){
            return result;
        }
        var partitions=session.RequireEngine.Database.PartitionBases;
        if(spec.Kind=="Output"){
            return result with{RolledBack=!result.RolledBack};
        }
        if(spec.Kind=="DropWrite"){
            foreach(var record in session.Records){
                if(record.Kind is WriteKind.Insert or WriteKind.Update){
                    partitions[record.Partition].Tables[record.TableId-1].DeleteRawBoxed(record.Key);
                    break;
                }
            }
        }else if(spec.Kind=="RouteWrong"&&partitions.Count>1){
            foreach(var record in session.Records){
                if(record.Kind is WriteKind.Insert or WriteKind.Update){
                    partitions[(record.Partition+1)%partitions.Count].Tables[record.TableId-1].PutRawBoxed(record.Row!);
                    break;
                }
            }
        }else if(spec.Kind=="SkipUndo"&&result.RolledBack){
            foreach(var record in session.Records){
                if(record.Kind==WriteKind.Insert){
                    partitions[record.Partition].Tables[record.TableId-1].PutRawBoxed(record.Row!);
                    break;
                }
            }
        }
        return result;
    }
    internal static void AfterReopen(string? text,TpcCVariantSession session,long txnIndex){
        var spec=Parse(text);
        if(spec is null||spec.Kind!="ReopenTamper"||!string.Equals(spec.Engine,session.ShortName,StringComparison.OrdinalIgnoreCase)||txnIndex<spec.TxnIndex){
            return;
        }
        if(session.ReopenTampered){
            return;
        }
        session.ReopenTampered=true;
        var table=session.RequireEngine.Database.PartitionBases[0].Tables[0];
        foreach(var row in table.EnumerateRowsBoxed()){
            table.DeleteRawBoxed(table.KeyOf(row));
            break;
        }
    }
}
