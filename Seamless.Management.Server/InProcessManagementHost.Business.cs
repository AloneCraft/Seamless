namespace Seamless.Management.Server;

public sealed partial class InProcessManagementHost{
    private readonly object resultGate=new();
    private readonly HashSet<IDisposable> results=[];
    /// <summary>結果コピーを構築して取引を確定し、一度だけ読める所有型ストリームを返す。</summary>
    public QueryResultStream<TResult> ExecuteStream<TResult>(ExecuteRequest request,CancellationToken cancellationToken=default,long memoryLimitBytes=16L<<20,long temporaryStorageLimitBytes=16L<<30){
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(memoryLimitBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(temporaryStorageLimitBytes);
        var frozen=request with{Arguments=request.Arguments.Clone()};
        return this.Invoke((service,user)=>{
            var result=new QueryResultStream<TResult>(new(){ParentBudget=this.store!.ResourceBudget,MemoryLimitBytes=memoryLimitBytes,TemporaryStorageLimitBytes=temporaryStorageLimitBytes,TemporaryDirectory=Path.Combine(this.store.Root,"temporary","results"),MaximumQueryWorkers=1,CancellationToken=cancellationToken},this.ReleaseResult);
            try{
                foreach(var row in service.Execute(user,frozen,cancellationToken,result.Seal)){result.Add(row.Value);}
                lock(this.resultGate){this.results.Add(result);}
                return result;
            }catch{result.Dispose();throw;}
        },cancellationToken);
    }
    private void ReleaseResult(IDisposable result){lock(this.resultGate){this.results.Remove(result);}}
    private void CloseResults(){
        IDisposable[] pending;lock(this.resultGate){pending=this.results.ToArray();this.results.Clear();}
        List<Exception>? errors=null;
        foreach(var result in pending){try{result.Dispose();}catch(Exception error){(errors??=[]).Add(error);}}
        if(errors is not null){throw new AggregateException("結果ストリームの解放に失敗しました。",errors);}
    }
    /// <summary>公開クエリを同じDB実行スレッドで実行し、戻り値の検査と取引確定後に返す。</summary>
    public TResult ExecuteScalar<TResult>(ExecuteRequest request,CancellationToken cancellationToken=default){
        ArgumentNullException.ThrowIfNull(request);
        if(request.MaximumRows is <1 or >10000){throw new ManagementException("Validation","結果上限は1〜10000で指定してください。");}
        var frozen=request with{Arguments=request.Arguments.Clone(),MaximumRows=2};
        return this.Invoke((service,user)=>{
            cancellationToken.ThrowIfCancellationRequested();var count=0;TResult result=default!;
            void Validate(){if(count!=1){throw new ManagementException("ResultShape","単一値の問い合わせには結果が1件必要です。");}}
            foreach(var row in service.Execute(user,frozen,cancellationToken,Validate)){
                if(count!=0){throw new ManagementException("ResultShape","複数の結果を単一値として返せません。");}
                result=(TResult)RuntimeValueJson.Deserialize(row.Value,typeof(TResult))!;count++;
            }
            return result;
        },cancellationToken);
    }
    public ObjectExecutionResult ExecuteObject(ObjectExecutionRequest request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.Invoke((service,user)=>service.ExecuteObjectAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken);
    }
    public TableRows ReadRows(TableRowsRequest request,CancellationToken cancellationToken=default){
        var frozen=request.Freeze();return this.Invoke((service,user)=>service.ReadRowsAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken);
    }
    public TableRowCommit CommitRows(TableRowBatch batch,CancellationToken cancellationToken=default){
        var frozen=batch.Freeze();return this.Invoke((service,user)=>service.CommitRowsAsync(user,frozen,cancellationToken).GetAwaiter().GetResult(),cancellationToken);
    }
}
