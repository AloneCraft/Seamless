using System.Data;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Win32;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private ManagementClient 必須接続()=>this.クライアント??throw new ManagementException("Connection","接続してください。");
    private Definition 現在定義(){
        var 定義=this.編集定義??throw new ManagementException("Selection","定義を選んでください。");
        return 定義 with{EntryType=this.入口型.Text,EntryMember=this.入口メソッド.Text,WritesData=this.更新定義.IsChecked==true};
    }
    private async Task 接続Async(){
        if(this.未保存か&&MessageBox.Show(this,"未保存の編集を破棄して接続先を変更しますか？","接続先の変更",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}
        this.クライアント=null;this.ログイン利用者=null;this.定義一覧.Items.Clear();this.DB項目.Clear();this.表示を消去();this.ソース案内.Text="接続中です。ログイン後に一覧を取得します。";this.検証DBID=null;this.権限表示を更新();
        if(this.ローカル接続 is{PipeName:not null} ローカル情報&&ReferenceEquals(this.接続一覧.SelectedItem,this.ローカル接続設定)&&this.ホスト.Text==ローカル情報.Host&&this.ポート.Text==ローカル情報.Port.ToString()){await this.ローカルIPC接続Async(ローカル情報);return;}
        X509Certificate2? 信頼=null;if(this.証明書.Text.Length>0){信頼=X509CertificateLoader.LoadCertificateFromFile(this.証明書.Text);}
        var 接続=new ManagementClient(this.ホスト.Text,int.Parse(this.ポート.Text,System.Globalization.CultureInfo.InvariantCulture),信頼);
        var 情報=await 接続.InfoAsync();if(this.ローカル接続設定 is not null&&ReferenceEquals(this.接続一覧.SelectedItem,this.ローカル接続設定)&&this.ローカル接続 is{} ローカル&&情報.InstanceId!=ローカル.InstanceId){throw new ManagementException("Connection","検出したローカルサーバーと接続先が一致しません。接続情報を更新してください。");}var 認証=await 接続.LoginAsync(this.利用者.Text,this.パスワード.Password);this.パスワード.Clear();this.サーバー能力=情報.Capabilities;
        await this.接続を確定Async(接続,情報,認証.User,false);
    }
    private async Task ローカルIPC接続Async(LocalServerConnection 情報){
        var 接続=ManagementClient.ForLocalPipe(情報.PipeName??throw new ManagementException("Connection","ローカルIPCが有効ではありません。サーバーを更新して再起動してください。"));var サーバー=await 接続.InfoAsync();if(サーバー.InstanceId!=情報.InstanceId){throw new ManagementException("Connection","検出したサーバーとIPC接続先が一致しません。");}
        var 利用者=await 接続.LocalIdentityAsync();await this.接続を確定Async(接続,サーバー,利用者,true);
    }
    private async Task 接続を確定Async(ManagementClient 接続,ServerInfo 情報,UserInfo 利用者,bool IPCか){
        this.クライアント=接続;this.ログイン利用者=利用者;this.サーバー能力=情報.Capabilities;this.パスワード.Clear();this.診断.Text="";await this.一覧更新Async();
        var 方式="TCP / TLS";if(IPCか){方式="ローカルIPC · OSユーザー認証";}this.状態.Text="接続済み · "+方式+" · "+利用者.Name;this.接続案内.Text=this.状態.Text;this.接続案内.ToolTip=情報.InstanceId.ToString();this.接続設定パネル.IsExpanded=false;
        this.ソース案内.Text="接続済みです。DBを展開してオブジェクト、または公開済み定義・個人下書きを選択してください。";
        if(this.利用者DBが未作成か){this.ソース案内.Text="閲覧できる利用者DBはまだありません。「新規DB」でDBの下書きを作成できます。システムDBの構造は左の「データベース」から参照できます。";}
    }
    private Task 接続保存Async(){
        var 設定=new 接続設定(this.ホスト.Text+":"+this.ポート.Text,this.ホスト.Text,int.Parse(this.ポート.Text,System.Globalization.CultureInfo.InvariantCulture),this.利用者.Text,this.証明書.Text);
        this.接続設定一覧.RemoveAll(既存=>既存.名前==設定.名前);this.接続設定一覧.Add(設定);Directory.CreateDirectory(this.設定ディレクトリ);
        File.WriteAllText(Path.Combine(this.設定ディレクトリ,"connections.json"),JsonSerializer.Serialize(this.接続設定一覧,ManagementJson.Options));this.接続一覧.ItemsSource=null;this.接続一覧.ItemsSource=this.接続設定一覧;this.状態.Text="接続先を保存しました。パスワードは保存していません。";return Task.CompletedTask;
    }
    private void 接続設定を読む(){
        var パス=Path.Combine(this.設定ディレクトリ,"connections.json");if(File.Exists(パス)){try{this.接続設定一覧.AddRange(JsonSerializer.Deserialize<接続設定[]>(File.ReadAllText(パス),ManagementJson.Options)??[]);}catch(JsonException){this.診断.Text="接続設定のJSONを確認してください。";}}
        this.接続一覧.ItemsSource=this.接続設定一覧;
    }
    private async Task 一覧更新Async(){
        this.必須接続();this.展開処理.Clear();this.定義一覧.Items.Clear();this.DB項目.Clear();this.個人下書き.Items.Clear();this.稼働カタログ.Items.Clear();this.公開定義一覧.Items.Clear();
        this.個人下書き=new(){Header="個人の下書き",IsExpanded=true,Foreground=System.Windows.Media.Brushes.White};this.稼働カタログ=new(){Header="データベース",IsExpanded=true,Foreground=System.Windows.Media.Brushes.White};this.公開定義一覧=new(){Header="公開済み定義",IsExpanded=true,Foreground=System.Windows.Media.Brushes.White};this.表示DB.Clear();
        var 接続先=new TreeViewItem{Header=this.ホスト.Text+":"+this.ポート.Text,IsExpanded=true,Foreground=System.Windows.Media.Brushes.White};this.定義一覧.Items.Add(接続先);接続先.Items.Add(this.稼働カタログ);接続先.Items.Add(this.公開定義一覧);接続先.Items.Add(this.個人下書き);
        if(this.サーバー能力.Contains("NativeCatalogV1")){await this.カタログDBAsync();}else{this.稼働カタログ.Items.Add(カタログ項目("このサーバーはオブジェクト一覧APIに未対応です。"));}
        await this.定義ページAsync(0);await this.下書きページAsync(0);
        this.概要を表示();
    }
    private async Task 下書きページAsync(long カーソル){if(カーソル<0){this.状態.Text="下書きの最後のページです。";return;}var ページ=await this.必須接続().CallAsync<Page<DraftSummary>>("drafts","",new{cursor=カーソル});this.下書きカーソル=-1;if(ページ.HasMore){this.下書きカーソル=ページ.NextCursor;}foreach(var 下書き in ページ.Items){this.下書き項目を更新(下書き);}}
    private void 下書き項目を更新(DraftSummary 下書き){var 項目=this.個人下書き.Items.OfType<TreeViewItem>().FirstOrDefault(候補=>候補.Tag is DraftSummary 既存&&既存.Definition.Id==下書き.Definition.Id);if(項目 is null){項目=new TreeViewItem{Foreground=System.Windows.Media.Brushes.White};this.個人下書き.Items.Add(項目);}項目.Header=下書き.Definition.Id+" / "+下書き.Definition.Database+" · 下書き "+下書き.Revision;項目.Tag=下書き;}
    private async Task 下書きを開くAsync(DraftSummary 概要){if(this.未保存か&&MessageBox.Show(this,"未保存の編集を破棄して下書きを開きますか？","下書きを開く",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}var 下書き=await this.必須接続().CallAsync<Draft>("draft",概要.Definition.Database,new{id=概要.Definition.Id});this.定義を表示(下書き.Definition,下書き.Revision,下書き.BaseVersion);await this.解析Async();}
    private async Task 定義ページAsync(long カーソル){
        if(カーソル<0){this.状態.Text="最後のページです。";return;}
        var ページ=await this.必須接続().CallAsync<Page<DefinitionSummary>>("definitions","",new{cursor=カーソル});this.定義カーソル=-1;if(ページ.HasMore){this.定義カーソル=ページ.NextCursor;}
        foreach(var 版 in ページ.Items){if(!this.DB項目.TryGetValue(版.Definition.Database,out var 親)){親=new TreeViewItem{Header=版.Definition.Database,IsExpanded=true,Foreground=System.Windows.Media.Brushes.White};this.DB項目.Add(版.Definition.Database,親);this.公開定義一覧.Items.Add(親);}親.Items.Add(new TreeViewItem{Header=版.Definition.Id+"  v"+版.Version,Tag=版,Foreground=System.Windows.Media.Brushes.White});}
        if(ページ.HasMore){this.状態.Text="定義の続きがあります。「定義の次のページ」で取得できます。";}
    }
    private async Task 定義を開くAsync(DefinitionVersion 版){
        if(this.未保存か&&MessageBox.Show(this,"未保存の編集を破棄して開きますか？","定義を開く",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}
        this.定義を表示(版.Definition,0,版.Version);
        await this.解析Async();
    }
    private async Task 定義を取得して開くAsync(DefinitionSummary 概要){var 版=await this.必須接続().CallAsync<DefinitionVersion>("definition",概要.Definition.Database,new{id=概要.Definition.Id});await this.定義を開くAsync(版);}
    private void 定義を表示(Definition 定義,long 下書き,long 基準){
        this.編集タブ.SelectedIndex=0;this.属性を表示(true);
        if(this.編集定義?.Database!=定義.Database){this.検証DBID=null;}
        this.反映中か=true;try{
            this.システム表示か=false;this.編集定義=定義;this.下書き版=下書き;this.基準版=基準;this.保存済みハッシュ=定義.Hash();this.未保存か=false;this.計画表示か=false;this.ビルド結果=null;this.選択ノード=null;this.折り畳み.Clear();this.ソース案内.Text=下書き>0?"個人下書き r"+下書き+" · 基準版 v"+基準:"稼働版 v"+基準+" · 編集して保存すると個人下書きになります。";
            this.定義ID.Text=定義.Id;this.DB名.Text=定義.Database;this.定義ID.IsReadOnly=true;this.DB名.IsReadOnly=true;this.入口型.Text=定義.EntryType;this.入口メソッド.Text=定義.EntryMember;this.更新定義.IsChecked=定義.WritesData;
            this.ファイル一覧.ItemsSource=定義.Source.Files.Select(ファイル=>ファイル.Path).ToArray();this.ファイル一覧.SelectedIndex=0;
            this.選択ファイル=定義.Source.Files.FirstOrDefault()?.Path;this.コード.Text=定義.Source.Files.FirstOrDefault()?.Text??"";if(定義.Source.Files.Length==0){this.ソース案内.Text="ソース未登録";}
        }finally{this.反映中か=false;}this.権限表示を更新();this.状態.Text="編集中: "+定義.Id+" · 下書き "+下書き+" · 基準版 "+基準;
    }
    private async Task 新規Async(DefinitionKind 種類){
        if(!this.破棄を確認("新しい定義を開く")){return;}
        var 入力=new 入力ダイアログ(this,"新規"+種類,new 入力項目("定義ID",Guid.NewGuid().ToString("N")[..8]),new 入力項目("DB名",this.DB名.Text));
        if(入力.ShowDialog()!=true){return;}var 値=入力.値;var ソース=DB雛形;var 型="UserCode.ManagedDb";var メソッド="";
        if(種類==DefinitionKind.Query){ソース=クエリ雛形;型="UserCode.Query";メソッド="Run";}
        var 定義=new Definition(値[0],値[1],種類,型,メソッド,new("1",[new("Definition.cs",ソース)],[]));this.定義を表示(定義,0,0);this.未保存か=true;await this.解析Async();
    }
    private async Task 保存Async(){
        var 定義=this.現在定義();var 保存=await this.必須接続().CallAsync<Draft>("saveDraft",定義.Database,new SaveDraftRequest(定義,this.下書き版,this.基準版));
        this.下書き版=保存.Revision;this.保存済みハッシュ=定義.Hash();this.未保存か=this.現在定義().Hash()!=定義.Hash();this.状態.Text="個人の下書きを保存しました · "+保存.Revision;
        this.下書き項目を更新(new(new(保存.Definition.Id,保存.Definition.Database,保存.Definition.Kind,保存.Definition.EntryType,保存.Definition.EntryMember,保存.Definition.WritesData),保存.Revision,保存.BaseVersion,保存.SavedAt));
    }
    private async Task 保存を確認Async(){if(this.未保存か||this.下書き版==0){await this.保存Async();}}
    private async Task 解析Async(){
        if(this.編集定義 is null){return;}var 定義=this.現在定義();AnalysisResult 結果;
        if(this.クライアント is null){結果=await Task.Run(()=>new SourceEditor().Analyze(定義.Source));}
        else if(this.権限があるか(ManagementPermission.Edit|ManagementPermission.Validate)){結果=await this.必須接続().CallAsync<AnalysisResult>("analyze",定義.Database,定義);}
        else{結果=await this.必須接続().CallAsync<AnalysisResult>("publishedAnalysis",定義.Database,new{id=定義.Id});}
        if(this.編集定義.Source.Hash()!=結果.SourceHash){return;}
        this.診断.Text=string.Join("\n",結果.Diagnostics.Select(項目=>項目.Code+" "+項目.Path+":"+項目.Start+" "+項目.Message));
        if(結果.Current){this.選択ノード=null;this.解析結果=結果;this.計画表示か=false;this.図の状態.Text="問い合わせ・スキーマの編集図 · "+結果.Nodes.Length+" ノード";this.図を更新();}
        else{this.図の状態.Text="更新前の図 · コンパイル診断を確認してください";}
    }
    private async Task 計画Async(){
        var 定義=this.現在定義();BuildResult 結果;if(this.権限があるか(ManagementPermission.Edit|ManagementPermission.Validate)){await this.保存を確認Async();結果=await this.必須接続().CallAsync<BuildResult>("build",定義.Database,new DraftKey(定義.Id,this.下書き版));}else{結果=await this.必須接続().CallAsync<BuildResult>("plans",定義.Database,new{id=定義.Id});}this.ビルド結果=結果;this.計画表示か=true;this.図を更新();this.編集タブ.SelectedIndex=1;
        this.診断.Text=string.Join("\n",結果.Diagnostics.Select(項目=>項目.Message));this.図の状態.Text="静的実行計画 · "+結果.Plans.Length+" メソッド · 実測値は含みません";
        if(結果.Plans.Length==0){this.診断.Text+="\nこの版にはFLinq実行計画属性がありません。LinqIntercept対象と呼び出しを確認してください。";}
    }
    private async Task 検証DB作成Async(){
        await this.保存を確認Async();var 定義=this.現在定義();var 対象ID=定義.Id;long? 下書き=this.下書き版;
        if(定義.Kind==DefinitionKind.Query){
            long カーソル=0;DefinitionSummary? DB定義=null;do{var 一覧=await this.必須接続().CallAsync<Page<DefinitionSummary>>("definitions",定義.Database,new{cursor=カーソル});DB定義=一覧.Items.FirstOrDefault(項目=>項目.Definition.Database==定義.Database&&項目.Definition.Kind==DefinitionKind.Database);if(DB定義 is not null||!一覧.HasMore){break;}カーソル=一覧.NextCursor;}while(true);
            対象ID=DB定義?.Definition.Id??throw new InvalidOperationException("公開済みDB定義を取得できませんでした。");下書き=null;
        }
        var 検証=await this.必須接続().CallAsync<ValidationDatabase>("validation",定義.Database,new{id=対象ID,draftRevision=下書き});this.検証DBID=検証.Id;this.状態.Text="空の検証DBを作成しました: "+検証.Id;this.診断.Text="検証データは明示的に投入してください。稼働DBのデータはコピーしていません。";
    }
    private async Task 検証データAsync(){
        if(this.検証DBID is null){throw new InvalidOperationException("先に検証DBを作成してください。");}
        var 入力=new 入力ダイアログ(this,"検証データの投入",new 入力項目("テーブル名","Items"),new 入力項目("行のJSON配列","[{\"Id\":1,\"Name\":\"sample\"}]",複数行か:true));if(入力.ShowDialog()!=true){return;}var 値=入力.値;using var JSON=JsonDocument.Parse(値[1]);var 行=JSON.RootElement.EnumerateArray().Select(項目=>項目.Clone()).ToArray();
        await this.必須接続().CallAsync<JsonElement>("seed",this.DB名.Text,new SeedRequest(this.検証DBID,値[0],行));this.状態.Text="検証DBへ "+行.Length+" 行を投入しました。";
    }
    private async Task 検証管理Async(){
        var ページ=await this.必須接続().CallAsync<Page<ValidationDatabase>>("validations",this.DB名.Text,new{cursor=0});
        var 入力=new 入力ダイアログ(this,"個人の検証DB",new 入力項目("一覧",string.Join("\n",ページ.Items.Select(項目=>項目.Database+" · "+項目.Id+" · v"+項目.SourceVersion)),複数行か:true,読取専用か:true),new 入力項目("選択する検証DBのID",this.検証DBID??""),new 入力項目("操作（選択 / 破棄）","選択"));if(入力.ShowDialog()!=true){return;}var 値=入力.値;
        if(値[2]=="破棄"){if(MessageBox.Show(this,"検証DB "+値[1]+" と投入した検証データを破棄しますか？","検証DBの破棄",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}await this.必須接続().CallAsync<JsonElement>("dropValidation",this.DB名.Text,new{id=値[1]});if(this.検証DBID==値[1]){this.検証DBID=null;}this.状態.Text="検証DBを破棄しました。";}
        else{if(!ページ.Items.Any(項目=>項目.Id==値[1]&&項目.Database==this.DB名.Text)){throw new InvalidOperationException("現在のDBの一覧から検証DBを選択してください。");}this.検証DBID=値[1];this.状態.Text="検証DBを選択しました: "+値[1];}
    }
    private async Task 検証実行Async(){
        await this.保存を確認Async();if(this.検証DBID is null){throw new InvalidOperationException("先に検証DBを作成してください。");}var 定義=this.現在定義();if(定義.Kind!=DefinitionKind.Query){this.診断.Text="DB定義は検証DBの作成とデータ投入で確認できます。問い合わせ定義を選んで試験実行してください。";return;}
        var 引数=this.実行引数();if(引数 is null){return;}await this.実行結果Async(new(定義.Id,this.検証DBID,引数.Value,DraftRevision:this.下書き版));
    }
    private async Task 公開実行Async(){var 定義=this.現在定義();var 公開=await this.必須接続().CallAsync<DefinitionVersion>("definition",定義.Database,new{id=定義.Id});if(公開.Definition.WritesData&&MessageBox.Show(this,"この公開済み問い合わせは稼働DBを更新します。実行しますか？","更新問い合わせの実行",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}var 引数=this.実行引数();if(引数 is null){return;}await this.実行結果Async(new(定義.Id,null,引数.Value));}
    private JsonElement? 実行引数(){var 入力=new 入力ダイアログ(this,"問い合わせの引数",new 入力項目("入口メソッドの引数（contextを除くJSONオブジェクト）","{}",複数行か:true));if(入力.ShowDialog()!=true){return null;}using var 文書=JsonDocument.Parse(入力.値[0]);if(文書.RootElement.ValueKind!=JsonValueKind.Object){throw new InvalidDataException("引数はJSONオブジェクトで指定してください。");}return 文書.RootElement.Clone();}
    private async Task 実行結果Async(ExecuteRequest 要求){
        var 表=new DataTable();var 件数=0;
        await foreach(var 行 in this.必須接続().ExecuteAsync(this.DB名.Text,要求)){
            if(行.Value.ValueKind==JsonValueKind.Object){foreach(var 項目 in 行.Value.EnumerateObject()){if(!表.Columns.Contains(項目.Name)){表.Columns.Add(項目.Name);}}var 新行=表.NewRow();foreach(var 項目 in 行.Value.EnumerateObject()){新行[項目.Name]=項目.Value.ToString();}表.Rows.Add(新行);}
            else{if(!表.Columns.Contains("値")){表.Columns.Add("値");}var 新行=表.NewRow();新行["値"]=行.Value.ToString();表.Rows.Add(新行);}件数++;
        }
        this.結果.ItemsSource=表.DefaultView;this.編集タブ.SelectedIndex=2;this.状態.Text="実行完了 · "+件数+" 行（取得上限 "+要求.MaximumRows+"）";
    }
    private async Task 適用Async(){
        await this.保存を確認Async();var 定義=this.現在定義();var 影響=await this.必須接続().CallAsync<ChangePreview>("preview",定義.Database,new DraftKey(定義.Id,this.下書き版));
        var 説明=string.Join("\n",影響.Changes)+"\n依存先:\n"+string.Join("\n",影響.Dependencies)+"\n"+string.Join("\n",影響.Diagnostics.Select(項目=>項目.Message));this.診断.Text=説明;
        if(!影響.CanApply){this.状態.Text="適用できません。診断と共有版を確認してください。";return;}
        var 確認=new 入力ダイアログ(this,"差分・検証結果・依存関係の確認",new 入力項目("適用対象: "+定義.Id+" / 稼働版 v"+影響.CurrentVersion,説明+"\n\n検証結果:\n"+JsonSerializer.Serialize(影響.Validation,ManagementJson.Options),複数行か:true,読取専用か:true)){決定ラベル="稼働DBへ適用"};if(確認.ShowDialog()!=true){return;}
        this.適用操作ID=Guid.NewGuid();Directory.CreateDirectory(this.設定ディレクトリ);File.WriteAllText(Path.Combine(this.設定ディレクトリ,"last-operation.txt"),this.適用操作ID.ToString());this.状態.Text="適用中 · 操作ID "+this.適用操作ID;OperationInfo 結果;
        try{結果=await this.必須接続().CallAsync<OperationInfo>("apply",定義.Database,new ApplyRequest(定義.Id,this.下書き版,影響.Token,this.適用操作ID.Value));}
        catch(Exception 例外){this.診断.Text="適用結果は不明です。操作ID "+this.適用操作ID+" を使って結果を照会してください。\n"+例外.Message;this.状態.Text="結果不明 · 操作ID "+this.適用操作ID;return;}
        this.診断.Text=JsonSerializer.Serialize(結果,ManagementJson.Options);this.状態.Text="適用結果: "+結果.State;
        if(結果.State==OperationState.Succeeded){this.基準版=結果.PublishedVersion!.Value;await this.一覧更新Async();}
    }
    private async Task 取消Async(){
        if(this.適用操作ID is null){this.状態.Text="この画面で開始した適用操作がありません。";return;}
        try{await this.必須接続().CallAsync<JsonElement>("cancel",this.DB名.Text,this.適用操作ID.Value);var 結果=await this.必須接続().CallAsync<OperationInfo>("operation",this.DB名.Text,this.適用操作ID.Value);this.状態.Text="取消要求後の状態: "+結果.State;this.診断.Text=JsonSerializer.Serialize(結果,ManagementJson.Options);}catch(Exception 例外){this.診断.Text=例外.Message;}
    }
    private long 履歴カーソル;
    private Task 履歴Async(){this.履歴カーソル=0;return this.履歴続きAsync();}
    private async Task 履歴続きAsync(){if(this.履歴カーソル<0){this.状態.Text="履歴の最後です。";return;}var 履歴=await this.必須接続().CallAsync<Page<HistoryEntry>>("history",this.DB名.Text,new{cursor=this.履歴カーソル});this.履歴カーソル=-1;if(履歴.HasMore){this.履歴カーソル=履歴.NextCursor;}this.結果.ItemsSource=履歴.Items;this.編集タブ.SelectedIndex=2;this.状態.Text="履歴を "+履歴.Items.Length+" 件取得しました。";}
    private async Task 操作結果Async(){
        var 入力=new 入力ダイアログ(this,"切断・再起動後の適用結果を照会",new 入力項目("適用操作ID",this.適用操作ID?.ToString()??""));if(入力.ShowDialog()!=true){return;}
        var ID=Guid.Parse(入力.値[0]);var 操作=await this.必須接続().CallAsync<OperationInfo>("operation",this.DB名.Text,ID);this.適用操作ID=ID;this.診断.Text=JsonSerializer.Serialize(操作,ManagementJson.Options);this.状態.Text="操作結果: "+操作.State;
    }
    private async Task 基準更新Async(){
        var 定義=this.現在定義();var 共有=await this.必須接続().CallAsync<DefinitionVersion>("definition",定義.Database,new{id=定義.Id});
        var 差分="共有版 v"+共有.Version+"\n"+string.Join("\n",共有.Definition.Source.Files.Select(ファイル=>ファイル.Path+"\n"+ファイル.Text))+"\n\n個人の下書き\n"+string.Join("\n",定義.Source.Files.Select(ファイル=>ファイル.Path+"\n"+ファイル.Text));
        var 確認=new 入力ダイアログ(this,"共有版との差分を確認（コードは自動置換しません）",new 入力項目("確認内容",差分,複数行か:true,読取専用か:true));
        if(確認.ShowDialog()==true){this.基準版=共有.Version;await this.保存Async();this.状態.Text="共有版を基準として下書きを保存しました。内容のマージはコードで明示的に行ってください。";}
    }
    private Task ノード反映Async(){
        if(this.選択ノード is not{} ノード||this.解析結果 is null){throw new InvalidOperationException("編集図のノードを選んでください。");}
        var 定義=this.現在定義();var 更新=new SourceEditor().Apply(定義.Source,new(this.解析結果.SourceHash,ノード.Path,ノード.Start,ノード.Length,this.ノード編集.Text));this.ソースを反映(更新);return this.解析Async();
    }
    private Task 演算子変更Async(bool 削除か){
        if(this.選択ノード is not{Kind:"Operator"} ノード||this.解析結果?.SourceHash!=this.現在定義().Source.Hash()){throw new InvalidOperationException("現在の編集図から演算子を選んでください。");}
        var ファイル=this.現在定義().Source.Files.Single(項目=>項目.Path==ノード.Path);var テキスト=ファイル.Text.Substring(ノード.Start,ノード.Length);var 置換=テキスト+".Where(item=>true)";
        if(削除か){if(!ノード.Editable){throw new InvalidOperationException("static形式の呼び出しは選択部分のコードとして編集してください。");}if(SyntaxFactory.ParseExpression(テキスト) is not InvocationExpressionSyntax{Expression:MemberAccessExpressionSyntax 呼出}){throw new InvalidOperationException("この呼び出しはGUIで削除できません。");}置換=呼出.Expression.ToFullString();}
        this.ノード編集.Text=置換;return this.ノード反映Async();
    }
    private Task 列追加Async(){
        if(this.選択ノード is not{Kind:"Type"} ノード||this.解析結果?.SourceHash!=this.現在定義().Source.Hash()){throw new InvalidOperationException("列を追加するRecords型などを選んでください。");}
        var 入力=new 入力ダイアログ(this,"メンバー・列を追加",new 入力項目("C#宣言","public partial int NewColumn{get;}"));if(入力.ShowDialog()!=true){return Task.CompletedTask;}
        var ファイル=this.現在定義().Source.Files.Single(項目=>項目.Path==ノード.Path);var 範囲=ファイル.Text.Substring(ノード.Start,ノード.Length);var 位置=範囲.LastIndexOf('}');if(位置<0){throw new InvalidOperationException("型の本文がありません。");}
        var 更新=new SourceEditor().Apply(this.現在定義().Source,new(this.解析結果!.SourceHash,ノード.Path,ノード.Start+位置,0,"\n"+入力.値[0]+"\n"));this.ソースを反映(更新);return this.解析Async();
    }
    private void ソースを反映(SourceSnapshot 更新){this.編集定義=this.現在定義() with{Source=更新};this.未保存か=true;var ファイル=更新.Files.Single(項目=>項目.Path==this.選択ファイル);this.反映中か=true;try{this.コード.Text=ファイル.Text;}finally{this.反映中か=false;}}
    private Task 書出Async(){
        var 定義=this.現在定義();var 選択=new OpenFolderDialog{Title="C#と版情報の出力先"};if(選択.ShowDialog(this)!=true){return Task.CompletedTask;}
        SourceArchive.Export(選択.FolderName,new(定義,this.基準版,this.下書き版));this.状態.Text="C#と版情報を書き出しました。";return Task.CompletedTask;
    }
    private async Task 読込Async(){
        if(!this.破棄を確認("ソースを読み込む")){return;}
        var 選択=new OpenFileDialog{Filter="Seamless定義|*.seamless.json"};if(選択.ShowDialog(this)!=true){return;}var 書庫=SourceArchive.Import(選択.FileName);long 下書き=0;
        try{var 既存=await this.必須接続().CallAsync<Draft>("draft",書庫.Definition.Database,new{id=書庫.Definition.Id});if(MessageBox.Show(this,"個人の下書きが既にあります。取り込んだ内容で編集を始めますか？ 保存は別の操作です。","下書きの読込",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}下書き=既存.Revision;}catch(ManagementException 例外)when(例外.Code=="NotFound"){}
        this.定義を表示(書庫.Definition,下書き,書庫.BaseVersion);this.未保存か=true;this.状態.Text="C#ファイルを読み込みました。下書きの保存・適用は別の操作です。";await this.解析Async();
    }
    private Task DOT書出Async(){if(this.ビルド結果 is null||this.ビルド結果.Plans.Length==0){throw new InvalidOperationException("先に静的計画を生成してください。");}var 選択=new SaveFileDialog{Filter="DOTグラフ|*.dot",FileName="ExecutionPlan.dot"};if(選択.ShowDialog(this)==true){File.WriteAllText(選択.FileName,this.ビルド結果.Plans[0].ToDot());}return Task.CompletedTask;}
    private async Task 利用者管理Async(){
        var 一覧=await this.必須接続().CallAsync<Page<UserInfo>>("users","*",new{cursor=0});this.診断.Text=string.Join("\n",一覧.Items.Select(項目=>項目.Name+" "+string.Join(",",項目.Grants.Select(許可=>許可.Database+":"+許可.Permissions))));
        var 入力=new 入力ダイアログ(this,"利用者を作成・更新",new 入力項目("利用者名"),new 入力項目("新しいパスワード（既存のままなら空）",秘密か:true),new 入力項目("対象DB（全体は *）",this.DB名.Text),new 入力項目("権限: View,Execute,Edit,Validate,Apply,Manage","View,Execute")){入力検証=値=>入力ダイアログ.パスワードを検証(値,1,空なら変更なしか:true)};if(入力.ShowDialog()!=true){return;}var 値=入力.値;string? 秘密=値[1];if(秘密.Length==0){秘密=null;}
        await this.必須接続().CallAsync<JsonElement>("changeUser","*",new ChangeUserRequest(値[0],秘密,[new(値[2],Enum.Parse<ManagementPermission>(値[3]))]));this.状態.Text="利用者情報を更新しました。";
    }
    private async Task パスワード再設定Async(){
        var 入力=new 入力ダイアログ(this,"権限を維持してパスワードを再設定",new 入力項目("利用者名",this.利用者.Text),new 入力項目("新しいパスワード（12文字以上）",秘密か:true),new 入力項目("新しいパスワードの確認",秘密か:true)){入力検証=値=>入力ダイアログ.パスワードを検証(値,1,2)};if(入力.ShowDialog()!=true){return;}var 値=入力.値;
        await this.必須接続().CallAsync<JsonElement>("changePassword","*",new ChangePasswordRequest(値[0],値[1]));this.状態.Text="パスワードを再設定しました。対象利用者の以前のセッションは失効しました。";
        if(string.Equals(値[0],this.ログイン利用者?.Name,StringComparison.OrdinalIgnoreCase)){this.クライアント=null;this.ログイン利用者=null;this.接続案内.Text="パスワードを変更しました。新しいパスワードでログインしてください。";}
    }
    private const string DB雛形="""
        using Seamless.Database;
        namespace UserCode{
            internal static partial class DatabaseGenerator;
            public sealed partial class ManagedDb(DatabaseContainerOptions options):DatabaseContainer(options){
                public Table<Keys.Item,Records.Item> Items=>this.Table<Keys.Item,Records.Item>(row=>new(row.Id));
            }
        }
        namespace UserCode.Keys{public partial struct Item{public partial int Id{get;}}}
        namespace UserCode.Records{public partial struct Item{public partial int Id{get;}public partial string Name{get;}}}
        """;
    private const string クエリ雛形="""
        using System.Linq;
        using System.Collections.Generic;
        using Seamless.FLinq;
        using Seamless.Management.Server;
        namespace UserCode;
        public sealed class Query{
            [LinqIntercept(true)]
            public static IEnumerable<Records.Item> Run(ManagementExecutionContext context)=>
                context.GetDatabase<ManagedDb>().Items.Where(item=>item.Id>0);
        }
        """;
}
