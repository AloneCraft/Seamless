using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Seamless.Management.UI;

public sealed partial class 管理ウィンドウ{
    private readonly TabItem 監査タブ=new(){Header="監査ログ"};
    private readonly WrapPanel 監査条件=new(){Margin=new Thickness(8)};
    private readonly TextBox 監査検索語=入力("");
    private readonly TextBox 監査DB=入力("");
    private readonly TextBox 監査利用者=入力("");
    private readonly TextBox 監査開始=入力("");
    private readonly TextBox 監査終了=入力("");
    private readonly ComboBox 監査表示種別=new(){MinWidth=180,SelectedIndex=0,Foreground=Brushes.Black,Background=Brushes.White};
    private readonly ComboBox 監査操作種別=new(){MinWidth=110,DisplayMemberPath="名前",Foreground=Brushes.Black,Background=Brushes.White};
    private readonly ComboBox 監査結果種別=new(){MinWidth=100,DisplayMemberPath="名前",Foreground=Brushes.Black,Background=Brushes.White};
    private readonly TextBlock 監査状態=new(){Text="未検索です。条件を入力して「検索」を押してください。",Margin=new Thickness(12),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black};
    private readonly DataGrid 監査結果=new(){IsReadOnly=true,AutoGenerateColumns=false,HeadersVisibility=DataGridHeadersVisibility.Column,RowHeaderWidth=0,EnableRowVirtualization=true,EnableColumnVirtualization=true,SelectionMode=DataGridSelectionMode.Single,CanUserAddRows=false,Background=Brushes.White,Foreground=Brushes.Black};
    private readonly TextBox 監査詳細=new(){IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxHeight=220,Padding=new Thickness(10),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Visibility=Visibility.Collapsed,Foreground=Brushes.Black,Background=Brushes.White};
    private Button 監査検索ボタン=null!,監査続きボタン=null!,監査取消ボタン=null!,監査解除ボタン=null!;
    private CancellationTokenSource? 監査取消;
    private AuditQuery? 監査検索条件;
    private long 監査カーソル,監査上限;
    private bool 監査続きがあるか,監査条件変更中か;
    private sealed record 監査選択肢<T>(string 名前,T? 値)where T:struct;
    private sealed record 監査表示行(AuditEvent 元){
        public string 完了UTC=>this.元.CompletedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture);
        public string 利用者=>this.元.SubjectId;
        public string DB=>this.元.Database;
        public string 操作=>監査操作名(this.元.Action);
        public string 結果=>監査結果名(this.元.Outcome);
        public string 対象{get{var 名前=string.Join('.',new[]{this.元.Namespace,this.元.TypeName,this.元.MethodName}.Where(static 項目=>!string.IsNullOrEmpty(項目)));if(名前.Length==0){return this.元.ObjectId??"—";}return 名前;}}
        public string メッセージ=>this.元.Message??this.元.ResultCode;
    }
    private sealed record 集計表示行(AuditReadEntry 元){
        public string 開始UTC=>this.元.Key.WindowStart.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        public string 終了UTC=>this.元.Key.WindowEnd.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        public string 利用者=>this.元.Key.SubjectId;
        public string DB=>this.元.Key.Database;
        public string 問い合わせ=>this.元.Key.QueryId;
        public long 成功回数=>this.元.Count;
    }
    private static string 監査操作名(AuditAction 値)=>値 switch{AuditAction.Read=>"読み取り",AuditAction.DataChange=>"データ更新",AuditAction.SchemaChange=>"スキーマ変更",AuditAction.PermissionChange=>"権限変更",AuditAction.AccountChange=>"アカウント変更",AuditAction.SourceRead=>"ソース閲覧",AuditAction.Validation=>"検証",_=>"その他"};
    private static string 監査結果名(AuditOutcome 値)=>値 switch{AuditOutcome.Succeeded=>"成功",AuditOutcome.Failed=>"失敗",AuditOutcome.Cancelled=>"取消",AuditOutcome.Rejected=>"拒否",_=>"結果不明"};
    private Grid 監査画面を構築(){
        var 面=new Grid{Background=Brushes.White};面.RowDefinitions.Add(new(){Height=GridLength.Auto});面.RowDefinitions.Add(new(){Height=GridLength.Auto});面.RowDefinitions.Add(new(){Height=GridLength.Auto});面.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});面.RowDefinitions.Add(new(){Height=GridLength.Auto});
        var 見出し=new TextBlock{Text="監査ログ · 条件で絞り込み、行を選ぶと詳細を表示",FontSize=18,Margin=new Thickness(12),Foreground=Brushes.Black};面.Children.Add(見出し);
        void 条件(string 名前,FrameworkElement 要素,double 幅){var 枠=new StackPanel{Margin=new Thickness(4)};要素.Width=幅;AutomationProperties.SetAutomationId(要素,"監査"+名前);枠.Children.Add(new TextBlock{Text=名前,Foreground=Brushes.Black});枠.Children.Add(要素);this.監査条件.Children.Add(枠);}
        this.監査表示種別.Items.Add("詳細な操作履歴");this.監査表示種別.Items.Add("通常読み取りの集計");this.監査表示種別.SelectedIndex=0;
        this.監査操作種別.Items.Add(new 監査選択肢<AuditAction>("すべて",null));foreach(var 操作 in Enum.GetValues<AuditAction>()){this.監査操作種別.Items.Add(new 監査選択肢<AuditAction>(監査操作名(操作),操作));}this.監査操作種別.SelectedIndex=0;
        this.監査結果種別.Items.Add(new 監査選択肢<AuditOutcome>("すべて",null));foreach(var 結果 in Enum.GetValues<AuditOutcome>()){this.監査結果種別.Items.Add(new 監査選択肢<AuditOutcome>(監査結果名(結果),結果));}this.監査結果種別.SelectedIndex=0;
        条件("表示",this.監査表示種別,190);条件("検索語（空白区切り・すべて含む）",this.監査検索語,300);条件("DB（完全一致）",this.監査DB,150);条件("利用者ID（完全一致）",this.監査利用者,180);条件("開始UTC（yyyy-MM-dd HH:mm:ss）",this.監査開始,230);条件("終了UTC（yyyy-MM-dd HH:mm:ss）",this.監査終了,230);条件("操作種別",this.監査操作種別,130);条件("結果",this.監査結果種別,110);
        foreach(var 入力欄 in new[]{this.監査検索語,this.監査DB,this.監査利用者,this.監査開始,this.監査終了}){入力欄.TextChanged+=(_,_)=>this.監査条件変更時();}foreach(var 選択欄 in new[]{this.監査表示種別,this.監査操作種別,this.監査結果種別}){選択欄.SelectionChanged+=(_,_)=>this.監査条件変更時();}
        Grid.SetRow(this.監査条件,1);面.Children.Add(this.監査条件);
        var 操作列=new DockPanel();var ボタン列=new WrapPanel();this.監査検索ボタン=押下("検索",()=>this.監査操作Async(true));this.監査続きボタン=押下("続きを取得（最大100件）",()=>this.監査操作Async(false));this.監査取消ボタン=押下("検索を中止",()=>{this.監査取消?.Cancel();return Task.CompletedTask;});
        foreach(var ボタン in new[]{this.監査検索ボタン,this.監査続きボタン,this.監査取消ボタン}){ボタン列.Children.Add(ボタン);}this.監査解除ボタン=押下("条件を解除",()=>{this.監査検索語.Clear();this.監査DB.Clear();this.監査利用者.Clear();this.監査開始.Clear();this.監査終了.Clear();this.監査操作種別.SelectedIndex=0;this.監査結果種別.SelectedIndex=0;return Task.CompletedTask;});ボタン列.Children.Add(this.監査解除ボタン);DockPanel.SetDock(ボタン列,Dock.Top);操作列.Children.Add(ボタン列);操作列.Children.Add(this.監査状態);Grid.SetRow(操作列,2);面.Children.Add(操作列);
        ScrollViewer.SetHorizontalScrollBarVisibility(this.監査結果,ScrollBarVisibility.Auto);ScrollViewer.SetVerticalScrollBarVisibility(this.監査結果,ScrollBarVisibility.Auto);this.監査結果.SelectionChanged+=(_,_)=>this.監査詳細を表示();AutomationProperties.SetAutomationId(this.監査結果,"監査検索結果");AutomationProperties.SetAutomationId(this.監査状態,"監査検索状態");Grid.SetRow(this.監査結果,3);面.Children.Add(this.監査結果);Grid.SetRow(this.監査詳細,4);面.Children.Add(this.監査詳細);this.監査列を設定(false);this.監査操作状態を更新();return 面;
    }
    private void 監査列を設定(bool 集計か){
        this.監査結果.Columns.Clear();
        void 列(string 名前,double 幅){var 項目=new DataGridTextColumn{Header=名前,Binding=new Binding(名前),Width=幅};if(名前 is "対象" or "メッセージ" or "問い合わせ"){項目.Width=new DataGridLength(1,DataGridLengthUnitType.Star);項目.MinWidth=180;}this.監査結果.Columns.Add(項目);}
        if(集計か){列("開始UTC",170);列("終了UTC",170);列("利用者",160);列("DB",140);列("問い合わせ",280);列("成功回数",100);}
        else{列("完了UTC",190);列("利用者",160);列("DB",130);列("操作",120);列("結果",80);列("対象",300);列("メッセージ",350);}
    }
    private bool 監査利用可能か()=>this.クライアント is not null&&this.ログイン利用者?.Grants.Any(static 許可=>許可.Database=="*"&&(許可.Permissions&ManagementPermission.Manage)==ManagementPermission.Manage)==true&&this.サーバー能力.Contains("AuditSearchV1",StringComparer.Ordinal);
    private void 監査操作状態を更新(){if(this.監査検索ボタン is null){return;}var 取得中か=this.監査取消 is not null;this.監査検索ボタン.IsEnabled=this.監査利用可能か()&&!取得中か;this.監査続きボタン.IsEnabled=this.監査検索ボタン.IsEnabled&&this.監査続きがあるか&&!this.監査条件変更中か;this.監査取消ボタン.IsEnabled=取得中か;this.監査解除ボタン.IsEnabled=!取得中か;this.監査条件.IsEnabled=!取得中か;}
    private void 監査条件変更時(){this.監査条件変更中か=true;this.監査操作状態を更新();if(this.監査検索条件 is not null){this.監査状態.Text="条件を変更しました。「検索」で取得し直してください。下の結果は以前の条件です。";}}
    private void 監査接続を消去(){this.監査取消?.Cancel();this.監査取消=null;this.監査検索条件=null;this.監査続きがあるか=false;this.監査カーソル=0;this.監査上限=0;this.監査結果.ItemsSource=null;this.監査詳細.Clear();this.監査詳細.Visibility=Visibility.Collapsed;this.監査状態.Text="未接続です。サーバーに接続してください。";this.監査操作状態を更新();}
    private Task 監査を表示Async(){
        this.編集タブ.SelectedItem=this.監査タブ;this.属性を表示(false);this.監査操作状態を更新();this.状態.Text="監査ログ · 読み取り専用";
        if(this.クライアント is null){this.監査状態.Text="未接続です。サーバーに接続してください。";}
        else if(!this.サーバー能力.Contains("AuditSearchV1",StringComparer.Ordinal)){this.監査状態.Text="このサーバーは監査検索に未対応です。形式3の場合は、停止・バックアップを伴うCLIのupgrade-auditで明示更新できます。";}
        else if(!this.監査利用可能か()){this.監査状態.Text="監査ログの閲覧にはサーバー管理権限が必要です。";}
        else if(this.監査検索条件 is null){this.監査状態.Text="未検索です。条件を入力して「検索」を押してください。時刻はUTC、空欄は制限なしです。";}
        return Task.CompletedTask;
    }
    private static DateTimeOffset? 監査日時(string 値,string 名前){if(string.IsNullOrWhiteSpace(値)){return null;}if(DateTimeOffset.TryParseExact(値.Trim(),"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out var 日時)){return 日時;}throw new ManagementException("InvalidRequest",名前+"はUTCのyyyy-MM-dd HH:mm:ssで入力してください。");}
    private static string? 空欄を省略(string 値){if(string.IsNullOrWhiteSpace(値)){return null;}return 値.Trim();}
    private async Task 監査操作Async(bool 新規か){var 接続=this.クライアント;try{await this.監査ページAsync(新規か);}catch(Exception 例外){if(ReferenceEquals(this.クライアント,接続)){this.監査状態.Text="検索できませんでした: "+例外.Message;}}}
    private async Task 監査ページAsync(bool 新規か){
        if(!this.監査利用可能か()){await this.監査を表示Async();return;}if(this.監査取消 is not null){return;}var 接続=this.必須接続();AuditQuery 要求;
        if(新規か){
            var 開始=監査日時(this.監査開始.Text,"開始時刻");var 終了=監査日時(this.監査終了.Text,"終了時刻");if(開始>終了){throw new ManagementException("InvalidRequest","開始時刻は終了時刻以前にしてください。");}
            要求=new(開始,終了,空欄を省略(this.監査DB.Text),空欄を省略(this.監査利用者.Text),(this.監査操作種別.SelectedItem as 監査選択肢<AuditAction>)?.値,(this.監査結果種別.SelectedItem as 監査選択肢<AuditOutcome>)?.値,Text:空欄を省略(this.監査検索語.Text));
        }else{if(this.監査検索条件 is null||!this.監査続きがあるか||this.監査条件変更中か){return;}要求=this.監査検索条件 with{Before=this.監査カーソル,Ceiling=this.監査上限};}
        var 集計か=this.監査表示種別.SelectedIndex==1;using var 取消=new CancellationTokenSource();this.監査取消=取消;this.監査操作状態を更新();this.監査状態.Text="検索中です。中止できます。";
        try{
            object[] 行;int 走査数;long 続き,上限;bool 続きがあるか;
            if(集計か){var 頁=await 接続.ReadSummariesAsync(要求,取消.Token);行=頁.Items.Select(static 項目=>(object)new 集計表示行(項目)).ToArray();走査数=頁.Examined;続き=頁.NextBefore;上限=頁.Ceiling;続きがあるか=頁.HasMore;}
            else{var 頁=await 接続.AuditEventsAsync(要求,取消.Token);行=頁.Items.Select(static 項目=>(object)new 監査表示行(項目)).ToArray();走査数=頁.Examined;続き=頁.NextBefore;上限=頁.Ceiling;続きがあるか=頁.HasMore;}
            if(!ReferenceEquals(this.クライアント,接続)||!ReferenceEquals(this.監査取消,取消)||取消.IsCancellationRequested){return;}
            this.監査検索条件=要求;this.監査条件変更中か=false;this.監査カーソル=続き;this.監査上限=上限;this.監査続きがあるか=続きがあるか;this.監査列を設定(集計か);this.監査結果.ItemsSource=行;this.監査結果.SelectedItem=null;this.監査詳細.Visibility=Visibility.Collapsed;
            this.監査状態.Text="このページ: "+行.Length+" 件 / 調べたレコード: "+走査数+" 件。";if(続きがあるか){this.監査状態.Text+="続きがあります。0件でも続きを取得できます。";}else if(行.Length==0){this.監査状態.Text+="この条件に一致する保存済みログはありません。";}else{this.監査状態.Text+="最後のページです。";}if(集計か){this.監査状態.Text+="保存済みの件数を表示しています。";}
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){if(ReferenceEquals(this.監査取消,取消)){this.監査状態.Text="検索を中止しました。表示中の結果は以前の検索結果です。";}}
        finally{if(ReferenceEquals(this.監査取消,取消)){this.監査取消=null;this.監査操作状態を更新();}}
    }
    private void 監査詳細を表示(){
        if(this.監査結果.SelectedItem is 監査表示行 行){var 項目=行.元;this.監査詳細.Text=$"イベントID: {項目.EventId}\n操作ID: {項目.OperationId}\n認証元ID: {項目.AuthorityId}\n利用者ID: {項目.SubjectId}\nDB: {項目.Database}\n対象ID: {項目.ObjectId}\n開始UTC: {項目.StartedAt.UtcDateTime:O}\n完了UTC: {項目.CompletedAt.UtcDateTime:O}\n操作: {監査操作名(項目.Action)} / 結果: {監査結果名(項目.Outcome)} / 結果コード: {項目.ResultCode}\n名前空間: {項目.Namespace}\n型: {項目.TypeName}\nメソッド: {項目.MethodName}\nシグネチャ: {項目.MethodSignature}\nコード版: {項目.CodeVersion}\n権限版: {項目.PolicyVersion}\nモジュールID: {項目.MethodModuleId} / MetadataToken: {項目.MethodMetadataToken}\n取引ID: {項目.TransactionId}\nメッセージ: {項目.Message}\nクエリテンプレート: "+(項目.QueryTemplate??"未記録、または閲覧権限なし");this.監査詳細.Visibility=Visibility.Visible;}
        else if(this.監査結果.SelectedItem is 集計表示行 集計){var 項目=集計.元;this.監査詳細.Text=$"認証元ID: {項目.Key.AuthorityId}\n利用者ID: {項目.Key.SubjectId}\nDB: {項目.Key.Database}\n問い合わせID: {項目.Key.QueryId}\n集計開始UTC: {項目.Key.WindowStart.UtcDateTime:O}\n集計終了UTC: {項目.Key.WindowEnd.UtcDateTime:O}\n成功回数: {項目.Count}\nコード版: {項目.Key.CodeVersion}\n権限版: {項目.Key.PolicyVersion}\n保存UTC: {項目.SavedAt.UtcDateTime:O}";this.監査詳細.Visibility=Visibility.Visible;}
        else{this.監査詳細.Clear();this.監査詳細.Visibility=Visibility.Collapsed;}
    }
}
