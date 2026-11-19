namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private bool DB一括適用対応か;

    private async Task 未公開DBを読むAsync(){
        var 未公開=new Dictionary<string,CatalogDatabase>(StringComparer.Ordinal);
        long カーソル=0;
        while(true){
            var ページ=await this.管理操作.UnpublishedDatabasesAsync(カーソル,this.寿命.Token);
            foreach(var 下書き in ページ.Items){
                var 定義=下書き.Definition;
                未公開["db:"+定義.Id]=new("db:"+定義.Id,定義.Database,定義.Id,0,"",false);
            }
            if(!ページ.HasMore){break;}
            カーソル=ページ.NextCursor;
        }
        foreach(var 文書 in this.文書一覧.Values.Where(文書=>文書.DB.Version==0&&文書.未保管か)){
            未公開[文書.DB.Id]=文書.DB;
        }
        var 稼働=this.DB一覧.Select(DB=>DB.Id).ToHashSet(StringComparer.Ordinal);
        this.DB一覧=[..this.DB一覧,..未公開.Values.Where(DB=>!稼働.Contains(DB.Id))];
    }

    private string DB表示名(CatalogDatabase DB)=>this.文書一覧.GetValueOrDefault(DB.Id)?.DB変更?.NewName??DB.Name;
    private bool DB名が使えるか(CatalogDatabase? 対象,string 名前)=>
        名前.Length is >0 and <=128&&!名前.Any(char.IsControl)&&名前!="*"&&!名前.Equals("system",StringComparison.OrdinalIgnoreCase)&&
        !this.DB一覧.Any(DB=>DB.Id!=対象?.Id&&(DB.Name==名前||this.DB表示名(DB)==名前));

    private async Task 新規DBを下書きに追加Async(string 名前){
        名前=名前.Trim();
        if(!this.DB名が使えるか(null,名前)||!this.権限があるか(名前,ManagementPermission.Edit)){
            this.案内=表示文言.取得("DB名の重複・使用できない名前・権限を確認してください。");this.エラーか=true;return;
        }
        var 定義=DatabaseDesigner.Empty("db-"+Guid.NewGuid().ToString("N"),名前);
        var DB=new CatalogDatabase("db:"+定義.Id,名前,定義.Id,0,"",false);
        this.文書一覧.Add(DB.Id,new(DB,null,new(定義,this.利用者!.Name,0,0,DateTimeOffset.UtcNow){DatabaseChange=new(null)}));
        this.DB一覧=[..this.DB一覧,DB];this.DB名編集中か=false;
        this.DBツリー状態(DB.Id).展開中か=true;
        await this.DBを開くAsync(DB.Id);
        this.案内=表示文言.取得("データベースを下書きに追加しました。");this.エラーか=false;
    }

    private Task DB名前変更を下書きに追加Async(CatalogDatabase DB,string 名前)=>this.処理Async(async()=>{
        名前=名前.Trim();
        if(!this.DB名が使えるか(DB,名前)){throw new ManagementException("Validation",表示文言.取得("DB名の重複・使用できない名前・権限を確認してください。"));}
        await this.DB文書を読み込むAsync(DB);
        var 文書=this.文書一覧[DB.Id];
        if(文書.未確定操作 is not null||文書.DB変更 is{Delete:true}){return;}
        if(DB.Version==0){
            if(!this.権限があるか(名前,ManagementPermission.Edit)){throw new ManagementException("Permission",表示文言.取得("権限がありません。"));}
            var 更新=DB with{Name=名前};
            文書.DB情報を更新(更新);文書.定義=文書.定義 with{Database=名前};
            this.DB一覧=this.DB一覧.Select(項目=>項目.Id==DB.Id?更新:項目).ToArray();
        }else{
            文書.DB変更=DB.Name==名前?null:new(DB.Name,名前);
        }
        文書.プレビュー=null;this.DB名前変更ID=null;
        this.案内=表示文言.取得("DB名の変更を下書きに反映しました。");
    });

    private bool DB削除を下書きにできるか(CatalogDatabase DB)=>this.DB一括適用対応か&&!DB.IsSystem&&DB.Version>0
        &&this.権限があるか(DB.Name,ManagementPermission.Manage|ManagementPermission.Edit)
        &&this.文書一覧.GetValueOrDefault(DB.Id)?.未確定操作 is null;

    private Task DB削除を切り替えるAsync(CatalogDatabase DB)=>this.処理Async(async()=>{
        if(!this.DB削除を下書きにできるか(DB)){return;}
        await this.DB文書を読み込むAsync(DB);
        var 文書=this.文書一覧[DB.Id];
        if(文書.DB変更 is{Delete:true} 削除){
            文書.DB変更=削除.NameBeforeDeletion is{} 名前?new(DB.Name,名前):null;
            this.案内=表示文言.取得("DBの削除予定を取り消しました。");
        }else{
            this.ツリーの入力を確認(文書);
            if(this.対象文書一覧.Values.Any(項目=>項目.DBID==DB.Id&&項目.データ編集中か)){
                throw new ManagementException("Validation",表示文言.取得("行の編集を保存するか取り消してからDBを削除してください。"));
            }
            文書.DB変更=new(DB.Name,Delete:true){NameBeforeDeletion=文書.DB変更?.NewName};
            this.案内=表示文言.取得("DBの削除を下書きに追加しました。");
        }
        文書.プレビュー=null;
        this.文書を選ぶ(DB.Id);
    });

    private async Task DB適用完了時Async(文書状態 文書,OperationInfo 結果){
        var DB=文書.DB;
        if(結果.PublishedVersion is{} 公開版){
            文書.公開完了(公開版);
            if(文書.定義.Kind==DefinitionKind.Database){
                文書.DB情報を更新(DB with{Name=結果.Database,Version=公開版});
                if(DB.Name!=結果.Database){
                    if(this.利用者 is{} 利用者){this.利用者=利用者 with{Grants=利用者.Grants.Select(許可=>許可.Database==DB.Name?許可 with{Database=結果.Database}:許可).ToArray()};}
                    this.登録関数一覧.Remove(DB.Id);
                }
            }
            await this.更新Async();
            if(文書.定義.Kind==DefinitionKind.Database&&DB.Version==0&&this.選択DBID==DB.Id&&this.ソース操作できるか){
                this.プロジェクト保存開始時(文書);
            }
            this.案内=表示文言.取得("変更を公開しました · v")+公開版;
        }else if(文書.DB変更 is{Delete:true}){
            foreach(var 項目 in this.対象文書一覧.Values.Where(項目=>項目.DBID==DB.Id).ToArray()){
                this.タブ順序.削除(項目.ID);this.対象文書一覧.Remove(項目.ID);this.対象画面.Remove(項目.ID);
            }
            foreach(var ID in this.関数定義文書.Where(項目=>項目.Value.DB.Id==DB.Id).Select(項目=>項目.Key).ToArray()){this.関数定義文書.Remove(ID);}
            this.文書一覧.Remove(DB.Id);this.登録関数一覧.Remove(DB.Id);this.公開DB一覧.Remove(DB.Id);
            if(this.選択DBID==DB.Id){this.選択DBID=null;this.選択作業文書ID=null;}
            await this.更新Async();
            this.案内=表示文言.取得("データベースを削除しました。");
        }else{throw new InvalidOperationException(表示文言.取得("公開版を確認できませんでした。"));}
        if(結果.BackupPath is{} バックアップ){this.案内+=" · "+表示文言.取得("バックアップ: {0}",バックアップ);}
    }
}
