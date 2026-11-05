using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private readonly ObservableCollection<CatalogDatabase> 表示DB=[];
    private readonly ObservableCollection<CatalogObject> 表示オブジェクト=[];
    private readonly TextBlock 概要=new(){FontSize=18,Margin=new Thickness(12),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black};
    private readonly TextBlock ブラウザー状態=new(){Margin=new Thickness(12),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black};
    private readonly TextBox 名前絞込=入力("");
    private readonly TextBox スキーマ絞込=入力("");
    private readonly ComboBox 種類絞込=new(){MinWidth=140,Margin=new Thickness(4),SelectedIndex=0};
    private readonly DataGrid DB表=new(){IsReadOnly=true,AutoGenerateColumns=false,MinHeight=90,MaxHeight=180,EnableRowVirtualization=true};
    private readonly DataGrid オブジェクト表=new(){IsReadOnly=true,AutoGenerateColumns=false,EnableRowVirtualization=true,EnableColumnVirtualization=true};
    private readonly ScrollViewer 属性領域=new(){VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Visibility=Visibility.Collapsed};
    private readonly ColumnDefinition 属性列=new(){Width=new GridLength(0)};
    private void 属性を表示(bool 表示するか){this.属性列.Width=new GridLength(0);this.属性領域.Visibility=Visibility.Collapsed;if(表示するか){this.属性列.Width=new GridLength(290);this.属性領域.Visibility=Visibility.Visible;}}
    private CatalogDatabase? ブラウザーDB;
    private CatalogRequest? 一覧要求;
    private long 一覧カーソル;
    private string? 選択スキーマ;
    private sealed class 表示値変換(string 項目):IValueConverter{
        public object Convert(object 値,Type 型,object 引数,System.Globalization.CultureInfo 言語){
            if(項目=="Schema"&&値 is string{Length:0}){return "スキーマなし";}
            if(項目=="IsSystem"&&値 is bool システムか){if(システムか){return "システム（参照用）";}return "利用者DB";}
            if(項目=="Version"&&値 is long 版&&版==0){return "—";}
            if(項目=="Kind"&&値 is string 種類){return 種類 switch{"Table"=>"テーブル","View"=>"ビュー","TableFunction"=>"テーブル関数","ScalarFunction"=>"スカラー関数","StoredProcedure"=>"ストアドプロシージャ","SystemTable"=>"システムテーブル",_=>種類};}return 値;
        }
        public object ConvertBack(object 値,Type 型,object 引数,System.Globalization.CultureInfo 言語)=>Binding.DoNothing;
    }
    private static void 表の列(DataGrid 表,string 表示,string プロパティ)=>表.Columns.Add(new DataGridTextColumn{Header=表示,Binding=new Binding(プロパティ){Converter=new 表示値変換(プロパティ)},MinWidth=160,Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
    private DockPanel ブラウザーを構築(){
        var 面=new DockPanel{Background=Brushes.White};DockPanel.SetDock(this.概要,Dock.Top);面.Children.Add(this.概要);
        var 操作=new WrapPanel();操作.Children.Add(押下("DB一覧を更新",()=>this.操作Async(this.一覧更新Async)));操作.Children.Add(押下("利用者DBを作成（下書き）",()=>this.操作Async(()=>this.新規Async(DefinitionKind.Database))));DockPanel.SetDock(操作,Dock.Top);面.Children.Add(操作);
        表の列(this.DB表,"データベース","Name");表の列(this.DB表,"稼働版","Version");表の列(this.DB表,"システムDB","IsSystem");this.DB表.ItemsSource=this.表示DB;this.DB表.SelectionChanged+=async(_,_)=>{if(this.DB表.SelectedItem is CatalogDatabase DB){await this.操作Async(()=>this.DBを表示Async(DB));}};DockPanel.SetDock(this.DB表,Dock.Top);面.Children.Add(this.DB表);
        this.スキーマ絞込.TextChanged+=(_,_)=>this.選択スキーマ=null;var 条件=new WrapPanel{Margin=new Thickness(8)};
        条件.Children.Add(new TextBlock{Text="名前に含む",VerticalAlignment=VerticalAlignment.Center});this.名前絞込.Width=160;条件.Children.Add(this.名前絞込);条件.Children.Add(new TextBlock{Text="スキーマ（完全一致）",VerticalAlignment=VerticalAlignment.Center});this.スキーマ絞込.Width=130;条件.Children.Add(this.スキーマ絞込);
        foreach(var 種類 in new[]{"すべて","Table","View","TableFunction","ScalarFunction","StoredProcedure","SystemTable"}){this.種類絞込.Items.Add(種類);}条件.Children.Add(this.種類絞込);
        条件.Children.Add(押下("絞り込む",()=>this.操作Async(()=>this.オブジェクトページAsync(true))));条件.Children.Add(押下("条件を解除",()=>this.操作Async(async()=>{this.選択スキーマ=null;this.名前絞込.Clear();this.スキーマ絞込.Clear();this.種類絞込.SelectedIndex=0;await this.オブジェクトページAsync(true);})));
        条件.Children.Add(押下("続きを取得",()=>this.操作Async(()=>this.オブジェクトページAsync(false))));条件.Children.Add(押下("選択対象の定義を開く",()=>this.操作Async(this.一覧から開くAsync)));DockPanel.SetDock(条件,Dock.Top);面.Children.Add(条件);
        DockPanel.SetDock(this.ブラウザー状態,Dock.Top);面.Children.Add(this.ブラウザー状態);
        表の列(this.オブジェクト表,"名前","Name");表の列(this.オブジェクト表,"スキーマ","Schema");表の列(this.オブジェクト表,"種類","Kind");表の列(this.オブジェクト表,"レコード型","RecordType");this.オブジェクト表.ItemsSource=this.表示オブジェクト;
        this.オブジェクト表.MouseDoubleClick+=async(_,_)=>await this.操作Async(this.一覧から開くAsync);this.オブジェクト表.KeyDown+=async(_,引数)=>{if(引数.Key==Key.Enter){引数.Handled=true;await this.操作Async(this.一覧から開くAsync);}};
        this.オブジェクト表.SelectionChanged+=async(_,_)=>{if(this.オブジェクト表.SelectedItem is CatalogObject 項目&&this.一覧要求 is{} 要求){await this.操作Async(async()=>{var 詳細=await this.必須接続().CallAsync<CatalogObjectDetails>("catalogDetails","",要求 with{ObjectId=項目.Id});this.属性を表示(true);this.オブジェクト詳細.Text=項目.Name+" / "+項目.Kind+"\n列:\n"+string.Join("\n",詳細.Columns.Select(列=>列.Name+" : "+列.Type))+"\nキー型: "+(項目.KeyType??"情報なし");});}};
        ScrollViewer.SetHorizontalScrollBarVisibility(this.オブジェクト表,ScrollBarVisibility.Auto);ScrollViewer.SetVerticalScrollBarVisibility(this.オブジェクト表,ScrollBarVisibility.Auto);面.Children.Add(this.オブジェクト表);return 面;
    }
    private void 概要を表示(){
        this.編集タブ.SelectedIndex=3;this.属性を表示(false);this.ブラウザーDB=null;this.一覧要求=null;this.表示オブジェクト.Clear();this.概要.Text="サーバーの概要 · "+this.ホスト.Text;
        this.ブラウザー状態.Text="表示中のDB："+this.表示DB.Count+" 件。DB名を選ぶとスキーマを展開せずにオブジェクト一覧を表示します。";
        if(this.利用者DBが未作成か){this.ブラウザー状態.Text="閲覧できる利用者DBはありません。新規DBを下書きとして作成するか、公開状態・閲覧権限を確認してください。システムDBを選ぶと管理用テーブルの構造を参照できます。";}
    }
    private async Task DBを表示Async(CatalogDatabase DB){
        this.状態.Text="参照中："+DB.Name+" · 稼働版 v"+DB.Version;
        this.ブラウザーDB=DB;this.選択スキーマ=null;this.名前絞込.Clear();this.スキーマ絞込.Clear();this.種類絞込.SelectedIndex=0;this.編集タブ.SelectedIndex=3;this.概要.Text=DB.Name+" · 稼働版 v"+DB.Version;if(DB.IsSystem){this.概要.Text="システムDB · 構造の参照専用";}await this.オブジェクトページAsync(true);
    }
    private async Task スキーマを表示Async(CatalogRequest 要求){
        var DB=this.表示DB.FirstOrDefault(DB=>DB.Id==要求.DatabaseId);if(DB is null){return;}this.ブラウザーDB=DB;this.スキーマ絞込.Text=要求.Schema??"";this.選択スキーマ=要求.Schema;this.名前絞込.Clear();this.種類絞込.SelectedIndex=0;this.編集タブ.SelectedIndex=3;await this.オブジェクトページAsync(true);
    }
    private async Task オブジェクトページAsync(bool 最初か){
        if(this.ブラウザーDB is null){this.ブラウザー状態.Text="先にDBを選択してください。";return;}
        if(最初か){this.表示オブジェクト.Clear();this.一覧カーソル=0;string? 種類=null;if(this.種類絞込.SelectedIndex>0){種類=this.種類絞込.SelectedItem as string;}string? スキーマ=this.選択スキーマ;if(this.スキーマ絞込.Text.Length>0){スキーマ=this.スキーマ絞込.Text;}this.一覧要求=new(this.ブラウザーDB.Id,Schema:スキーマ,Kind:種類,NameFilter:this.名前絞込.Text);}
        if(this.一覧カーソル<0){this.ブラウザー状態.Text="最後のページです。";return;}this.ブラウザー状態.Text="オブジェクトを取得中…";
        try{var ページ=await this.必須接続().CallAsync<CatalogPage<CatalogObject>>("catalogObjects","",this.一覧要求! with{Cursor=this.一覧カーソル});this.一覧要求=this.一覧要求! with{VersionToken=ページ.VersionToken};foreach(var 項目 in ページ.Items){this.表示オブジェクト.Add(項目);}this.一覧カーソル=-1;if(ページ.HasMore){this.一覧カーソル=ページ.NextCursor;}
            this.ブラウザー状態.Text="対象："+this.ブラウザーDB.Name+" / スキーマ："+(this.一覧要求.Schema??"すべて")+" / 種類："+(this.一覧要求.Kind??"すべて")+" / 名前："+(this.一覧要求.NameFilter??"")+" · 取得済み "+this.表示オブジェクト.Count+" 件";if(ページ.HasMore){this.ブラウザー状態.Text+="（続きがあります）";}if(this.表示オブジェクト.Count==0){this.ブラウザー状態.Text+="。該当するオブジェクトはありません。条件を解除して確認できます。";}
        }catch(Exception 例外){this.ブラウザー状態.Text="一覧を取得できません："+例外.Message+"。DBを選び直して再取得してください。";throw;}
    }
    private Task 一覧から開くAsync(){if(this.オブジェクト表.SelectedItem is not CatalogObject 項目||this.一覧要求 is null){return Task.CompletedTask;}return this.カタログ選択Async(this.一覧要求 with{ObjectId=項目.Id});}
}
