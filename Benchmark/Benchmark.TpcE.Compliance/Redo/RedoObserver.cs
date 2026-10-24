using Benchmark.TpcE.Core;
namespace Benchmark.TpcE.Compliance;
/// <summary>取引中の書き込みを保存形式で積み、取引が終わったら <see cref="Seal"/> で 1 件の記録にする。</summary>
internal sealed class RedoObserver:IWriteObserver{
    private readonly List<RedoEntry> entries=[];
    private readonly Dictionary<int,ITableView> tables=[];
    public RedoObserver(TpcEDatabaseBase database){
        foreach(var table in database.Tables){
            this.tables.Add(table.TableId,table);
        }
    }
    public void OnWrite(int tableId,WriteKind kind,object key,object? row){
        var table=this.tables[tableId];
        var keyBytes=table.EncodeKey(key);
        byte[]? rowBytes=null;
        if(row is not null){
            rowBytes=table.EncodeRow(row);
        }
        this.entries.Add(new RedoEntry(tableId,kind,keyBytes,rowBytes));
    }
    public int Pending=>this.entries.Count;
    /// <summary>積んだ書き込みを取引の記録にして、積み直しに備える。書き込みが無い取引は null。</summary>
    public RedoTransaction? Seal(long sequence){
        if(this.entries.Count==0){
            return null;
        }
        var record=new RedoTransaction(sequence,this.entries.ToArray());
        this.entries.Clear();
        return record;
    }
    public void Discard()=>this.entries.Clear();
}
