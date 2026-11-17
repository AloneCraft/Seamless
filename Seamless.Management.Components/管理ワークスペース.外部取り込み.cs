namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    // 接続文字列の履歴の鍵。画面の表示状態と同じ「インスタンス・利用者」ごと(外部DBの取り込みの決定2)。表示状態を使えないときは履歴を使わない。
    private string? 外部接続履歴キー=>this.表示状態キー?.Replace("Seamless.Manager.Sidebar.v1.","Seamless.Manager.ExternalConnections.v1.",StringComparison.Ordinal);
    private void 外部取り込み開始時(){
        if(!this.DB作成できるか||!this.外部取り込みできるか){this.案内=表示文言.取得("外部DBの取り込みには、DB作成の権限と、OS の所有者として開いた Manager が必要です。");this.エラーか=true;return;}
        this.外部取り込み表示か=true;
    }
    private Task 外部取り込み完了時Async(ExternalImportReport 報告)=>this.処理Async(async()=>{
        this.外部取り込み表示か=false;
        await this.更新Async();
        if(this.DB一覧.FirstOrDefault(DB=>DB.DefinitionId==報告.DefinitionId) is{} DB){await this.DBを開くAsync(DB.Id);}
        this.案内=表示文言.取得("外部DBを「{0}」として取り込みました。",報告.Database);
    });
}
