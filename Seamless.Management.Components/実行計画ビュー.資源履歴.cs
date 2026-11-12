using Seamless.Management;

namespace Seamless.Management.Components;

public partial class 実行計画ビュー{
    private ResourceHeatTrace? 保存資源記録;
    private ResourceHeatReadResult? 資源履歴状態;
    private ResourceHeatQuery? 前資源要求;
    private CancellationTokenSource? 資源読込取消;
    private long 資源読込世代;
    private bool 資源読込中か,資源閲覧拒否か;
    private string? 資源履歴案内;
    private async Task 可視化選択時Async(string 方式){
        this.表示選択時(方式);
        if(this.描画方式==計画描画方式.Heat){await this.資源履歴を読むAsync();}
    }
    private void 資源読込を止める(){
        if(this.資源読込中か){this.前資源要求=null;}
        this.資源読込世代++;this.資源読込取消?.Cancel();this.資源読込取消?.Dispose();this.資源読込取消=null;this.資源読込中か=false;
    }
    private async Task 資源履歴を読むAsync(bool 強制か=false){
        if(!this.表示中か||this.描画方式!=計画描画方式.Heat||this.取得結果 is not{FromDraft:false} 対象||this.現在実行ID is not{} 実行ID){return;}
        var 要求=new ResourceHeatQuery(対象.DefinitionId,対象.Selection.ObjectId,実行ID,対象.Version,対象.ArtifactHash);
        if(!強制か&&this.前資源要求==要求){return;}
        this.資源読込を止める();this.資源読込取消=new();var 取消=this.資源読込取消.Token;var 世代=this.資源読込世代;
        this.資源読込中か=true;this.前資源要求=要求;this.資源履歴案内=null;
        try{
            var 結果=await this.管理操作.QueryResourceHeatAsync(要求,取消).WaitAsync(取消);
            if(世代!=this.資源読込世代||実行ID!=this.現在実行ID||!ReferenceEquals(対象,this.取得結果)){return;}
            this.資源履歴状態=結果;this.資源閲覧拒否か=false;
            if(結果.Trace is{} 記録){
                記録.Validate();
                if(!記録.IdentityResolved||記録.ExecutionId!=実行ID||記録.DefinitionId!=要求.DefinitionId||記録.ObjectId!=要求.ObjectId||記録.Version!=対象.Version||記録.ArtifactHash!=対象.ArtifactHash){throw new InvalidDataException("資源履歴の対象・版・実行IDが一致しません。");}
                this.保存資源記録=記録;
            }else{this.保存資源記録=null;this.資源履歴案内=結果.UnavailableReason;}
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){}
        catch(Exception 例外){
            if(世代!=this.資源読込世代){return;}
            this.保存資源記録=null;this.資源履歴案内=例外.Message;
            if(例外 is ManagementException{Code:"Permission" or "Authentication"}){this.資源閲覧拒否か=true;}
        }finally{if(世代==this.資源読込世代){this.資源読込中か=false;}}
    }
    private ResourceHeatTrace? 適合保存資源{get{
        if(this.資源閲覧拒否か){return null;}
        if(this.取得結果 is{FromDraft:false} 対象&&this.保存資源記録 is{} 記録&&記録.ExecutionId==this.現在実行ID&&記録.DefinitionId==対象.DefinitionId&&記録.ObjectId==対象.Selection.ObjectId&&記録.Version==対象.Version&&記録.ArtifactHash==対象.ArtifactHash){return 記録;}
        return null;
    }}
}
