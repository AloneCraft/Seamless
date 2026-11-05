using System.IO;
using System.Text.Json;

namespace Seamless.Management.UI;

internal sealed record 画面検証設定(string ホスト,int ポート,string 利用者,string 証明書,string 定義ID,bool 自動接続か=false);
public sealed partial class 管理ウィンドウ{
    internal void 画面検証を終了(){this.未保存か=false;this.解析タイマー.Stop();this.接続情報タイマー.Stop();}
    internal void 画像配置(){this.図.SetInitialTransform();foreach(var 要素 in this.図.Entities){this.図.Invalidate(要素);}}
    private async Task 配置完了Async(){
        if(!this.配置中か&&this.待機図 is null){return;}var 完了=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void 配置後(object? 送信元,EventArgs 引数){this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,()=>{if(!this.配置中か&&this.待機図 is null){完了.TrySetResult();}});}
        this.図.LayoutComplete+=配置後;try{await 完了.Task.WaitAsync(TimeSpan.FromSeconds(30));}finally{this.図.LayoutComplete-=配置後;}
    }
    /// <summary>自分のウィンドウ内だけで通常の編集処理を通す。OS入力・フォーカスを使用しない。</summary>
    internal async Task 画面検証Async(string 設定ファイル,string 画像ファイル){
        _=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        this.初期設定ダイアログを作る(true,"validation-only.json").パスワード検証を試験する(3,4,画像ファイル+".password-validation.png");
        var 設定=JsonSerializer.Deserialize<画面検証設定>(File.ReadAllText(設定ファイル),ManagementJson.Options)??throw new InvalidDataException("画面検証の設定がありません。");
        if(設定.自動接続か){await this.ローカル接続を読むAsync();if(this.接続一覧.SelectedItem is not 接続設定||this.ホスト.Text!=設定.ホスト||this.ポート.Text!=設定.ポート.ToString()||this.証明書.Text!=設定.証明書){throw new InvalidOperationException("ローカル接続情報を自動表示できませんでした。");}}
        else{this.ホスト.Text=設定.ホスト;this.ポート.Text=設定.ポート.ToString();this.証明書.Text=設定.証明書;}
        this.利用者.Text=設定.利用者;this.パスワード.Password=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_UI_TEST_PASSWORD")??throw new InvalidOperationException("検証用のパスワードが必要です。");
        await this.接続Async();var 版=await this.必須接続().CallAsync<DefinitionVersion>("definition","",new{id=設定.定義ID});await this.定義を開くAsync(版);await this.配置完了Async();
        await this.一覧更新Async();await this.一覧更新Async();if(this.編集タブ.SelectedIndex!=3||this.接続設定パネル.IsExpanded){throw new InvalidOperationException("接続後の概要と接続設定の折り畳みを表示できませんでした。");}
        await this.DBを表示Async(this.表示DB.First(DB=>!DB.IsSystem));if(this.表示オブジェクト.Count==0){throw new InvalidOperationException("DB選択から一覧を表示できませんでした。");}this.名前絞込.Text="not-found-fixture";await this.オブジェクトページAsync(true);if(this.表示オブジェクト.Count!=0){throw new InvalidOperationException("名前の絞り込みが機能しません。");}this.名前絞込.Clear();await this.オブジェクトページAsync(true);起動.画像保存(this,画像ファイル+".browser.png");
        await this.定義を開くAsync(版);await this.配置完了Async();
        if(this.解析結果 is not{Current:true}){throw new InvalidOperationException("初回解析: "+this.診断.Text);}
        if(this.未保存か){throw new InvalidOperationException("読み込んだだけのソースが未保存になっています。");}
        if(this.稼働カタログ.Items.Count<2){throw new InvalidOperationException("利用者DBとシステムDBを表示できませんでした。");}
        var DB一覧=await this.必須接続().CallAsync<CatalogPage<CatalogDatabase>>("catalogDatabases","",new{});var DB=DB一覧.Items.First(項目=>!項目.IsSystem);
        var オブジェクト一覧=await this.必須接続().CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",new CatalogRequest(DB.Id,DB一覧.VersionToken));
        async Task 展開Async(System.Windows.Controls.TreeViewItem 項目){項目.IsExpanded=true;if(!this.展開処理.TryGetValue(項目,out var 処理)){throw new InvalidOperationException("ツリーの展開処理が開始されませんでした。");}await 処理;}
        var DB項目=this.稼働カタログ.Items.OfType<System.Windows.Controls.TreeViewItem>().First(項目=>項目.Header.ToString()!.StartsWith(DB.Name+" ·",StringComparison.Ordinal));await 展開Async(DB項目);
        var スキーマ項目=DB項目.Items.OfType<System.Windows.Controls.TreeViewItem>().First(項目=>項目.Header.ToString()=="スキーマなし");await 展開Async(スキーマ項目);
        var 表項目=スキーマ項目.Items.OfType<System.Windows.Controls.TreeViewItem>().First(項目=>項目.Header.ToString()=="テーブル");await 展開Async(表項目);if(!表項目.Items.OfType<System.Windows.Controls.TreeViewItem>().Any(項目=>項目.Tag is CatalogRequest{ObjectId:not null})){throw new InvalidOperationException("ツリーにテーブルが表示されませんでした。");}
        await this.カタログ選択Async(new(DB.Id,DB一覧.VersionToken,ObjectId:オブジェクト一覧.Items.First(項目=>項目.Kind=="Table").Id));
        if(this.ファイル一覧.Items.Count==0||!this.ソース案内.Text.StartsWith("稼働版",StringComparison.Ordinal)){throw new InvalidOperationException("オブジェクトから稼働版ソースを表示できませんでした。");}
        起動.画像保存(this,画像ファイル+".catalog.png");
        var 下書き=await this.必須接続().CallAsync<Draft>("draft","",new{id=設定.定義ID});this.定義を表示(下書き.Definition,下書き.Revision,下書き.BaseVersion);await this.解析Async();await this.配置完了Async();
        var 元=this.コード.Text;var 対象=this.解析結果.Nodes.First(ノード=>ノード.Kind=="Operator"&&ノード.Label=="Where");this.図ノード選択時(対象);
        this.ノード編集.Text=this.ノード編集.Text.Replace("row.Value>0","row.Value>1",StringComparison.Ordinal);await this.ノード反映Async();
        if(!this.コード.Text.Contains("row.Value>1",StringComparison.Ordinal)||!this.コード.Text.Contains("// preserved",StringComparison.Ordinal)){throw new InvalidOperationException("GUIからの編集で変更範囲を保持できませんでした。");}
        var 正常=this.コード.Text;var 最後の図=this.解析結果;this.コード.Text+="\n invalid incomplete {";this.解析タイマー.Stop();await this.解析Async();
        if(!ReferenceEquals(最後の図,this.解析結果)||!this.図の状態.Text.Contains("更新前",StringComparison.Ordinal)){throw new InvalidOperationException("入力途中に最後の図を保持できませんでした。");}
        this.コード.Text=正常;this.解析タイマー.Stop();await this.解析Async();await this.保存Async();
        await this.検証DB作成Async();await this.必須接続().CallAsync<JsonElement>("seed",this.DB名.Text,new SeedRequest(this.検証DBID!,"Rows",[ManagementJson.Element(new{Id=1,Value=7})]));
        await this.実行結果Async(new(設定.定義ID,this.検証DBID,ManagementJson.Element(new{}),DraftRevision:this.下書き版));
        if(this.結果.Items.Count!=1){throw new InvalidOperationException("検証結果を表示できませんでした。");}
        this.編集タブ.SelectedIndex=0;起動.画像保存(this,画像ファイル+".code.png");
        await this.計画Async();await this.配置完了Async();if(this.ビルド結果?.Plans.Length is null or 0){throw new InvalidOperationException("計画がありません: "+this.診断.Text);}
        var 全体数=this.図.Graph.NodeCount;var 終端=this.ビルド結果.Plans.SelectMany(文書=>文書.Nodes).Last(ノード=>ノード.Kind=="Fragment"&&ノード.Inputs.Length>0);this.図ノード選択時(終端);this.選択を折り畳む();await this.配置完了Async();if(this.図.Graph.NodeCount>=全体数){throw new InvalidOperationException("計画を折り畳めませんでした。");}this.選択を折り畳む();await this.配置完了Async();if(this.図.Graph.NodeCount!=全体数){throw new InvalidOperationException("計画を再展開できませんでした。");}
        var 対応=this.ビルド結果.Plans.SelectMany(文書=>文書.Nodes).First(ノード=>ノード.Kind=="SourceOperator");this.ファイル一覧.SelectedItem=対応.SourcePath;this.コード.CaretOffset=対応.Start;this.コード位置変更時();if(!this.図.Entities.Any(要素=>要素.DrawingObject is Microsoft.Msagl.Drawing.Node ノード&&ノード.Attr.FillColor.Equals(Microsoft.Msagl.Drawing.Color.Gold))){throw new InvalidOperationException("コードと計画を相互強調できませんでした。");}
        this.権限表示を更新();起動.画像保存(this,画像ファイル);File.WriteAllText(画像ファイル+".result.json",JsonSerializer.Serialize(new{接続=true,構造解析=true,範囲編集=true,入力途中保持=true,下書き保存=true,検証実行=true,計画数=this.ビルド結果.Plans.Length},ManagementJson.Options));
    }
}
