namespace Seamless.Management.Components;

public partial class プロジェクト作業領域{
    private bool 構成変更中か;
    private Task 構成変更時Async(ソース文書 文書,string 構成)=>this.実行Async(async()=>{
        if(構成==文書.情報.Configuration){return;}
        this.構成変更中か=true;
        try{
            await this.同期Async(文書);
            if(文書.未保存か){throw new ManagementException("Conflict","同期中の追加入力を保持しています。同期してから構成を変更してください。");}
            var 基準=文書.現在.Freeze();
            var 応答=await this.操作.OpenSourceProjectAsync(new(文書.情報.ProjectPath,文書.情報.TargetFramework){Configuration=構成},this.寿命.Token);
            var 統合=SourceWorkspace.Merge(基準,文書.現在,応答.Source!);
            文書.情報=応答;文書.基準=応答.Source!;文書.現在=応答.Source! with{Files=統合.Files};
            文書.同期停止か=応答.HasConflict;文書.競合ありか=応答.HasConflict;文書.競合=null;
            文書.成果物=null;文書.成果物案内=null;文書.成果物エラーか=false;
            if(!文書.現在.Files.Any(ファイル=>ファイル.Path==文書.ファイル)){文書.ファイル=文書.現在.Files.First().Path;}
            this.案内=表示文言.取得("ビルド構成を {0} に変更しました。{1} ファイルを評価しました。下書きへ反映後、この構成でビルドしたDLLを検査してください。",応答.Configuration,文書.現在.Files.Length);
        }finally{this.構成変更中か=false;}
    });
}
