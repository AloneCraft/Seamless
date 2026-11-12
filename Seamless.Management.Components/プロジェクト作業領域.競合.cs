namespace Seamless.Management.Components;

public partial class プロジェクト作業領域{
    private Task 競合を確認時Async(ソース文書 文書)=>this.実行Async(async()=>{
        var 結果=await this.操作.InspectSourceConflictAsync(new(文書.情報.Id,文書.現在),this.寿命.Token);
        文書.競合=結果;文書.同期停止か=結果.Conflict is not null;
        if(結果.Conflict is null){文書.競合ありか=false;this.案内=表示文言.取得("未解決のC#同期の競合はありません。同期して最新内容を確認できます。");}
        else{this.案内=表示文言.取得("両側の内容を保持しています。保存する内容を選択してください。");}
    });
    private Task 競合を保存時Async(ソース文書 文書,Dictionary<string,string?> ファイル)=>this.実行Async(async()=>{
        var 競合=文書.競合?.Conflict??throw new ManagementException("Conflict","競合を確認し直してください。");
        var 結果=await this.操作.ResolveSourceConflictAsync(new(文書.情報.Id,競合.Id,競合.DiskHash,競合.ManagerHash,文書.現在,ファイル),this.寿命.Token);
        文書.情報=結果.Document;文書.基準=結果.Document.Source!;文書.現在=結果.Document.Source!;文書.競合=null;文書.競合ありか=false;文書.同期停止か=false;
        if(!文書.現在.Files.Any(ファイル=>ファイル.Path==文書.ファイル)){文書.ファイル=文書.現在.Files.First().Path;}
        this.案内=表示文言.取得("競合を解消してファイルへ保存しました。DB下書きへの反映・公開は別の操作です。");
        if(結果.EditorBackup is not null){this.案内+=表示文言.取得(" 追加の入力の退避先 · ")+結果.EditorBackup;}
    });
}
