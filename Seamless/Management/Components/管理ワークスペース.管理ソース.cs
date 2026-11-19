using Seamless.Management.Server;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool 管理ソースを開けるか=>this.管理操作 is IManagedDatabaseSourceOperations{CanOpenManagedDatabaseSource:true};
    private Task? 管理ソース監視;
    private readonly HashSet<string> 開いた管理ソース=new(StringComparer.Ordinal);

    private Task 管理ソースを開くAsync(文書状態 文書)=>this.処理Async(async()=>{
        this.ツリーの入力を確認(文書);
        await this.定義編集を同期Async(文書);
        if(文書.未保管か){await this.下書き保管本体Async(文書);}
        await this.管理ソースの下書きを読むAsync(文書);
        var ファイル=await ((IManagedDatabaseSourceOperations)this.管理操作).OpenManagedDatabaseSourceAsync(文書.定義.Id,this.寿命.Token);
        this.開いた管理ソース.Add(文書.DB.Id);
        this.管理ソース監視??=this.管理ソース監視Async(this.寿命.Token);
        this.案内="VS Codeで開きました · "+ファイル;
    });

    private async Task<bool> 管理ソースの下書きを読むAsync(文書状態 文書){
        if(文書.未保管か||文書.未確定操作 is not null||文書.DB変更 is{Delete:true}||
            this.対象文書一覧.Values.Any(対象=>対象.定義ID==文書.定義.Id&&対象.編集中か)){return false;}
        var 版=文書.下書き版;
        var 下書き=await this.管理操作.FindDraftAsync(文書.定義.Id,this.寿命.Token);
        if(下書き is null||下書き.Revision<=版||文書.下書き版!=版||文書.未保管か||文書.未確定操作 is not null||
            this.対象文書一覧.Values.Any(対象=>対象.定義ID==文書.定義.Id&&対象.編集中か)){return false;}
        文書.管理ソースの下書きを反映(下書き);
        return true;
    }

    private async Task 管理ソース監視Async(CancellationToken 取消){
        using var 時計=new PeriodicTimer(TimeSpan.FromSeconds(2));
        try{
            while(await 時計.WaitForNextTickAsync(取消)){
                await this.InvokeAsync(async()=>{
                    if(this.操作中か||!this.認証有効か){return;}
                    foreach(var ID in this.開いた管理ソース.ToArray()){
                        if(!this.文書一覧.TryGetValue(ID,out var 文書)){continue;}
                        try{
                            if(await this.管理ソースの下書きを読むAsync(文書)){
                                this.案内="VS Codeで保存した変更を下書きに反映しました。";this.エラーか=false;this.StateHasChanged();
                            }
                        }catch(IOException){/* エディターが保存中のときは次の読み込みで取り込む。 */}
                        catch(ManagementException 例外){
                            this.案内=例外.Message;this.エラーか=true;
                            if(例外.Code=="Authentication"){this.認証有効か=false;}
                            this.StateHasChanged();
                        }
                    }
                });
            }
        }catch(OperationCanceledException) when(取消.IsCancellationRequested){}
    }
}
