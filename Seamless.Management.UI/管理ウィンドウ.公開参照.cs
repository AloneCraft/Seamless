using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private PublishedDefinitionInfo? 公開参照;
    private readonly StackPanel 公開情報パネル=new(){Margin=new Thickness(8),Visibility=Visibility.Collapsed};
    private readonly TextBlock 公開情報=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
    private readonly Button 公開参照実行=new(){Content="公開問い合わせを実行…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,6,12,6)};
    private bool 対象の権限があるか(string DB,ManagementPermission 権限)=>this.ログイン利用者?.Grants.Any(許可=>(許可.Database=="*"||許可.Database==DB)&&(許可.Permissions&権限)==権限)==true;
    private DockPanel 結果領域を作る(){var 領域=new DockPanel();this.公開情報パネル.Children.Add(this.公開情報);this.公開情報パネル.Children.Add(this.公開参照実行);DockPanel.SetDock(this.公開情報パネル,Dock.Top);領域.Children.Add(this.公開情報パネル);領域.Children.Add(this.結果);this.公開参照実行.Click+=async(_,_)=>await this.操作Async(this.公開実行Async);return 領域;}
    private void 公開参照を消去(){this.公開参照=null;this.結果.Visibility=Visibility.Visible;this.公開情報.Text="";this.公開情報パネル.Visibility=Visibility.Collapsed;}
    private async Task 公開情報を開くAsync(DefinitionSummary 概要){
        if(!this.破棄を確認("公開情報を開く")){return;}
        var 情報=new PublishedDefinitionInfo(概要.Definition,概要.Version,"");if(this.サーバー能力.Contains("DefinitionInfoV1")){情報=await this.必須接続().CallAsync<PublishedDefinitionInfo>("definitionInfo",概要.Definition.Database,new{id=概要.Definition.Id});}
        this.表示を消去();this.結果.Visibility=Visibility.Collapsed;this.公開参照=情報;this.公開情報パネル.Visibility=Visibility.Visible;this.属性を表示(false);this.公開参照実行.Visibility=Visibility.Collapsed;
        this.公開情報.Text=情報.Definition.Database+" / "+情報.Definition.Id+" · 稼働版 v"+情報.Version+"\n公開メソッド: "+情報.Definition.EntryType+"."+情報.Definition.EntryMember;if(情報.Method is{} メソッド){this.公開情報.Text+="\n"+メソッド.MethodSignature;}
        this.公開情報.Text+="\nC#ソース・静的計画の閲覧には、このDBの閲覧権限と編集権限が必要です。";if(情報.Definition.Kind==DefinitionKind.Query){this.公開参照実行.Visibility=Visibility.Visible;this.公開参照実行.IsEnabled=this.対象の権限があるか(情報.Definition.Database,ManagementPermission.Execute);}
        this.ソース案内.Text="公開情報 · C#ソースの閲覧権限がありません。";this.状態.Text="公開情報を参照中: "+情報.Definition.Id;this.編集タブ.SelectedIndex=2;this.権限表示を更新();
    }
    private TreeViewItem 公開項目を作る(DefinitionSummary 概要,string 名前){var 項目=new TreeViewItem{Header=名前+" · v"+概要.Version,ToolTip=概要.Definition.Id,Tag=概要,Foreground=System.Windows.Media.Brushes.White};if(概要.Definition.Kind==DefinitionKind.Query){var メニュー=new ContextMenu();var 実行=new MenuItem{Header="公開問い合わせを実行…",IsEnabled=this.対象の権限があるか(概要.Definition.Database,ManagementPermission.Execute)};実行.Click+=async(_,_)=>await this.操作Async(()=>this.公開ヘッダーを実行Async(概要.Definition));メニュー.Items.Add(実行);項目.ContextMenu=メニュー;}return 項目;}
    private async Task 公開ヘッダーを実行Async(DefinitionHeader 定義){
        if(this.サーバー能力.Contains("DefinitionInfoV1")){定義=(await this.必須接続().CallAsync<PublishedDefinitionInfo>("definitionInfo",定義.Database,new{id=定義.Id})).Definition;}
        if(定義.Kind!=DefinitionKind.Query){throw new InvalidOperationException("公開問い合わせを選択してください。");}
        if(定義.WritesData&&MessageBox.Show(this,"この公開済み問い合わせは稼働DBを更新します。実行しますか？","更新問い合わせの実行",MessageBoxButton.OKCancel)!=MessageBoxResult.OK){return;}var 引数=this.実行引数();if(引数 is null){return;}await this.実行結果Async(new(定義.Id,null,引数.Value),定義.Database);
    }
}
