using Seamless.Collections;
namespace Benchmark.TpcE.Core;
/// <summary>表への書き込みの種類。巻き戻しによる書き戻しも別の種類として通知する。</summary>
public enum WriteKind:byte{
    Insert=1,
    Update=2,
    Delete=3,
    UndoInsert=4,
    UndoUpdate=5,
    UndoDelete=6,
}
/// <summary>表へ書くたびに呼ばれる。計測の内側で呼ばれるので、受け取った値を積むだけにすること。</summary>
public interface IWriteObserver{
    void OnWrite(int tableId,WriteKind kind,object key,object? row);
}
/// <summary>行の変化(索引の更新に使う)。挿入は before が null、削除は after が null。</summary>
public delegate void TableChange<TRow>(in TRow? before,in TRow? after) where TRow:struct;
/// <summary>取引ごとの取り消し記録と書き込み通知の受け口。DB ごとに 1 つ。</summary>
public sealed class TxnLog{
    private readonly List<Action> undo=[];
    private bool recording;
    public IWriteObserver? Observer{get;set;}
    /// <summary>取引を始める。以後の書き込みを取り消し記録に積む。</summary>
    public void Begin(){
        if(this.recording){
            throw new InvalidOperationException("A transaction is already in progress.");
        }
        this.undo.Clear();
        this.recording=true;
    }
    /// <summary>取引を確定する。取り消し記録を捨てる。</summary>
    public void Commit(){
        this.EnsureRecording();
        this.undo.Clear();
        this.recording=false;
    }
    /// <summary>取引の書き込みを逆順に元へ戻す。戻す書き込みも通知する。</summary>
    public void Rollback(){
        this.EnsureRecording();
        this.recording=false;
        for(var index=this.undo.Count-1;index>=0;index--){
            this.undo[index]();
        }
        this.undo.Clear();
    }
    public bool IsRecording=>this.recording;
    internal void Push(Action restore){
        if(this.recording){
            this.undo.Add(restore);
        }
    }
    private void EnsureRecording(){
        if(!this.recording){
            throw new InvalidOperationException("No transaction is in progress.");
        }
    }
}
/// <summary>
/// 保存先に依らない表の口。突き合わせ・ダイジェスト・読み戻し・故障注入が使う(取引のコードは型付きの表を使う)。
/// </summary>
public interface ITableView{
    int TableId{get;}
    string Name{get;}
    long Count{get;}
    Type KeyType{get;}
    Type RowType{get;}
    bool TryGetBoxed(object key,out object? row);
    /// <summary>生きている行を順不同で列挙する。</summary>
    IEnumerable<object> EnumerateRowsBoxed();
    /// <summary>行から主キーを取り出す。</summary>
    object KeyOf(object row);
    /// <summary>故障注入用。索引も取り消し記録も通知も通さずに書く。</summary>
    void PutRawBoxed(object row);
    /// <summary>故障注入用。索引も取り消し記録も通知も通さずに消す。</summary>
    void DeleteRawBoxed(object key);
}
/// <summary>
/// 保存先ごとの表の共通部分。書き込みは必ずここを通り、索引の更新・取り消し記録・書き込み通知を行う。
/// 派生クラスは生の読み書きと、FLinq の起点になる具体型の <c>Rows</c> を持つ。
/// </summary>
public abstract class TableCore<TKey,TRow>:ITableView
    where TKey:unmanaged
    where TRow:struct,IRecord<TKey>{
    private readonly TxnLog log;
    private readonly List<TableChange<TRow>> hooks=[];
    protected TableCore(TxnLog log,int tableId,string name){
        this.log=log;
        this.TableId=tableId;
        this.Name=name;
    }
    public int TableId{get;}
    public string Name{get;}
    public abstract long Count{get;}
    public Type KeyType=>typeof(TKey);
    public Type RowType=>typeof(TRow);
    /// <summary>行の変化を受け取る処理(索引の更新)を足す。</summary>
    public void AddHook(TableChange<TRow> hook)=>this.hooks.Add(hook);
    public abstract bool TryGet(TKey key,out TRow row);
    public bool Contains(TKey key)=>this.TryGet(key,out _);
    /// <summary>主キーで 1 行を引く。無ければ <see cref="KeyNotFoundException"/>。</summary>
    public TRow Get(TKey key){
        if(!this.TryGet(key,out var row)){
            throw new KeyNotFoundException($"{this.Name}: key {key} was not found.");
        }
        return row;
    }
    /// <summary>主キーで 1 行を引く。無ければ null。</summary>
    public TRow? Find(TKey key){
        if(this.TryGet(key,out var row)){
            return row;
        }
        return null;
    }
    /// <summary>行を追加する。同じ主キーがあれば <see cref="InvalidOperationException"/>(主キー違反)。</summary>
    public void Insert(TRow row){
        var key=row.Key;
        if(this.TryGet(key,out _)){
            throw new InvalidOperationException($"{this.Name}: duplicate key {key}.");
        }
        this.InsertRaw(key,row);
        this.RaiseChanged(null,row);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.Insert,key,row);
        this.log.Push(()=>this.UndoInsert(key,row));
    }
    /// <summary>行を置き換える。主キーが無ければ <see cref="KeyNotFoundException"/>。</summary>
    public void Update(TRow row){
        var key=row.Key;
        var before=this.Get(key);
        this.ReplaceRaw(key,row);
        this.RaiseChanged(before,row);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.Update,key,row);
        this.log.Push(()=>this.UndoUpdate(key,before,row));
    }
    /// <summary>行を消す。主キーが無ければ <see cref="KeyNotFoundException"/>。</summary>
    public void Delete(TKey key){
        var before=this.Get(key);
        this.DeleteRaw(key);
        this.RaiseChanged(before,null);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.Delete,key,null);
        this.log.Push(()=>this.UndoDelete(key,before));
    }
    /// <summary>初期投入。主キー違反は確かめず、取り消し記録も通知もしない(索引は更新する)。</summary>
    public void Load(TRow row){
        this.InsertRaw(row.Key,row);
        this.RaiseChanged(null,row);
    }
    /// <summary>生きている行を順不同で列挙する(ダイジェスト用。LINQ は使わない)。</summary>
    public abstract IEnumerable<TRow> EnumerateRows();
    protected abstract void InsertRaw(TKey key,TRow row);
    protected abstract void ReplaceRaw(TKey key,TRow row);
    protected abstract void DeleteRaw(TKey key);
    private void UndoInsert(TKey key,TRow row){
        this.DeleteRaw(key);
        this.RaiseChanged(row,null);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.UndoInsert,key,null);
    }
    private void UndoUpdate(TKey key,TRow before,TRow after){
        this.ReplaceRaw(key,before);
        this.RaiseChanged(after,before);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.UndoUpdate,key,before);
    }
    private void UndoDelete(TKey key,TRow before){
        this.InsertRaw(key,before);
        this.RaiseChanged(null,before);
        this.log.Observer?.OnWrite(this.TableId,WriteKind.UndoDelete,key,before);
    }
    private void RaiseChanged(TRow? before,TRow? after){
        foreach(var hook in this.hooks){
            hook(before,after);
        }
    }
    bool ITableView.TryGetBoxed(object key,out object? row){
        if(this.TryGet((TKey)key,out var found)){
            row=found;
            return true;
        }
        row=null;
        return false;
    }
    IEnumerable<object> ITableView.EnumerateRowsBoxed(){
        foreach(var row in this.EnumerateRows()){
            yield return row;
        }
    }
    object ITableView.KeyOf(object row)=>((TRow)row).Key;
    void ITableView.PutRawBoxed(object row){
        var typed=(TRow)row;
        if(this.TryGet(typed.Key,out _)){
            this.ReplaceRaw(typed.Key,typed);
        }else{
            this.InsertRaw(typed.Key,typed);
        }
    }
    void ITableView.DeleteRawBoxed(object key){
        var typed=(TKey)key;
        if(this.TryGet(typed,out _)){
            this.DeleteRaw(typed);
        }
    }
}
