using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using DrawingNode=Microsoft.Msagl.Drawing.Node;
using DrawingColor=Microsoft.Msagl.Drawing.Color;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ:Window{
    private readonly TextEditor コード=new(){ShowLineNumbers=true,FontFamily=new FontFamily("Consolas"),FontSize=14,Background=Brushes.White,Foreground=Brushes.Black,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,SyntaxHighlighting=HighlightingManager.Instance.GetDefinition("C#")};
    private readonly Expander 接続設定パネル=new(){Header="接続設定（TCP / TLS）",IsExpanded=true,Foreground=Brushes.White};
    private readonly RowDefinition 接続設定行=new(){Height=new GridLength(0.58,GridUnitType.Star)};
    private readonly TextBox ホスト=入力("localhost");
    private readonly TextBox ポート=入力("14336");
    private readonly TextBox 利用者=入力("admin");
    private readonly PasswordBox パスワード=new(){Padding=new Thickness(6),Margin=new Thickness(0,3,0,8)};
    private readonly TextBox 証明書=入力("");
    private readonly ComboBox 接続一覧=new(){Margin=new Thickness(0,4,0,10),MinHeight=30};
    private readonly ComboBox ファイル一覧=new(){MinWidth=200,Margin=new Thickness(3)};
    private readonly StackPanel ソース選択領域=new(){Orientation=System.Windows.Controls.Orientation.Horizontal,Visibility=Visibility.Collapsed};
    private readonly TextBlock 接続案内=new(){Text="未接続：接続先を設定してログインしてください。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4)};
    private readonly TextBlock ソース案内=new(){Text="未接続です。ログイン後、DB・オブジェクトまたは定義を選択してください。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8),Foreground=Brushes.Black};
    private readonly TextBox オブジェクト詳細=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=100,Foreground=Brushes.Black,Background=Brushes.White};
    private readonly DispatcherTimer 接続情報タイマー=new(){Interval=TimeSpan.FromSeconds(1)};
    private LocalServerConnection? ローカル接続;
    private 接続設定? ローカル接続設定;
    private bool 接続情報読取中か,システム表示か;
    private string[] サーバー能力=[];
    private string カタログ版="";
    private TreeViewItem 稼働カタログ=new(){Header="データベース",IsExpanded=true,Foreground=Brushes.White};
    private bool 利用者DBが未作成か;
    private TreeViewItem 公開定義一覧=new(){Header="公開済み定義",IsExpanded=true,Foreground=Brushes.White};
    private readonly TreeView 定義一覧=new(){Background=色("#15263C"),Foreground=Brushes.White,BorderThickness=new Thickness(0)};
    private readonly TextBox 定義ID=入力("");
    private readonly TextBox DB名=入力("");
    private readonly TextBox 入口型=入力("");
    private readonly TextBox 入口メソッド=入力("Run");
    private readonly CheckBox 更新定義=new(){Content="データを更新する問い合わせ",Foreground=Brushes.White,Margin=new Thickness(0,6,0,10)};
    private readonly TextBlock 状態=new(){Text="未接続 · 保存・適用は行われていません",Margin=new Thickness(12,4,12,4),Foreground=Brushes.White};
    private readonly TextBlock 図の状態=new(){Text="問い合わせ・スキーマの構造",Margin=new Thickness(8),Foreground=Brushes.Black};
    private readonly TextBox 診断=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Background=色("#132137"),Foreground=Brushes.White,BorderThickness=new Thickness(0),Padding=new Thickness(12)};
    private readonly TextBox ノード編集=new(){AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,FontFamily=new FontFamily("Consolas")};
    private readonly TextBox 検索=入力("");
    private readonly RowDefinition 診断行=new(){Height=GridLength.Auto};
    private readonly DataGrid 結果=new(){IsReadOnly=true,AutoGenerateColumns=true,EnableRowVirtualization=true,EnableColumnVirtualization=true,Background=Brushes.White,Foreground=Brushes.Black};
    private readonly GraphViewer 図=new(){RunLayoutAsync=true,LayoutEditingEnabled=false};
    private readonly DockPanel 図の領域=new(){Background=Brushes.White};
    private readonly TabControl 編集タブ=new();
    private readonly ProgressBar 進捗=new(){Height=3,IsIndeterminate=true,Visibility=Visibility.Collapsed};
    private readonly DispatcherTimer 解析タイマー=new(){Interval=TimeSpan.FromMilliseconds(700)};
    private readonly List<接続設定> 接続設定一覧=[];
    private readonly List<Button> 操作ボタン=[];
    private readonly HashSet<string> 折り畳み=[];
    private readonly Dictionary<string,TreeViewItem> DB項目=new(StringComparer.Ordinal);
    private TreeViewItem 個人下書き=new(){Header="個人の下書き",IsExpanded=true,Foreground=Brushes.White};
    private long 下書きカーソル;
    private readonly string 設定ディレクトリ;
    private ManagementClient? クライアント;
    private UserInfo? ログイン利用者;
    private Definition? 編集定義;
    private AnalysisResult? 解析結果;
    private BuildResult? ビルド結果;
    private EditorNode? 選択ノード;
    private ExecutionPlanNode? 選択計画;
    private (Graph 図,string? ハッシュ)? 待機図;
    private string? 表示図ハッシュ;
    private long 下書き版,基準版,定義カーソル;
    private string? 選択ファイル,保存済みハッシュ,検証DBID;
    private Guid? 適用操作ID;
    private bool 反映中か,未保存か,計画表示か,配置中か;
    private int 操作中数;
    private string? 起動設定ファイル;
    public 管理ウィンドウ(string? 起動設定ファイル=null){
        this.起動設定ファイル=起動設定ファイル;
        if(this.起動設定ファイル is null&&Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_ROOT") is null){var 候補=this.起動設定の場所();if(File.Exists(候補)){this.起動設定ファイル=候補;}}
        this.Title="Seamless Management";this.Width=1600;this.Height=1000;this.MinWidth=1100;this.MinHeight=700;this.Background=色("#101C2E");this.Foreground=Brushes.White;
        this.設定ディレクトリ=Environment.GetEnvironmentVariable("SEAMLESS_MANAGEMENT_HOME")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Seamless","Management");
        this.Content=this.画面を構築();
        this.図.BindToPanel(this.図の領域);
        this.図.LayoutStarted+=(_,_)=>this.配置中か=true;
        this.図.LayoutComplete+=(_,_)=>{this.配置中か=false;this.Dispatcher.BeginInvoke(DispatcherPriority.Render,()=>{if(this.配置中か){return;}if(this.待機図 is{} 待機){this.待機図=null;this.表示図ハッシュ=待機.ハッシュ;this.図.Graph=待機.図;}else{this.図.SetInitialTransform();}});};
        this.図.GraphCanvas.SizeChanged+=(_,_)=>{if(!this.配置中か){this.図.SetInitialTransform();}};
        this.図.MouseDown+=(_,_)=>{if(this.図.ObjectUnderMouseCursor?.DrawingObject is DrawingNode ノード){this.図ノード選択時(ノード.UserData);}};
        this.コード.TextChanged+=this.コード変更時;
        this.入口型.TextChanged+=(_,_)=>{if(!this.反映中か&&this.編集定義 is not null){this.未保存か=true;}};
        this.入口メソッド.TextChanged+=(_,_)=>{if(!this.反映中か&&this.編集定義 is not null){this.未保存か=true;}};
        this.更新定義.Checked+=(_,_)=>{if(!this.反映中か){this.未保存か=true;}};this.更新定義.Unchecked+=(_,_)=>{if(!this.反映中か){this.未保存か=true;}};
        this.コード.TextArea.Caret.PositionChanged+=(_,_)=>this.コード位置変更時();
        this.定義一覧.SelectedItemChanged+=async(_,_)=>{if(this.定義一覧.SelectedItem is TreeViewItem{Tag:DefinitionSummary 版}){await this.操作Async(()=>this.定義を取得して開くAsync(版));}else if(this.定義一覧.SelectedItem is TreeViewItem{Tag:DraftSummary 下書き}){await this.操作Async(()=>this.下書きを開くAsync(下書き));}else if(this.定義一覧.SelectedItem is TreeViewItem{Tag:CatalogRequest{ObjectId:not null} 要求}){await this.操作Async(()=>this.カタログ選択Async(要求));}};
        this.ファイル一覧.SelectionChanged+=(_,_)=>this.ファイル選択時();
        this.編集タブ.SelectionChanged+=(_,引数)=>{if(ReferenceEquals(引数.OriginalSource,this.編集タブ)){this.ソース選択領域.Visibility=Visibility.Collapsed;if(this.編集タブ.SelectedIndex is 0 or 1){this.ソース選択領域.Visibility=Visibility.Visible;}this.権限表示を更新();}};
        this.定義一覧.SelectedItemChanged+=async(_,_)=>{if(this.定義一覧.SelectedItem is TreeViewItem{Tag:CatalogDatabase DB}){await this.操作Async(()=>this.DBを表示Async(DB));}else if(this.定義一覧.SelectedItem is TreeViewItem{Tag:CatalogRequest{ObjectId:null} 要求}){await this.操作Async(()=>this.スキーマを表示Async(要求));}};
        this.接続一覧.SelectionChanged+=(_,_)=>this.接続設定選択時();
        this.解析タイマー.Tick+=async(_,_)=>{this.解析タイマー.Stop();if(this.操作中数==0&&this.編集定義 is not null){await this.操作Async(this.解析Async);} };
        this.検索.TextChanged+=(_,_)=>this.図を更新();
        this.接続設定を読む();
        var 前回操作=Path.Combine(this.設定ディレクトリ,"last-operation.txt");if(File.Exists(前回操作)&&Guid.TryParse(File.ReadAllText(前回操作),out var 操作ID)){this.適用操作ID=操作ID;}
        AutomationProperties.SetAutomationId(this.コード,"コード編集");AutomationProperties.SetAutomationId(this.状態,"管理状態");AutomationProperties.SetAutomationId(this.診断,"診断表示");
        this.診断.TextChanged+=(_,_)=>{this.診断.Visibility=Visibility.Collapsed;this.診断行.Height=GridLength.Auto;if(this.診断.Text.Length>0){this.診断.Visibility=Visibility.Visible;this.診断行.Height=new GridLength(150);}};this.診断.Visibility=Visibility.Collapsed;
        this.権限表示を更新();
        this.接続情報タイマー.Tick+=async(_,_)=>await this.ローカル接続を読むAsync();
        this.Loaded+=async(_,_)=>{this.接続情報タイマー.Start();await this.ローカル接続を読むAsync();};
        this.Closing+=(_,引数)=>{if(!this.破棄を確認("画面を閉じる")){引数.Cancel=true;}};
        this.Closed+=(_,_)=>{this.接続情報タイマー.Stop();this.解析タイマー.Stop();this.自分のサーバーに停止を要求();};
    }
    private Grid 画面を構築(){
        var 全体=new Grid{Background=色("#101C2E")};全体.RowDefinitions.Add(new(){Height=new GridLength(62)});全体.RowDefinitions.Add(new(){Height=GridLength.Auto});全体.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});全体.RowDefinitions.Add(this.診断行);全体.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var 見出し=new DockPanel{Margin=new Thickness(16,12,16,8)};
        見出し.Children.Add(new TextBlock{Text="SEAMLESS",FontSize=25,FontWeight=FontWeights.Bold,Foreground=色("#79D6E8")});
        見出し.Children.Add(new TextBlock{Text="  /  ネイティブ統合管理",FontSize=18,Margin=new Thickness(12,7,0,0)});全体.Children.Add(見出し);
        var 操作列=new WrapPanel{Margin=new Thickness(8,0,8,8)};
        void 操作(string 名前,Func<Task> 処理,ManagementPermission 権限){var ボタン=押下(名前,()=>this.操作Async(処理));ボタン.Tag=権限;this.操作ボタン.Add(ボタン);操作列.Children.Add(ボタン);}
        操作("新規DB",()=>this.新規Async(DefinitionKind.Database),ManagementPermission.Edit);
        操作("新規クエリ",()=>this.新規Async(DefinitionKind.Query),ManagementPermission.Edit);
        操作("保存",this.保存Async,ManagementPermission.Edit);操作("解析",this.解析Async,ManagementPermission.Validate);
        操作("静的計画",this.計画Async,ManagementPermission.View);操作("検証DB作成",this.検証DB作成Async,ManagementPermission.Validate);
        操作("検証DB選択・破棄",this.検証管理Async,ManagementPermission.Validate);
        操作("検証データ",this.検証データAsync,ManagementPermission.Validate);操作("検証実行",this.検証実行Async,ManagementPermission.Validate);
        操作("公開版を実行",this.公開実行Async,ManagementPermission.Execute);操作("差分・適用",this.適用Async,ManagementPermission.Apply);
        操作("履歴",this.履歴Async,ManagementPermission.View);操作("履歴の続き",this.履歴続きAsync,ManagementPermission.View);操作("操作結果を照会",this.操作結果Async,ManagementPermission.View);操作("利用者",this.利用者管理Async,ManagementPermission.Manage);
        操作("C#書出",this.書出Async,ManagementPermission.View);操作("C#読込",this.読込Async,ManagementPermission.Edit);
        操作("パスワード再設定",this.パスワード再設定Async,ManagementPermission.Manage);
        var 取消=押下("適用の取消要求",this.取消Async);操作列.Children.Add(取消);
        Grid.SetRow(操作列,1);全体.Children.Add(操作列);
        var 本体=new Grid();本体.ColumnDefinitions.Add(new(){Width=new GridLength(300)});本体.ColumnDefinitions.Add(new(){Width=new GridLength(5)});本体.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});本体.ColumnDefinitions.Add(new(){Width=new GridLength(5)});本体.ColumnDefinitions.Add(this.属性列);
        var 接続列=new Grid{Margin=new Thickness(8,0,4,0)};接続列.RowDefinitions.Add(new(){Height=GridLength.Auto});接続列.RowDefinitions.Add(this.接続設定行);接続列.RowDefinitions.Add(new(){Height=new GridLength(0.42,GridUnitType.Star)});
        var 起動欄=new StackPanel{Margin=new Thickness(8,0,8,0)};起動欄.Children.Add(this.接続案内);var 起動ボタン列=new WrapPanel();起動ボタン列.Children.Add(押下("ローカル初期設定",()=>this.操作Async(this.ローカル初期設定Async)));起動ボタン列.Children.Add(押下("サーバー起動",()=>this.操作Async(this.ローカルサーバー起動Async)));起動欄.Children.Add(起動ボタン列);接続列.Children.Add(起動欄);
        var 接続欄=new StackPanel{Margin=new Thickness(8)};見出しを追加(接続欄,"接続先");接続欄.Children.Add(this.接続一覧);
        項目を追加(接続欄,"ホスト",this.ホスト);項目を追加(接続欄,"TLSポート",this.ポート);項目を追加(接続欄,"利用者",this.利用者);項目を追加(接続欄,"パスワード",this.パスワード);
        項目を追加(接続欄,"信頼する証明書（任意）",this.証明書);接続欄.Children.Add(押下("証明書を選ぶ",()=>{var 選択=new Microsoft.Win32.OpenFileDialog{Filter="証明書|*.cer;*.crt;*.pem"};if(選択.ShowDialog(this)==true){this.証明書.Text=選択.FileName;}return Task.CompletedTask;}));
        接続欄.Children.Add(押下("接続・ログイン",()=>this.操作Async(this.接続Async)));接続欄.Children.Add(押下("パスワードを忘れた場合",()=>{this.診断.Text="別の管理者がログインできる場合は「パスワード再設定」を使用してください。最後の管理者もログインできない場合は、サーバーを停止し、CLIで reset-password --root <管理領域> --user <利用者> --backup <空の退避先> を実行してください。DBを再初期化しないでください。";return Task.CompletedTask;}));接続欄.Children.Add(押下("接続先を保存",()=>this.操作Async(this.接続保存Async)));
        接続欄.Children.Add(押下("一覧を更新",()=>this.操作Async(this.一覧更新Async)));接続欄.Children.Add(押下("定義の次のページ",()=>this.操作Async(()=>this.定義ページAsync(this.定義カーソル))));接続欄.Children.Add(押下("下書きの続き",()=>this.操作Async(()=>this.下書きページAsync(this.下書きカーソル))));見出しを追加(接続欄,"DB・定義");
        var 接続スクロール=new ScrollViewer{Content=接続欄,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};this.接続設定パネル.Content=接続スクロール;this.接続設定パネル.Expanded+=(_,_)=>this.接続設定行.Height=new GridLength(0.58,GridUnitType.Star);this.接続設定パネル.Collapsed+=(_,_)=>this.接続設定行.Height=GridLength.Auto;Grid.SetRow(this.接続設定パネル,1);接続列.Children.Add(this.接続設定パネル);ScrollViewer.SetHorizontalScrollBarVisibility(this.定義一覧,ScrollBarVisibility.Auto);ScrollViewer.SetVerticalScrollBarVisibility(this.定義一覧,ScrollBarVisibility.Auto);Grid.SetRow(this.定義一覧,2);接続列.Children.Add(this.定義一覧);本体.Children.Add(接続列);
        var 分割=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,Background=色("#29415C")};Grid.SetColumn(分割,1);本体.Children.Add(分割);
        var 中央=new DockPanel();var ファイル列=this.ソース選択領域;ファイル列.Children.Add(new TextBlock{Text="ソース",Margin=new Thickness(8)});ファイル列.Children.Add(this.ファイル一覧);DockPanel.SetDock(ファイル列,Dock.Top);中央.Children.Add(ファイル列);
        var コード欄=new DockPanel{Background=Brushes.White};DockPanel.SetDock(this.ソース案内,Dock.Top);コード欄.Children.Add(this.ソース案内);コード欄.Children.Add(this.コード);this.編集タブ.Items.Add(new TabItem{Header="C#コード",Content=コード欄});
        var 図面=new DockPanel();DockPanel.SetDock(this.図の状態,Dock.Top);図面.Children.Add(this.図の状態);図面.Children.Add(this.図の領域);
        this.編集タブ.Items.Add(new TabItem{Header="構造 / 静的計画",Content=図面});this.編集タブ.Items.Add(new TabItem{Header="実行結果",Content=this.結果});中央.Children.Add(this.編集タブ);Grid.SetColumn(中央,2);本体.Children.Add(中央);
        this.編集タブ.Items.Add(new TabItem{Header="概要・オブジェクト",Content=this.ブラウザーを構築()});this.編集タブ.SelectedIndex=3;
        var 分割右=new GridSplitter{Width=5,HorizontalAlignment=HorizontalAlignment.Stretch,Background=色("#29415C")};Grid.SetColumn(分割右,3);本体.Children.Add(分割右);
        var 属性=new StackPanel{Margin=new Thickness(12)};見出しを追加(属性,"オブジェクト詳細");属性.Children.Add(this.オブジェクト詳細);見出しを追加(属性,"定義");項目を追加(属性,"定義ID",this.定義ID);項目を追加(属性,"DB名",this.DB名);項目を追加(属性,"入口型",this.入口型);項目を追加(属性,"入口メソッド",this.入口メソッド);属性.Children.Add(this.更新定義);
        見出しを追加(属性,"図の検索・編集");項目を追加(属性,"検索",this.検索);属性.Children.Add(押下("選択ノードを折り畳む / 展開",()=>{this.選択を折り畳む();return Task.CompletedTask;}));
        項目を追加(属性,"選択部分（カスタムコードも保持）",this.ノード編集);属性.Children.Add(押下("この部分をコードへ反映",()=>this.操作Async(this.ノード反映Async)));
        属性.Children.Add(押下("選択演算子にWhereを追加",()=>this.操作Async(()=>this.演算子変更Async(false))));属性.Children.Add(押下("選択演算子を取り除く",()=>this.操作Async(()=>this.演算子変更Async(true))));
        属性.Children.Add(押下("メンバー・列を追加",()=>this.操作Async(this.列追加Async)));属性.Children.Add(押下("共有版との差分・基準更新",()=>this.操作Async(this.基準更新Async)));属性.Children.Add(押下("DOTを書き出す",()=>this.操作Async(this.DOT書出Async)));
        this.属性領域.Content=属性;Grid.SetColumn(this.属性領域,4);本体.Children.Add(this.属性領域);
        Grid.SetRow(本体,2);全体.Children.Add(本体);
        Grid.SetRow(this.診断,3);全体.Children.Add(this.診断);var 下=new DockPanel();DockPanel.SetDock(this.進捗,Dock.Top);下.Children.Add(this.進捗);下.Children.Add(this.状態);Grid.SetRow(下,4);全体.Children.Add(下);return 全体;
    }
    private static SolidColorBrush 色(string 値)=>new((System.Windows.Media.Color)ColorConverter.ConvertFromString(値));
    private static TextBox 入力(string 初期値)=>new(){Text=初期値,Foreground=Brushes.Black,Background=Brushes.White,Padding=new Thickness(6),Margin=new Thickness(0,3,0,8)};
    private static void 項目を追加(Panel 親,string 名前,UIElement 要素){親.Children.Add(new TextBlock{Text=名前,FontSize=12,Foreground=色("#A9BED4")});親.Children.Add(要素);}
    private static void 見出しを追加(Panel 親,string 名前)=>親.Children.Add(new TextBlock{Text=名前,FontSize=15,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,8),Foreground=色("#79D6E8")});
    private static Button 押下(string 名前,Func<Task> 処理){
        var ボタン=new Button{Content=名前,Padding=new Thickness(9,6,9,6),Margin=new Thickness(3),Background=色("#294565"),Foreground=Brushes.White,BorderBrush=色("#3B5A7C")};AutomationProperties.SetAutomationId(ボタン,名前);
        var 枠=new FrameworkElementFactory(typeof(Border));枠.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});枠.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));枠.SetValue(Border.PaddingProperty,new Thickness(9,6,9,6));
        var 内容=new FrameworkElementFactory(typeof(ContentPresenter));内容.SetValue(ContentPresenter.HorizontalAlignmentProperty,HorizontalAlignment.Center);枠.AppendChild(内容);var 型=new ControlTemplate(typeof(Button)){VisualTree=枠};var 無効=new Trigger{Property=IsEnabledProperty,Value=false};無効.Setters.Add(new Setter(OpacityProperty,0.4));型.Triggers.Add(無効);ボタン.Template=型;
        ボタン.Click+=async(_,_)=>await 処理();return ボタン;
    }
    private async Task 操作Async(Func<Task> 処理){
        if(this.操作中数>0){this.状態.Text="現在の操作の完了を待ってください。適用の取消要求は実行できます。";return;}
        this.操作中数++;this.進捗.Visibility=Visibility.Visible;this.権限表示を更新();
        try{await 処理();}catch(Exception 例外){this.状態.Text=例外 switch{ManagementException{Code:"Authentication"}=>"認証が必要です。利用者名・パスワードを確認してログインしてください。",ManagementException{Code:"CatalogChanged"}=>"一覧の版が変わりました。「一覧を更新」で取得し直してください。",System.Security.Authentication.AuthenticationException=>"証明書を検証できません。信頼する証明書とホスト名を確認してください。",System.Net.Sockets.SocketException=>"サーバーへ接続できません。起動状態と接続先を確認してください。",_=>"操作に失敗しました"};this.診断.Text=例外.Message;}
        finally{if(this.クライアント is null&&this.編集定義 is null){this.ソース案内.Text="未接続です。サーバーの起動状態を確認し、ログインしてください。";}this.操作中数--;if(this.操作中数==0){this.進捗.Visibility=Visibility.Collapsed;}this.権限表示を更新();if(this.未保存か&&this.編集定義 is not null&&this.解析結果?.SourceHash!=this.編集定義.Source.Hash()){this.解析タイマー.Start();}}
    }
    private void 権限表示を更新(){
        foreach(var ボタン in this.操作ボタン){if(ボタン.Tag is ManagementPermission 権限){var 対象=this.DB名.Text;if(権限==ManagementPermission.Manage){対象="*";}ボタン.IsEnabled=this.操作中数==0&&this.ログイン利用者 is{} 利用者&&利用者.Grants.Any(許可=>(許可.Database=="*"||許可.Database==対象||対象.Length==0)&&(許可.Permissions&権限)==権限);}}
        this.コード.IsReadOnly=this.システム表示か||!this.権限があるか(ManagementPermission.Edit);
        this.入口型.IsReadOnly=this.コード.IsReadOnly;this.入口メソッド.IsReadOnly=this.コード.IsReadOnly;
        this.更新定義.IsEnabled=!this.コード.IsReadOnly;
        foreach(var ボタン in this.操作ボタン){if(ボタン.Content is string 名前&&名前 is not "新規DB" and not "新規クエリ" and not "利用者" and not "パスワード再設定"){ボタン.Visibility=this.編集定義 is null||this.編集タブ.SelectedIndex==3?Visibility.Collapsed:Visibility.Visible;}}
        foreach(var ボタン in this.操作ボタン.Where(項目=>項目.Content as string=="C#書出")){ボタン.IsEnabled=this.編集定義 is not null&&this.操作中数==0;}
    }
    private bool 権限があるか(ManagementPermission 権限)=>this.ログイン利用者?.Grants.Any(許可=>(許可.Database=="*"||許可.Database==this.DB名.Text)&&(許可.Permissions&権限)==権限)==true;
    private void コード変更時(object? 送信元,EventArgs 引数){
        if(this.反映中か||this.編集定義 is null||this.選択ファイル is null){return;}
        this.編集定義=this.編集定義 with{Source=this.編集定義.Source with{Files=this.編集定義.Source.Files.Select(ファイル=>{if(ファイル.Path==this.選択ファイル){return ファイル with{Text=this.コード.Text};}return ファイル;}).ToArray()}};
        this.未保存か=true;this.図の状態.Text="更新前の図 · コードを解析中";this.解析タイマー.Stop();this.解析タイマー.Start();
    }
    private void ファイル選択時(){
        if(this.編集定義 is null||this.ファイル一覧.SelectedItem is not string パス){return;}var 反映前=this.反映中か;this.反映中か=true;try{this.選択ファイル=パス;this.コード.Text=this.編集定義.Source.Files.Single(ファイル=>ファイル.Path==パス).Text;}finally{this.反映中か=反映前;}
    }
    private void コード位置変更時(){
        if(this.計画表示か&&this.ビルド結果 is not null&&this.表示図ハッシュ==this.現在定義().Source.Hash()){
            foreach(var 要素 in this.図.Entities){if(要素.DrawingObject is DrawingNode{UserData:ExecutionPlanNode 計画} 図ノード){図ノード.Attr.FillColor=計画色(計画);if(計画.SourcePath==this.選択ファイル&&計画.Start<=this.コード.CaretOffset&&this.コード.CaretOffset<計画.Start+計画.Length){図ノード.Attr.FillColor=DrawingColor.Gold;}this.図.Invalidate(要素);}}return;
        }
        if(this.反映中か||this.解析結果 is null||this.編集定義?.Source.Hash()!=this.解析結果.SourceHash){return;}
        var 位置=this.コード.CaretOffset;var 選択=this.解析結果.Nodes.Where(ノード=>ノード.Path==this.選択ファイル&&ノード.Start<=位置&&位置<ノード.Start+ノード.Length).OrderBy(ノード=>ノード.Length).FirstOrDefault();if(選択 is not null){this.選択ノード=選択;this.ノード編集.Text=this.コード.Document.GetText(選択.Start,選択.Length);this.強調(選択);}
    }
    private void 図ノード選択時(object? 選択){
        if(選択 is EditorNode ノード){this.ノード編集.Text="";if(this.表示図ハッシュ!=this.編集定義?.Source.Hash()||this.配置中か){this.状態.Text="図は更新前です。解析結果を待ってください。";return;}
            this.選択ノード=ノード;this.ファイル一覧.SelectedItem=ノード.Path;if(ノード.Start+ノード.Length<=this.コード.Text.Length){this.反映中か=true;try{this.コード.Select(ノード.Start,ノード.Length);this.コード.ScrollToLine(this.コード.Document.GetLineByOffset(ノード.Start).LineNumber);this.ノード編集.Text=this.コード.Document.GetText(ノード.Start,ノード.Length);}finally{this.反映中か=false;}}this.強調(ノード);
        }else if(選択 is ExecutionPlanNode 計画){this.選択計画=計画;if(this.ビルド結果?.SourceHash!=this.現在定義().Source.Hash()){this.状態.Text="計画は更新前です。現在のコードの計画を取得してください。";return;}this.診断.Text=計画.Kind+"\n"+string.Join("\n",計画.Properties.Select(項目=>項目.Key+": "+項目.Value));if(計画.SourcePath is not null&&this.編集定義?.Source.Files.Any(ファイル=>ファイル.Path==計画.SourcePath)==true){this.ファイル一覧.SelectedItem=計画.SourcePath;if(計画.Start+計画.Length<=this.コード.Text.Length){this.コード.Select(計画.Start,計画.Length);}}}
    }
    private void 強調(EditorNode 選択){foreach(var 要素 in this.図.Entities){if(要素.DrawingObject is DrawingNode ノード&&ノード.UserData is EditorNode 対応){ノード.Attr.FillColor=DrawingColor.LightBlue;if(対応.Id==選択.Id){ノード.Attr.FillColor=DrawingColor.Gold;}this.図.Invalidate(要素);}}}
    private static DrawingColor 計画色(ExecutionPlanNode 計画){if(計画.Kind=="SourceOperator"){return DrawingColor.LightGray;}if(計画.Kind=="RuntimeCondition"){return DrawingColor.LightYellow;}return DrawingColor.LightBlue;}
    private void 選択を折り畳む(){
        string? ID=null;if(this.計画表示か&&this.選択計画 is{} 計画&&this.ビルド結果 is not null){var 所属=this.ビルド結果.Plans.FirstOrDefault(文書=>文書.Nodes.Any(候補=>ReferenceEquals(候補,計画)));if(所属 is not null){ID=所属.MethodKey+"#"+計画.Id;}}
        else if(this.選択ノード is{} 選択){ID=選択.Id;}if(ID is not null){if(!this.折り畳み.Add(ID)){this.折り畳み.Remove(ID);}this.図を更新();}
    }
    private void 図を更新(){
        var グラフ=new Graph();グラフ.Attr.LayerDirection=LayerDirection.LR;
        if(this.計画表示か&&this.ビルド結果 is not null){
            foreach(var 計画 in this.ビルド結果.Plans){
                var 接頭=計画.MethodKey+"#";var 非表示=new HashSet<string>();void 隠す(string ID){var 子=計画.Nodes.FirstOrDefault(項目=>項目.Id==ID);if(子 is null){return;}foreach(var 入力 in 子.Inputs){if(非表示.Add(入力)){隠す(入力);}}}
                foreach(var ノード in 計画.Nodes){if(this.折り畳み.Contains(接頭+ノード.Id)){隠す(ノード.Id);}}
                foreach(var ノード in 計画.Nodes){if(非表示.Contains(ノード.Id)||this.検索.Text.Length>0&&!ノード.Label.Contains(this.検索.Text,StringComparison.OrdinalIgnoreCase)){continue;}var 図ノード=グラフ.AddNode(接頭+ノード.Id);図ノード.LabelText=ノード.Label;if(this.折り畳み.Contains(接頭+ノード.Id)){図ノード.LabelText+=" [+]";}図ノード.UserData=ノード;図ノード.Attr.FillColor=計画色(ノード);}
                foreach(var ノード in 計画.Nodes){foreach(var 入力 in ノード.Inputs){if(グラフ.FindNode(接頭+入力) is not null&&グラフ.FindNode(接頭+ノード.Id) is not null){グラフ.AddEdge(接頭+入力,接頭+ノード.Id);}}}
            }
        }else if(this.解析結果 is not null){
            var 非表示=new HashSet<string>();foreach(var ノード in this.解析結果.Nodes){var 親=ノード.ParentId;var 既訪問=new HashSet<string>();while(親 is not null&&既訪問.Add(親)){if(this.折り畳み.Contains(親)){非表示.Add(ノード.Id);break;}親=this.解析結果.Nodes.FirstOrDefault(候補=>候補.Id==親)?.ParentId;}}
            foreach(var ノード in this.解析結果.Nodes){if(非表示.Contains(ノード.Id)||this.検索.Text.Length>0&&!ノード.Label.Contains(this.検索.Text,StringComparison.OrdinalIgnoreCase)){continue;}var 図ノード=グラフ.AddNode(ノード.Id);図ノード.LabelText=ノード.Label+"\n"+ノード.Kind;図ノード.UserData=ノード;図ノード.Attr.FillColor=DrawingColor.LightBlue;}
            foreach(var ノード in this.解析結果.Nodes){if(ノード.ParentId is not null&&グラフ.FindNode(ノード.Id) is not null&&グラフ.FindNode(ノード.ParentId) is not null){グラフ.AddEdge(ノード.Id,ノード.ParentId);}}
        }
        if(グラフ.NodeCount>1000){this.状態.Text="ノードが1000件を超えています。検索で表示対象を絞ってください。";return;}
        var ハッシュ=this.解析結果?.SourceHash;if(this.計画表示か){ハッシュ=this.ビルド結果?.SourceHash;}
        if(this.配置中か||this.待機図 is not null){this.待機図=(グラフ,ハッシュ);if(this.配置中か){this.図.CancelToken.Canceled=true;}return;}this.表示図ハッシュ=ハッシュ;this.図.Graph=グラフ;
    }
}
