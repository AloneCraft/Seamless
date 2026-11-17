namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly Dictionary<string,Definition> ソース反映基準=new(StringComparer.Ordinal);
    private async Task ソース成果物取込後Async(ImportedSourceBuild 成果物){
        var 下書き=await this.管理操作.GetDraftAsync(成果物.Build.DefinitionId,this.寿命.Token);
        await this.ソース下書き反映後Async(下書き);
        var 文書=this.全定義文書.Single(候補=>候補.定義.Id==成果物.Build.DefinitionId);
        if(!文書.成果物を選択(成果物)){throw new ManagementException("Conflict","取り込んだ成果物と表示中の下書きが異なります。再検査してください。");}
        this.プロジェクト表示か=false;
        this.案内=表示文言.取得("成果物を選択しました · 「変更を確認」から検証・明示適用へ進めます。");
    }
    private async Task ソース下書き反映前Async(string ID){
        if(this.処理中数>0||this.コマンド?.実行中か==true||this.表入力中か){
            throw new ManagementException("Busy","実行中の操作、または入力中のテーブル編集を確定してから反映してください。");
        }
        var 文書=this.全定義文書.FirstOrDefault(候補=>候補.定義.Id==ID);
        if(文書 is null){return;}
        await this.定義編集を同期Async(文書);
        if(文書.未確定操作 is not null){throw new ManagementException("Busy","適用操作の結果を確認してください。");}
        if(文書.未保管か){await this.下書き保管本体Async(文書);}
        if(文書.未保管か){throw new ManagementException("Conflict","保管中の追加入力を確認してから反映してください。");}
        this.ソース反映基準[ID]=文書.定義.Freeze();
    }
    private async Task ソース下書き反映後Async(Draft 下書き){
        var 既存=this.全定義文書.FirstOrDefault(候補=>候補.定義.Id==下書き.Definition.Id);
        var 定義=下書き.Definition.Freeze();
        if(既存 is not null&&this.ソース反映基準.TryGetValue(下書き.Definition.Id,out var 基準)){
            定義=SourceWorkspace.MergeDefinitions(基準,定義,既存.定義);
        }
        var 公開=await this.管理操作.GetDefinitionAsync(下書き.Definition.Id,this.寿命.Token);
        var DB=既存?.DB;
        if(DB is null){
            long カーソル=0;var 版="";
            while(true){
                var ページ=await this.管理操作.DatabasesAsync(カーソル,版,this.寿命.Token);
                DB=ページ.Items.FirstOrDefault(候補=>候補.DefinitionId==下書き.Definition.Id||公開.Definition.Kind==DefinitionKind.Query&&候補.Name==公開.Database);
                if(DB is not null||!ページ.HasMore){break;}
                カーソル=ページ.NextCursor;版=ページ.VersionToken;
            }
        }
        if(DB is null){throw new ManagementException("NotFound","反映先DBの一覧を更新してください。");}
        if(公開.Definition.Kind==DefinitionKind.Query){this.関数定義文書[定義.Id]=new(DB,公開,下書き){定義=定義};}
        else{this.文書一覧[DB.Id]=new(DB,公開,下書き){定義=定義};}
        if(!this.DB一覧.Any(候補=>候補.Id==DB.Id)){this.DB一覧=[..this.DB一覧,DB];}
        this.タブ順序.追加(DB.Id);this.文書を選ぶ(DB.Id);this.ソース表示か=false;this.保持計画=[];
        this.ソース反映基準.Remove(下書き.Definition.Id);
        this.案内=表示文言.取得("{0} · 下書き r{1}に反映しました。",DB.Name,下書き.Revision);
    }
}
