using System.Globalization;
namespace Benchmark.TpcE.Core;
/// <summary>
/// 故障注入(検出できるかの確認用)。書式は ENGINE:TXN:KIND(例 PF:500:Output)。ライブラリには手を入れず、ハーネス側で壊す。
/// Output=出力の status をずらす(L1 で検出)、DropWrite=書いた行を生で消す(L3 で検出)、
/// ReopenTamper=TXN 以降の最初の開き直しの直後に行を 1 つ生で消す(開き直しの比較で検出)。
/// </summary>
public static class FaultInjection{
    public sealed record Spec(string Engine,long TxnIndex,string Kind);
    public static Spec? Parse(string? text){
        if(string.IsNullOrWhiteSpace(text)){
            return null;
        }
        var parts=text.Split(':');
        if(parts.Length!=3){
            throw new ArgumentException($"--inject-fault must be ENGINE:TXN:KIND, but was {text}.");
        }
        var kind=parts[2];
        if(kind is not ("Output" or "DropWrite" or "ReopenTamper")){
            throw new ArgumentException($"Unknown fault kind {kind}. Use Output, DropWrite or ReopenTamper.");
        }
        return new(parts[0].ToUpperInvariant(),long.Parse(parts[1],CultureInfo.InvariantCulture),kind);
    }
    internal static TxnResult AfterTransaction(string? text,VariantSession session,long txnIndex,TxnResult result){
        var spec=Parse(text);
        if(spec is null||spec.Engine!=session.Variant.ShortName||spec.TxnIndex!=txnIndex){
            return result;
        }
        if(spec.Kind=="Output"){
            return result with{Status=result.Status+1};
        }
        if(spec.Kind=="DropWrite"){
            foreach(var record in session.Recorder.Records){
                if(record.Kind is WriteKind.Insert or WriteKind.Update){
                    session.RequireEngine.Database.Tables[record.TableId-1].DeleteRawBoxed(record.Key);
                    break;
                }
            }
        }
        return result;
    }
    private static readonly HashSet<string> tampered=[];
    internal static void AfterReopen(string? text,VariantSession session,long txnIndex){
        var spec=Parse(text);
        if(spec is null||spec.Kind!="ReopenTamper"||spec.Engine!=session.Variant.ShortName||txnIndex<spec.TxnIndex){
            return;
        }
        lock(tampered){
            if(!tampered.Add(session.Directory)){
                return;
            }
        }
        var table=session.RequireEngine.Database.Tables[0];
        foreach(var row in table.EnumerateRowsBoxed()){
            table.DeleteRawBoxed(table.KeyOf(row));
            break;
        }
    }
}
