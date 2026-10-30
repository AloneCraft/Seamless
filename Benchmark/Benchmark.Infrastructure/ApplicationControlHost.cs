using System.Collections.Concurrent;
using System.Text.Json;

namespace Benchmark.Infrastructure;

public sealed class アプリ操作ホスト(string アプリ名,Iアプリ操作通信 通信,Iアプリ操作サービス サービス){
    private readonly Dictionary<string,進行操作> 操作一覧=new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string,進行操作> 終端待ち一覧=new(StringComparer.Ordinal);
    private readonly SemaphoreSlim 終端保存排他=new(1,1);
    private int 終了中か;
    public Exception? 最終障害{get;private set;}
    public bool 終了可能か=>this.操作一覧.Values.All(操作=>操作.後処理確認済みか&&操作.終端保存済みか);

    public async Task 実行Async(CancellationToken 終了){
        using var 終了登録=終了.Register(()=>Interlocked.Exchange(ref this.終了中か,1));
        var 登録時刻=DateTimeOffset.MinValue;
        try{
            while(!終了.IsCancellationRequested){
                try{
                    if(DateTimeOffset.UtcNow-登録時刻>TimeSpan.FromSeconds(5)){
                        var 登録=await 通信.送信Async(new("external-register",アプリ名,通信.接続先,サービス.操作一覧.ToArray()),終了).ConfigureAwait(false);
                        if(!登録.Ok){throw new InvalidDataException(登録.Error);}
                        登録時刻=DateTimeOffset.UtcNow;
                    }
                    var 応答=await 通信.送信Async(new("external-poll",アプリ名,通信.接続先),終了).ConfigureAwait(false);
                    if(!応答.Ok){throw new InvalidDataException(応答.Error);}
                    await this.受信を適用Async(応答.Tasks).ConfigureAwait(false);
                }catch(OperationCanceledException) when(終了.IsCancellationRequested){break;}
                catch(Exception 例外){this.最終障害=例外;this.取消を要求する();}
                await this.終端を再公開Async().ConfigureAwait(false);
                await Task.Delay(200,終了).ConfigureAwait(false);
            }
        }catch(OperationCanceledException) when(終了.IsCancellationRequested){
        }finally{
            Interlocked.Exchange(ref this.終了中か,1);
            this.取消を要求する();
            await this.実行完了を待つAsync().ConfigureAwait(false);
            // 完成済みreceiptが保存できるまで、正常なアプリ終了も延期する。
            while(this.終端待ち一覧.Values.Any(操作=>操作.再公開可能か)){
                await this.終端を再公開Async().ConfigureAwait(false);
                if(this.終端待ち一覧.Values.Any(操作=>操作.再公開可能か)){await Task.Delay(200).ConfigureAwait(false);}
            }
        }
    }

    public async Task 受信を適用Async(IReadOnlyList<ExternalTaskRecord> 受信一覧){
        foreach(var 操作 in 受信一覧){
            if(!this.束縛が正しいか(操作)){continue;}
            if(this.操作一覧.TryGetValue(操作.Id,out var 進行)){
                if(操作.External!.CancellationRequested||操作.Status is "Cancelling" or "Quarantined"){進行.取消.Cancel();}
                continue;
            }
            if(操作.External!.Claimed||操作.Status is not ("Reserved" or "Cancelling" or "Quarantined")){continue;}
            // claim要求より先に記録する。応答不明でも同じIDを再び開始しない。
            var 新規=new 進行操作(操作);
            this.操作一覧.Add(操作.Id,新規);
            if(Volatile.Read(ref this.終了中か)!=0||操作.External.CancellationRequested||操作.Status!="Reserved"){
                await this.未開始を確定Async(新規).ConfigureAwait(false);continue;
            }
            アプリ操作準備? 準備=null;
            try{
                if(サービス.操作一覧.Contains(操作.Operation,StringComparer.Ordinal)){
                    準備=await サービス.準備Async(操作.Operation,操作.External.Parameters).ConfigureAwait(false);
                }
            }catch(Exception 例外) when(例外 is ArgumentException or InvalidDataException or JsonException){this.最終障害=例外;}
            新規.準備=準備;
            if(準備 is null||Volatile.Read(ref this.終了中か)!=0){
                await this.未開始を確定Async(新規).ConfigureAwait(false);continue;
            }
            ExternalReply claim;
            try{
                claim=await 通信.送信Async(new("external-claim",アプリ名,通信.接続先,Id:操作.Id,GrantToken:操作.External.GrantToken),CancellationToken.None).ConfigureAwait(false);
            }catch(Exception 例外){
                this.最終障害=例外;
                await this.未開始を確定Async(新規).ConfigureAwait(false);continue;
            }
            var 許可=claim.Task;
            if(!claim.Ok||許可 is null||!this.束縛が正しいか(許可)||許可.Id!=操作.Id||許可.External!.GrantToken!=操作.External.GrantToken||!許可.External.Claimed||許可.Status!="Running"||許可.External.CancellationRequested||Volatile.Read(ref this.終了中か)!=0){
                this.最終障害=new InvalidDataException("BGO claim の実行許可を確認できません。");
                await this.未開始を確定Async(新規).ConfigureAwait(false);continue;
            }
            新規.実行=this.開始して終端を保存Async(新規);
        }
        await this.終端を再公開Async().ConfigureAwait(false);
    }

    public Task 実行完了を待つAsync()=>Task.WhenAll(this.操作一覧.Values.Select(操作=>操作.実行));

    public async Task 終端を再公開Async(){
        await this.終端保存排他.WaitAsync().ConfigureAwait(false);
        try{
            foreach(var 操作 in this.終端待ち一覧.Values){
                if(!操作.再公開可能か){continue;}
                try{
                    await 通信.終端を保存Async(操作.要求,操作.終端!).ConfigureAwait(false);
                    操作.終端保存済みか=true;
                    _=this.終端待ち一覧.TryRemove(操作.要求.Id,out _);
                    操作.準備?.Dispose();
                }catch(Exception 例外){
                    this.最終障害=例外;
                    // 異内容receiptなど明示的な不正は上書きも再試行もしない。
                    if(例外 is not (IOException or UnauthorizedAccessException)){操作.再公開可能か=false;}
                }
            }
        }finally{this.終端保存排他.Release();}
    }

    private Task 未開始を確定Async(進行操作 操作){
        // claimの返答に依存しない停止証拠: serviceの呼出し自体をしていない。
        操作.後処理確認済みか=true;
        return this.終端を保存Async(操作,new("Rejected","Unavailable",""));
    }

    private void 取消を要求する(){
        foreach(var 操作 in this.操作一覧.Values){if(!操作.実行.IsCompleted){操作.取消.Cancel();}}
    }

    private async Task 開始して終端を保存Async(進行操作 操作){
        try{
            var 結果=await 操作.準備!.実行Async(操作.取消.Token).ConfigureAwait(false);
            if(!結果.Worker停止済みか||!結果.保存完了か){throw new InvalidDataException("cleanup / 保存完了の証拠がありません。");}
            操作.後処理確認済みか=true;
            if(操作.取消.IsCancellationRequested){結果=結果 with{状態="Cancelled"};}
            if(結果.状態=="Cancelled"&&結果.検証結果=="Passed"){結果=結果 with{検証結果="Unavailable"};}
            await this.終端を保存Async(操作,結果).ConfigureAwait(false);
        }catch(Exception 例外){
            // cleanup / 保存完了不明ならreceiptを作らず、local gateとbroker所有権を保持する。
            this.最終障害=例外;
        }
    }

    private Task 終端を保存Async(進行操作 操作,アプリ操作結果 結果){
        if(結果.状態 is not ("Completed" or "Cancelled" or "Failed" or "Rejected")||結果.検証結果 is not ("Passed" or "Failed" or "Unavailable" or "Inconclusive")){
            throw new InvalidDataException("未知の終端状態です。");
        }
        // CompletedAtを含め一度だけ作る。再接続時は同じ完成済みreceiptだけ再公開する。
        操作.終端=new(1,操作.要求.Id,操作.要求.Id,アプリ名,通信.接続先,操作.要求.External!.GrantToken,1,結果.状態,結果.検証結果,true,結果.Worker停止済みか,結果.保存完了か,DateTimeOffset.UtcNow,結果.結果参照);
        this.終端待ち一覧.TryAdd(操作.要求.Id,操作);
        return this.終端を再公開Async();
    }

    private bool 束縛が正しいか(ExternalTaskRecord 操作)=>Guid.TryParseExact(操作.Id,"N",out _)&&操作.Application==アプリ名&&操作.External is{} 外部&&外部.OperationId==操作.Id&&外部.Target==通信.接続先&&外部.GrantToken.Length==64&&外部.GrantToken.All(Uri.IsHexDigit)&&Path.IsPathFullyQualified(操作.ArtifactDirectory);

    private sealed class 進行操作(ExternalTaskRecord 受信要求){
        internal readonly ExternalTaskRecord 要求=受信要求;
        internal readonly CancellationTokenSource 取消=new();
        internal Task 実行=Task.CompletedTask;
        internal アプリ操作準備? 準備;
        internal ExternalTerminalReceipt? 終端;
        internal bool 後処理確認済みか;
        internal bool 終端保存済みか;
        internal bool 再公開可能か=true;
    }
}
