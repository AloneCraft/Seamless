using System.Text.Json;

namespace Benchmark.Infrastructure;

// BGO protocol 1 の外部契約。wire の名前は BGO と同じ英語を維持する。
public sealed record ExternalTarget(string InstanceId,int Pid,long Start);
public sealed record ExternalRequest(string Operation,string Application,ExternalTarget External,string[]? Operations=null,string? Id=null,string? GrantToken=null);
public sealed record ExternalFileRequest(int Pid,long Start,ExternalRequest Request);
public sealed class ExternalReply{
    public bool Ok{get;set;}
    public string Error{get;set;}="";
    public ExternalTaskRecord[] Tasks{get;set;}=[];
    public ExternalTaskRecord? Task{get;set;}
}
public sealed class ExternalTaskRecord{
    public string Id{get;set;}="";
    public string Application{get;set;}="";
    public string Operation{get;set;}="";
    public string Status{get;set;}="";
    public string ArtifactDirectory{get;set;}="";
    public ExternalOperation? External{get;set;}
}
public sealed class ExternalOperation{
    public ExternalTarget? Target{get;set;}
    public string OperationId{get;set;}="";
    public string GrantToken{get;set;}="";
    public JsonElement Parameters{get;set;}
    public bool Claimed{get;set;}
    public bool CancellationRequested{get;set;}
}
public sealed record ExternalTerminalReceipt(int ProtocolVersion,string TaskId,string OperationId,string Application,ExternalTarget External,string GrantToken,long Sequence,string State,string ValidationOutcome,bool OperationCompleted,bool WorkersStopped,bool PersistenceCompleted,DateTimeOffset CompletedAt,string ResultRef);

public interface Iアプリ操作通信{
    ExternalTarget 接続先{get;}
    Task<ExternalReply> 送信Async(ExternalRequest 要求,CancellationToken 取消);
    Task 終端を保存Async(ExternalTaskRecord 操作,ExternalTerminalReceipt 終端);
}

public interface Iアプリ操作サービス{
    IReadOnlyList<string> 操作一覧{get;}
    // Dispatcher 上で型検証と手動操作との排他予約だけを行う。まだ処理を開始しない。
    Task<アプリ操作準備?> 準備Async(string 操作,JsonElement 設定);
}

public sealed record アプリ操作結果(string 状態,string 検証結果,string 結果参照,bool Worker停止済みか=true,bool 保存完了か=true);

public sealed class アプリ操作準備(IDisposable 予約,Func<CancellationToken,Task<アプリ操作結果>> 実行):IDisposable{
    public Task<アプリ操作結果> 実行Async(CancellationToken 取消)=>実行(取消);
    public void Dispose()=>予約.Dispose();
}

/// <summary>手動開始と claim 待ちを含む外部操作が同じ排他を共有する。</summary>
public sealed class アプリ操作排他{
    private int 予約済みか;
    private int 終了受付済みか;
    public bool 実行中か=>Volatile.Read(ref this.予約済みか)!=0;
    public IDisposable? 予約する(){
        if(Volatile.Read(ref this.終了受付済みか)!=0){return null;}
        if(Interlocked.CompareExchange(ref this.予約済みか,1,0)!=0){return null;}
        if(Volatile.Read(ref this.終了受付済みか)!=0){Volatile.Write(ref this.予約済みか,0);return null;}
        return new 予約(this);
    }
    public void 終了を受け付ける()=>Interlocked.Exchange(ref this.終了受付済みか,1);
    private sealed class 予約(アプリ操作排他 所有者):IDisposable{
        private アプリ操作排他? 所有=所有者;
        public void Dispose(){
            var 対象=Interlocked.Exchange(ref this.所有,null);
            if(対象 is not null){Volatile.Write(ref 対象.予約済みか,0);}
        }
    }
}

/// <summary>閉じる要求を一度だけ受け付け、drain完了後の再closeだけを通す。</summary>
public sealed class アプリ終了調停(Func<Task> 終了){
    private int 要求中か;
    public bool 終了可能か{get;private set;}
    public Exception? 最終障害{get;private set;}
    public async Task<bool> 終了を要求Async(){
        if(this.終了可能か||Interlocked.CompareExchange(ref this.要求中か,1,0)!=0){return false;}
        try{
            await 終了();
            this.終了可能か=true;
            return true;
        }catch(Exception 例外){
            this.最終障害=例外;
            Volatile.Write(ref this.要求中か,0);
            return false;
        }
    }
}
