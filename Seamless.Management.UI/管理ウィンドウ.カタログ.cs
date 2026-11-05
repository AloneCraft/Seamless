using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private readonly Dictionary<TreeViewItem,Task> 展開処理=[];
    private 接続設定? 前回接続設定;
    private bool 接続選択中か;
    private void 接続設定選択時(){
        if(this.接続選択中か||this.接続一覧.SelectedItem is not 接続設定 接続||Equals(this.前回接続設定,接続)){return;}
        if(this.クライアント is not null&&!this.破棄を確認("接続先を変更する")){this.接続選択中か=true;try{this.接続一覧.SelectedItem=this.前回接続設定;}finally{this.接続選択中か=false;}return;}
        if(this.クライアント is not null){this.クライアント=null;this.ログイン利用者=null;this.表示を消去();this.定義一覧.Items.Clear();this.権限表示を更新();this.ソース案内.Text="接続先を変更しました。ログインしてください。";}
        this.前回接続設定=接続;this.ホスト.Text=接続.ホスト;this.ポート.Text=接続.ポート.ToString();this.利用者.Text=接続.利用者;this.証明書.Text=接続.証明書パス??"";this.パスワード.Clear();
    }
    private bool 破棄を確認(string 操作)=>!this.未保存か||MessageBox.Show(this,"未保存の編集を破棄して「"+操作+"」を行いますか？","未保存の編集",MessageBoxButton.OKCancel)==MessageBoxResult.OK;
    private async Task ローカル接続を読むAsync(){
        string? 管理領域=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT");
        try{if(this.起動設定ファイル is not null){管理領域=ManagementLaunchConfiguration.Read(this.起動設定ファイル).Root;}}
        catch(Exception 例外)when(例外 is ManagementException or IOException or JsonException){this.接続案内.Text=例外.Message;return;}
        if(string.IsNullOrWhiteSpace(管理領域)){this.接続案内.Text="ローカルサーバーは未設定です。「ローカル初期設定」で作成するか、管理領域を設定してください。";return;}
        if(this.接続情報読取中か||this.操作中数>0){return;}this.接続情報読取中か=true;
        try{
            var 情報=await Task.Run(()=>LocalServerConnection.Read(管理領域));
            if(情報 is null){
                this.接続案内.Text="指定した管理領域のサーバー起動を待っています。";
                if(this.ローカル接続設定 is not null&&ReferenceEquals(this.接続一覧.SelectedItem,this.ローカル接続設定)){this.クライアント=null;this.ログイン利用者=null;this.権限表示を更新();this.状態.Text="ローカルサーバー停止 · 再起動後にログインしてください。";}return;
            }
            if(this.ローカル接続==情報){return;}
            var 選択中か=this.ローカル接続設定 is not null&&ReferenceEquals(this.接続一覧.SelectedItem,this.ローカル接続設定);var 初回か=this.ローカル接続 is null;
            this.ローカル接続=情報;
            if(this.ローカル接続設定 is not null){this.接続設定一覧.Remove(this.ローカル接続設定);}
            this.ローカル接続設定=new("ローカル",情報.Host,情報.Port,this.利用者.Text,Path.Combine(管理領域,LocalServerConnection.CertificateFileName));this.接続設定一覧.Insert(0,this.ローカル接続設定);
            var 前回選択=this.接続一覧.SelectedItem;this.接続一覧.ItemsSource=null;this.接続一覧.ItemsSource=this.接続設定一覧;
            if((初回か&&前回選択 is null||選択中か)&&!this.未保存か){this.接続一覧.SelectedItem=this.ローカル接続設定;this.クライアント=null;this.ログイン利用者=null;this.権限表示を更新();}else{this.接続一覧.SelectedItem=前回選択;}
            this.接続案内.Text="ローカルサーバーを検出しました。パスワードを入力してログインしてください。";
            if(情報.PipeName is not null&&ReferenceEquals(this.接続一覧.SelectedItem,this.ローカル接続設定)&&!this.未保存か){await this.操作Async(()=>this.ローカルIPC接続Async(情報));}
        }catch(Exception 例外)when(例外 is IOException or UnauthorizedAccessException or JsonException or System.ComponentModel.Win32Exception){this.接続案内.Text="接続情報を取得できません："+例外.Message;}
        finally{this.接続情報読取中か=false;}
    }
    private static TreeViewItem カタログ項目(string 表示,object? 値=null)=>new(){Header=表示,Tag=値,Foreground=Brushes.White};
    private void 展開時に取得(TreeViewItem 項目,Func<Task> 取得){
        項目.Items.Add(カタログ項目("展開して取得"));var 取得済みか=false;
        項目.Expanded+=async(_,引数)=>{if(引数.OriginalSource!=項目||取得済みか){return;}引数.Handled=true;var 処理=this.操作Async(async()=>{項目.Items.Clear();try{await 取得();取得済みか=true;}catch{項目.Items.Clear();項目.Items.Add(カタログ項目("取得できません。閉じて再展開、または一覧を更新してください。"));throw;}});this.展開処理[項目]=処理;await 処理;};
    }
    private void 続きを追加(TreeViewItem 親,Func<Task> 次){var 続き=new TreeViewItem();続き.Header=押下("続きを取得",()=>this.操作Async(async()=>{await 次();親.Items.Remove(続き);}));親.Items.Add(続き);}
    private async Task カタログDBAsync(long カーソル=0){
        var ページ=await this.必須接続().CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{cursor=カーソル,versionToken=カーソル==0?"":this.カタログ版});this.カタログ版=ページ.VersionToken;
        if(カーソル==0){this.利用者DBが未作成か=ページ.Items.All(DB=>DB.IsSystem)&&!ページ.HasMore;}
        foreach(var DB in ページ.Items){this.表示DB.Add(DB);var 表示=DB.Name+" · v"+DB.Version;if(DB.IsSystem){表示="システムDB（参照用）";}var 項目=カタログ項目(表示,DB);項目.ToolTip=DB.Name;var 要求=new CatalogRequest(DB.Id,ページ.VersionToken);this.展開時に取得(項目,()=>this.カタログスキーマAsync(項目,要求));this.稼働カタログ.Items.Add(項目);}
        if(ページ.HasMore){this.続きを追加(this.稼働カタログ,()=>this.カタログDBAsync(ページ.NextCursor));}
        if(カーソル==0&&ページ.Items.Length==0){this.稼働カタログ.Items.Add(カタログ項目("閲覧できる稼働DBがありません。"));}
    }
    private async Task カタログスキーマAsync(TreeViewItem 親,CatalogRequest 要求){
        var ページ=await this.必須接続().CallAsync<CatalogPage<CatalogSchema>>("catalogSchemas","",要求);
        foreach(var スキーマ in ページ.Items){var 項目=カタログ項目(スキーマ.Name.Length==0?"スキーマなし":スキーマ.Name,要求 with{Schema=スキーマ.Name});this.展開時に取得(項目,()=>{
            foreach(var 種類 in new[]{("Table","テーブル"),("View","ビュー"),("TableFunction","テーブル関数"),("ScalarFunction","スカラー関数"),("StoredProcedure","ストアドプロシージャ"),("SystemTable","システムテーブル")}){
                var 種類項目=カタログ項目(種類.Item2);this.展開時に取得(種類項目,()=>this.カタログオブジェクトAsync(種類項目,要求 with{Cursor=0,Schema=スキーマ.Name,Kind=種類.Item1}));項目.Items.Add(種類項目);
            }return Task.CompletedTask;
        });親.Items.Add(項目);}
        if(ページ.HasMore){this.続きを追加(親,()=>this.カタログスキーマAsync(親,要求 with{Cursor=ページ.NextCursor}));}else if(親.Items.Count==0){親.Items.Add(カタログ項目("スキーマ・オブジェクトがありません。"));}
    }
    private async Task カタログオブジェクトAsync(TreeViewItem 親,CatalogRequest 要求){
        var ページ=await this.必須接続().CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",要求);
        foreach(var 項目 in ページ.Items){親.Items.Add(カタログ項目(項目.Name,要求 with{ObjectId=項目.Id}));}
        if(ページ.HasMore){this.続きを追加(親,()=>this.カタログオブジェクトAsync(親,要求 with{Cursor=ページ.NextCursor}));}else if(親.Items.Count==0){親.Items.Add(カタログ項目("この種類のオブジェクトはありません。"));}
    }
    private async Task カタログ選択Async(CatalogRequest 要求){
        if(!this.破棄を確認("オブジェクトを開く")){return;}
        var 詳細=await this.必須接続().CallAsync<CatalogObjectDetails>("catalogDetails","",要求);
        var ソース=await this.必須接続().CallAsync<CatalogObjectSource>("catalogSource","",要求);
        this.オブジェクト詳細.Text=詳細.Object.Kind+" · "+詳細.Object.Name+"\n型: "+詳細.Object.RecordType+"\nキー型: "+(詳細.Object.KeyType??"情報なし")+"\n列:\n"+string.Join("\n",詳細.Columns.Select(列=>列.Name+" : "+列.Type))+"\nキーメンバー:\n"+string.Join("\n",詳細.KeyMembers.Select(列=>列.Name+" : "+列.Type))+"\n引数:\n"+string.Join("\n",詳細.Parameters.Select(引数=>引数.Name+" : "+引数.Type))+"\n戻り値: "+(詳細.ReturnType??"該当なし");
        this.編集タブ.SelectedIndex=0;
        if(ソース.Definition is{} 版){this.定義を表示(版.Definition,0,版.Version);await this.解析Async();if(ソース.Location.Path is{} パス){this.ファイル一覧.SelectedItem=パス;this.コード.Select(ソース.Location.Start,ソース.Location.Length);this.コード.ScrollToLine(this.コード.Document.GetLineByOffset(ソース.Location.Start).LineNumber);}}
        else{this.表示を消去();this.システム表示か=要求.DatabaseId==システムDBID;this.ソース案内.Text="ソース未登録 · "+詳細.Object.Name+(this.システム表示か?" · システムDBの構造は参照専用です。":"");this.権限表示を更新();}
    }
    private const string システムDBID="system";
    private void 表示を消去(){
        this.反映中か=true;try{this.編集定義=null;this.解析結果=null;this.ビルド結果=null;this.未保存か=false;this.システム表示か=false;this.計画表示か=false;this.選択ノード=null;this.選択計画=null;this.ファイル一覧.ItemsSource=null;this.選択ファイル=null;this.コード.Text="";this.定義ID.Text="";this.DB名.Text="";this.入口型.Text="";this.入口メソッド.Text="";this.更新定義.IsChecked=false;this.ノード編集.Text="";this.結果.ItemsSource=null;this.図を更新();}finally{this.反映中か=false;}
    }
}
