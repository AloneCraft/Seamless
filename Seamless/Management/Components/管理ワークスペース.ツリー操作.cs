namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    private readonly Dictionary<string,ツリー対象操作> ツリー操作一覧=new(StringComparer.Ordinal);

    // 左ツリーのメニュー(ボタン・右クリック・Shift+F10)に出す操作。作成は子ノードの「＋…を追加」行で行うため、メニューには出さない。
    // 見出し・DBは全ソース、分類・スキーマ・テーブル・ビュー・関数はその対象の生成コードを表示できる。
    // コマンドパレット(Ctrl+K)は従来どおり全操作を検索できる。
    private bool ツリーメニュー操作か(管理対象 対象,string ID){
        if(this.パレットか){return true;}
        if(対象.種類=="DB"){return ID is "DB名変更" or "DB削除" or "全ソース";}
        if(対象.種類=="分類"){return ID=="全ソース";}
        if(対象.種類 is "表" or "問い合わせ"){return ID is "対象名前変更" or "対象コピー" or "対象削除" or "生成コード";}
        if(対象.種類=="スキーマ"){return ID is "生成コード" or "スキーマ削除";}
        if(対象.種類=="オブジェクト分類"){return ID=="生成コード";}
        return true;
    }

    private ツリー対象操作 ツリー操作(string DBID){
        if(!this.ツリー操作一覧.TryGetValue(DBID,out var 操作)){
            操作=new(){
                作成=(所属,分類,名前)=>this.ツリー作成Async(DBID,所属,分類,名前),
                スキーマ作成=名前=>this.ツリースキーマ作成Async(DBID,名前),
                名前変更=(項目,名前)=>this.ツリー名前変更Async(DBID,項目,名前),
                コピー=項目=>this.ツリーコピーAsync(DBID,項目),
                削除=項目=>this.ツリー削除Async(DBID,項目),
                スキーマ削除=名前=>this.ツリースキーマ削除Async(DBID,名前),
            };
            this.ツリー操作一覧.Add(DBID,操作);
        }
        return 操作;
    }

    private 文書状態? 編集できる文書(string DBID,out ツリー操作結果? 拒否){
        拒否=null;
        if(!this.文書一覧.TryGetValue(DBID,out var 文書)){拒否=new(表示文言.取得("DBの下書きを読み込んでから操作してください。"));return null;}
        if(文書.未確定操作 is not null||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)||this.操作中か){拒否=new(表示文言.取得("現在この操作はできません。権限または実行中の操作を確認してください。"));return null;}
        return 文書;
    }

    private Task<ツリー操作結果> 下書きを更新Async(文書状態 文書,Func<Definition,Definition> 変更){
        try{
            文書.定義=変更(文書.定義);文書.プレビュー=null;
        }catch(ManagementException 例外){return Task.FromResult(new ツリー操作結果(例外.Message));}
        // 再描画は、操作したツリー部品からの未保存変更の通知で行う。
        return Task.FromResult(ツリー操作結果.成功);
    }

    private async Task<ツリー操作結果> ツリー作成Async(string DBID,string 所属,string 分類,string 名前){
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return 拒否!;}
        ツリー操作結果 結果;
        if(分類=="Table"){
            結果=await this.下書きを更新Async(文書,定義=>DatabaseDesigner.Apply(定義,new TableDesign(名前,所属,[new ColumnDesign("Id","int",true)],[])));
        }else if(分類=="View"){
            結果=await this.下書きを更新Async(文書,定義=>DatabaseDesigner.ApplyQuery(定義,new(名前,所属,DatabaseQueryKind.View,[new("Id","int",false)],[],"",新規ビュー本体(名前)),formatBody:true));
        }else{
            結果=await this.下書きを更新Async(文書,定義=>DatabaseDesigner.ApplyQuery(定義,new(名前,所属,DatabaseQueryKind.ScalarFunction,[new("Id","int",false)],[],"int","return default;")));
        }
        if(結果.案内 is null){
            var 経路=所属.Length==0?名前:所属+"."+名前;
            this.DBツリー状態(DBID).テーブルを開閉(経路,true);
        }
        return 結果;
    }

    private Task<ツリー操作結果> ツリースキーマ作成Async(string DBID,string 名前){
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return Task.FromResult(拒否!);}
        return this.下書きを更新Async(文書,定義=>DatabaseDesigner.AddSchema(定義,名前));
    }

    // 空のスキーマを下書きから消す。消したスキーマを開いている文書のタブは閉じる。
    private async Task<ツリー操作結果> ツリースキーマ削除Async(string DBID,string 名前){
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return 拒否!;}
        var 結果=await this.下書きを更新Async(文書,定義=>DatabaseDesigner.RemoveSchema(定義,名前));
        if(結果.案内 is null){
            foreach(var 開いた文書 in this.対象文書一覧.Values.Where(項目=>項目.DBID==DBID&&項目.対象 is null&&項目.スキーマ==名前).ToArray()){this.作業文書を閉じる(開いた文書.ID);}
            if(this.選択DBID==DBID&&this.選択スキーマ名==名前){this.選択スキーマ名="";}
        }
        return 結果;
    }
    private async Task スキーマ削除をメニューから実行Async(管理対象 対象){
        var 結果=await this.ツリースキーマ削除Async(対象.ID,対象.スキーマ);
        if(結果.案内 is{} 案内){this.案内=案内;this.エラーか=true;}
        await this.InvokeAsync(this.StateHasChanged);
    }

    private async Task<ツリー操作結果> ツリー名前変更Async(string DBID,CatalogObject 項目,string 名前){
        if(項目.Kind=="Set"){return new(表示文言.取得("取引を通さない表の名前の変更・コピー・削除は、C#で編集してください。"),true);}
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return 拒否!;}
        if(項目.Kind=="Table"){
            return await this.下書きを更新Async(文書,定義=>DatabaseDesigner.Apply(定義,DatabaseDesigner.Read(定義,項目.Id) with{Name=名前,OriginalPath=項目.Id}));
        }
        await this.定義編集を同期Async(文書);
        var 結果=await this.下書きを更新Async(文書,定義=>DatabaseDesigner.RenameQuery(定義,項目.Id,名前,new Server.RuntimeCompiler().CompileSchemaForEditing(定義.Source)));
        if(結果.案内 is not null){return 結果;}
        var 次=文書.オブジェクト.Single(対象=>対象.Schema==項目.Schema&&対象.Name==名前);
        foreach(var 開いた文書 in this.対象文書一覧.Values.Where(対象=>対象.DBID==DBID&&対象.対象?.Id==項目.Id&&対象.対象.Kind==項目.Kind)){開いた文書.対象=次;}
        var 状態=this.DBツリー状態(DBID);
        if(状態.テーブル展開中か(項目.Id)){状態.テーブルを開閉(項目.Id,false);状態.テーブルを開閉(次.Id,true);}
        if(!文書.定義.Source.Files.Any(ファイル=>ファイル.Path==文書.選択ファイル)){文書.選択ファイル=文書.定義.Source.Files[0].Path;}
        return 結果;
    }

    private Task<ツリー操作結果> ツリーコピーAsync(string DBID,CatalogObject 項目){
        if(項目.Kind=="Set"){return Task.FromResult(new ツリー操作結果(表示文言.取得("取引を通さない表の名前の変更・コピー・削除は、C#で編集してください。"),true));}
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return Task.FromResult(拒否!);}
        var 名前=this.コピー名(文書.定義,項目);
        if(項目.Kind=="Table"){
            // 構造だけを複製する。行データは複製しない。
            return this.下書きを更新Async(文書,定義=>{var 元=DatabaseDesigner.Read(定義,項目.Id);return DatabaseDesigner.Apply(定義,new TableDesign(名前,元.Schema,元.Columns.Select(列=>new ColumnDesign(列.Name,列.Type,列.PrimaryKey)).ToArray(),元.ForeignKeys));});
        }
        return this.下書きを更新Async(文書,定義=>DatabaseDesigner.ApplyQuery(定義,DatabaseDesigner.ReadQuery(定義,項目.Id) with{Name=名前,OriginalPath=null}));
    }

    private string コピー名(Definition 定義,CatalogObject 項目){
        var 既存=DatabaseDesigner.Objects(定義).Where(対象=>対象.Schema==項目.Schema).Select(対象=>対象.Name).ToHashSet(StringComparer.Ordinal);
        var 名前=項目.Name+"Copy";var 番号=2;
        while(既存.Contains(名前)){名前=項目.Name+"Copy"+番号++;}
        return 名前;
    }

    private CatalogObject? メニューの項目(管理対象 対象){
        if(対象.表 is not{} 経路||!this.文書一覧.TryGetValue(対象.ID,out var 文書)){return null;}
        return 文書.オブジェクト.FirstOrDefault(項目=>項目.Id==経路&&(対象.種類=="表")==(項目.Kind=="Table"));
    }
    private async Task ツリー名前変更をメニューから開始Async(管理対象 対象){
        if(this.メニューの項目(対象) is{} 項目&&this.DBツリー画面.TryGetValue(対象.ID,out var 木)){await 木.名前変更を開始Async(項目);}
    }
    private async Task ツリー操作をメニューから実行Async(管理対象 対象,Func<ツリー対象操作,CatalogObject,Task<ツリー操作結果>> 操作){
        if(this.メニューの項目(対象) is not{} 項目){return;}
        var 結果=await 操作(this.ツリー操作(対象.ID),項目);
        if(結果.案内 is{} 案内){this.案内=案内;this.エラーか=true;}
    }

    private async Task<ツリー操作結果> ツリー削除Async(string DBID,CatalogObject 項目){
        if(項目.Kind=="Set"){return new(表示文言.取得("取引を通さない表の名前の変更・コピー・削除は、C#で編集してください。"),true);}
        if(this.編集できる文書(DBID,out var 拒否) is not{} 文書){return 拒否!;}
        var 開いた文書=this.対象文書一覧.Values.Where(対象=>対象.DBID==DBID&&対象.対象?.Id==項目.Id&&対象.対象.Kind==項目.Kind&&!対象.登録関数か).ToArray();
        if(開いた文書.Any(対象=>対象.編集中か||対象.データ編集中か)||this.DBツリー画面.GetValueOrDefault(DBID)?.未保存か is true){
            return new(表示文言.取得("編集中の入力を確定するか取り消してから削除してください。"));
        }
        var 結果=await this.下書きを更新Async(文書,定義=>項目.Kind=="Table"?DatabaseDesigner.Remove(定義,項目.Id):DatabaseDesigner.RemoveQuery(定義,項目.Id));
        if(結果.案内 is not null){return 結果;}
        if(!文書.定義.Source.Files.Any(ファイル=>ファイル.Path==文書.選択ファイル)){文書.選択ファイル=文書.定義.Source.Files[0].Path;}
        var DBを選ぶか=開いた文書.Any(対象=>対象.ID==this.選択作業文書ID);
        foreach(var 対象 in 開いた文書){
            this.タブ順序.削除(対象.ID);this.対象文書一覧.Remove(対象.ID);this.対象画面.Remove(対象.ID);
        }
        if(DBを選ぶか){this.作業文書を選ぶ(this.対象文書を取得(DBID).ID);}
        this.案内=表示文言.取得("「{0}」の削除を下書きに追加しました。",項目.Id);this.エラーか=false;
        return 結果;
    }
}
