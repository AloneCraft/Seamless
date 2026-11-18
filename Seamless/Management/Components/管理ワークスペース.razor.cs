using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Seamless.Management.Components;

public partial class 管理ワークスペース{
    [Parameter]public UserInfo? 利用者{get;set;}
    [Parameter]public string 接続方式{get;set;}="ローカルIPC";
    [Parameter]public string? ログアウト先{get;set;}
    private ElementReference 根,新保存先入力,メニュー起点,文書操作起点;
    private IJSObjectReference? モジュール,相互操作;
    private DotNetObjectReference<管理ワークスペース>? 参照;
    private readonly CancellationTokenSource 寿命=new();
    private bool 破棄済みか;
    private CatalogDatabase[] DB一覧=[];
    private readonly Dictionary<string,文書状態> 文書一覧=new(StringComparer.Ordinal);
    private readonly 文書タブ順序 タブ順序=new([]);
    private string? 選択DBID;
    private long 次カーソル;
    private string カタログ版="";
    private bool 次ページがあるか,認証有効か=true;
    private bool 監査対応か,監査表示か;
    private bool 監査を利用できるか=>this.監査対応か&&this.権限があるか("*",ManagementPermission.Manage);
    private void 監査を開く(){if(this.監査を利用できるか){this.ロール表示か=false;this.設定表示か=false;this.利用者管理表示か=false;this.プロジェクト表示か=false;this.監査表示か=true;}}
    private bool 利用者管理対応か,利用者管理表示か,管理未保存か;
    private bool 設定対応か,設定表示か,設定未保存か;
    private bool ロール対応か,ロール表示か,ロール未保存か;
    private bool ロールを利用できるか=>this.ロール対応か&&this.権限があるか("*",ManagementPermission.Manage);
    private void ロールを開く(){if(this.ロールを利用できるか){this.設定表示か=false;this.利用者管理表示か=false;this.監査表示か=false;this.プロジェクト表示か=false;this.ロール表示か=true;}}
    private bool 設定を利用できるか=>this.設定対応か&&this.権限があるか("*",ManagementPermission.Manage);
    private void 設定を開く(){if(this.設定を利用できるか){this.ロール表示か=false;this.監査表示か=false;this.利用者管理表示か=false;this.プロジェクト表示か=false;this.設定表示か=true;}}
    private bool 利用者管理を利用できるか=>this.利用者管理対応か&&this.権限があるか("*",ManagementPermission.Manage);
    private void 利用者管理を開く(){if(this.利用者管理を利用できるか){this.ロール表示か=false;this.設定表示か=false;this.監査表示か=false;this.プロジェクト表示か=false;this.利用者管理表示か=true;}}
    private string 作業場所{get{if(this.ロール表示か){return "ロールと権限";}if(this.設定表示か){return "サーバー設定";}if(this.利用者管理表示か){return "利用者";}if(this.監査表示か){return "監査";}return "データベース";}}
    private int 処理中数;
    private string 案内=表示文言.取得("接続しています…");
    private bool 外部取り込みできるか,外部取り込み表示か;
    private bool エラーか,表入力中か,DB名編集中か,DB作成中か,DB保存先指定中か,保存先フォーカス待ちか,プロジェクト生成中か;
    private enum 表示種別{構造,ソース,データ,問い合わせ,クエリ一覧,計画,履歴}
    private 表示種別 表示=表示種別.構造;
    private bool ソース表示か{get=>this.表示==表示種別.ソース;set=>this.表示=value?表示種別.ソース:表示種別.構造;}
    private string? 選択表名;
    private ExecutionPlanDocument[]? 保持計画;
    private QueryPlanRequest? 計画対象;
    private string? 計画対象名;
    private ObjectExecutionStatistics? 最新実測;
    private string 新DB名="",新プロジェクト保存先="",表名="NewTable",スキーマ名="";
    private Definition? 作成完了定義;
    private string? 作成完了DB名;
    private SourceProjectDocument? 作成済みプロジェクト;
    private ApplyRequest? 新DB未確定操作;
    private string? 新DB定義ID;
    private readonly List<列入力> 入力列=[];
    private static readonly string[] 基本型候補=["bool","byte","sbyte","short","ushort","int","uint","long","ulong","nint","nuint","float","double","decimal","char","string","Guid","DateTime","DateTimeOffset","DateOnly","TimeOnly","TimeSpan","byte[]"];
    private string[] プロジェクト型候補=[];
    private SourceSnapshot? 型候補ソース;
    private string[]? 反映済みプロジェクト候補;
    private string[] 統合型候補=[];
    private string[] 型候補{
        get{
            var source=this.現在文書?.定義.Source;
            if(!ReferenceEquals(source,this.型候補ソース)||!ReferenceEquals(this.プロジェクト型候補,this.反映済みプロジェクト候補)){
                var declared=Array.Empty<string>();
                if(source is not null){try{declared=SourceStructure.Read(source).Types;}catch(ManagementException){}}
                this.統合型候補=基本型候補.Concat(this.プロジェクト型候補).Concat(declared).Distinct(StringComparer.Ordinal).ToArray();
                this.型候補ソース=source;this.反映済みプロジェクト候補=this.プロジェクト型候補;
            }
            return this.統合型候補;
        }
    }
    private TableDesign? 編集前の表;
    private コマンド登録<管理対象>? コマンド;
    private 管理対象? メニュー対象;
    private string? メニューID;
    private string メニュー見出し="操作";
    private long メニュー世代;
    private double メニューX,メニューY;
    private bool パレットか;
    private bool 操作中か=>this.DB名前変更中か||this.コマンド?.実行中か==true||this.DB作成中か||this.処理中数>0||this.ソース下書き処理中か;
    private bool DB作成できるか=>this.認証有効か&&this.利用者 is{Disabled:false} 利用者&&利用者.Grants.Any(許可=>(許可.Permissions&ManagementPermission.Edit)!=0);
    private 文書状態? 現在文書{get{if(this.選択DBID is not null&&!this.公開情報表示か(this.選択DBID)&&this.文書一覧.TryGetValue(this.選択DBID,out var 文書)){return 文書;}return null;}}
    private bool プロジェクト表示か,プロジェクト未保存か,ソース操作できるか,ソース参照できるか,ソース下書きできるか,ソース成果物できるか,ソースSDKビルドできるか,ソース成果物取込できるか,ソース下書き処理中か,ソース競合できるか,ソース構成できるか;
    private bool 未保管の作業があるか=>this.DBツリー画面.Values.Any(木=>木.未保存か)||this.ロール未保存か||this.設定未保存か||this.管理未保存か||this.プロジェクト未保存か||this.文書一覧.Values.Any(文書=>文書.未保管か)||this.関数定義文書.Values.Any(文書=>文書.未保管か)||this.対象文書一覧.Values.Any(文書=>文書.編集中か||文書.データ編集中か);

    protected override async Task OnInitializedAsync(){
        this.コマンド=new(new 操作コマンド<管理対象>[]{
            new("登録関数データ","データ",対象=>対象.種類=="登録関数",_=>true,対象=>this.登録関数表示時Async(対象,"データ"),["data","function"],アイコン:"データ"),
            new("登録関数編集","構造",対象=>対象.種類=="登録関数",対象=>this.対象文書(対象) is{} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit),対象=>this.登録関数表示時Async(対象,"構造"),["edit","function"],アイコン:"編集"),
            new("登録関数CSharp","C#",対象=>対象.種類=="登録関数",_=>true,対象=>this.登録関数表示時Async(対象,"C#"),["source","function"],アイコン:"ソース"),
            new("登録関数計画","実行計画",対象=>対象.種類=="登録関数",_=>true,対象=>this.登録関数表示時Async(対象,"実行計画"),["plan","function"],アイコン:"計画"),
            new("表の列追加","列を追加",対象=>対象.種類 is "表追加" or "表",対象=>this.作成できるか(対象),対象=>this.表追加編集Async(対象,true),["column","追加"],アイコン:"列を追加"),
            new("表の主キー","主キーを編集",対象=>対象.種類 is "表追加" or "表",対象=>this.作成できるか(対象),対象=>this.表追加編集Async(対象,false),["key","primary"],アイコン:"編集"),
            new("表の外部キー","外部キーをC#で追加",対象=>対象.種類 is "表追加" or "表",対象=>this.作成できるか(対象),対象=>this.対象表示を開くAsync(対象.表!,"C#"),["foreign","relation","参照"],アイコン:"ソース"),
            new("スキーマ作成","スキーマ",対象=>追加項目か(対象,"Schema")&&対象.スキーマ.Length==0,対象=>this.作成できるか(対象),対象=>{this.スキーマ作成開始時(対象);return Task.CompletedTask;},["schema","作成"],アイコン:"スキーマ"),
            new("テーブル作成","テーブル",対象=>追加項目か(対象,"Table"),対象=>this.作成できるか(対象),対象=>{this.表作成開始時(this.文書一覧[対象.ID],対象.スキーマ);return Task.CompletedTask;},["table","作成"],アイコン:"テーブル"),
            new("ビュー作成","ビュー",対象=>追加項目か(対象,"View"),対象=>this.作成できるか(対象),対象=>{this.問い合わせ作成開始時(対象,DatabaseQueryKind.View);return Task.CompletedTask;},["view","作成"],アイコン:"ビュー"),
            new("関数作成","関数",対象=>追加項目か(対象,"Function"),対象=>this.作成できるか(対象),対象=>{this.問い合わせ作成開始時(対象,DatabaseQueryKind.ScalarFunction);return Task.CompletedTask;},["function","値","行","作成"],アイコン:"関数"),
            new("その他の定義","C#でその他の定義を追加",対象=>追加項目か(対象,"Other"),対象=>this.作成できるか(対象),対象=>{this.文書を選ぶ(対象.ID);this.プロジェクト表示か=true;return Task.CompletedTask;},["stored","procedure","作業辞書","定義"],グループ:"その他",アイコン:"ソース"),
            new("問い合わせ編集","問い合わせを編集",対象=>対象.種類=="問い合わせ",対象=>this.対象文書(対象) is{} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit),対象=>this.対象表示を開くAsync(対象.表!,"構造"),["view","function","編集"],アイコン:"編集"),
            new("ロール","ロールと権限を表示",対象=>対象.種類 is not ("作成" or "表追加")&&this.ロールを利用できるか,_=>this.認証有効か,_=>{this.ロールを開く();return Task.CompletedTask;},["role","権限","割り当て"],アイコン:"ロール"),
            new("プロジェクト","ソースプロジェクトを開く",対象=>対象.種類 is not ("作成" or "表追加")&&this.ソース操作できるか,_=>this.認証有効か,_=>{this.ロール表示か=false;this.設定表示か=false;this.利用者管理表示か=false;this.監査表示か=false;this.プロジェクト表示か=true;return Task.CompletedTask;},["slnx","csproj","フォルダー","開発","source","project"],アイコン:"フォルダー"),
            new("設定","サーバー設定を表示",対象=>対象.種類 is not ("作成" or "表追加")&&this.設定を利用できるか,_=>this.設定を利用できるか,_=>{this.設定を開く();return Task.CompletedTask;},["settings","server","接続","通信予算","参照監査"],グループ:"表示",アイコン:"設定"),
            new("利用者","利用者を管理",対象=>対象.種類 is not ("作成" or "表追加")&&this.利用者管理を利用できるか,_=>this.利用者管理を利用できるか,_=>{this.利用者管理を開く();return Task.CompletedTask;},["users","account","アカウント","権限"],グループ:"表示",アイコン:"利用者"),
            new("監査","監査を表示",対象=>対象.種類 is not ("作成" or "表追加")&&this.監査を利用できるか,_=>this.監査を利用できるか,_=>{this.監査を開く();return Task.CompletedTask;},["audit","履歴","ログ","集計","転記"],グループ:"表示",アイコン:"監査"),
            new("プロジェクト保存","専用C#プロジェクトとして保存",対象=>対象.種類 is "DB" or "文書"&&this.ソース操作できるか,対象=>this.対象文書(対象) is{} 文書&&文書.未確定操作 is null&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit),対象=>{this.プロジェクト保存開始時(this.対象文書(対象)!);return Task.CompletedTask;},["csproj","保存","開発","project","export"],アイコン:"保存"),
            new("表データ","行データを参照・編集",対象=>対象.種類=="表",_=>true,対象=>{this.表データ表示時(対象.表!);return Task.CompletedTask;},["data","rows","行","レコード","table"],アイコン:"データ"),
            new("表を編集","テーブルを編集",対象=>対象.種類=="表",対象=>this.対象文書(対象) is{} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit),対象=>this.対象表示を開くAsync(対象.表!,"構造"),["列","設計","edit"],アイコン:"編集"),
            new("表ソース","C#の定義を開く",対象=>対象.種類=="表",_=>true,対象=>this.対象表示を開くAsync(対象.表!,"C#"),["source","コード"],アイコン:"ソース"),
            new("全ソース","全ソースを表示",対象=>対象.種類 is "DB" or "分類",_=>this.認証有効か,対象=>this.生成コードを開くAsync(対象,true),["source","generated","生成","全ソース"],アイコン:"ソース"),
            new("生成コード","生成コードを表示",対象=>対象.種類 is "表" or "問い合わせ" or "スキーマ" or "オブジェクト分類",_=>this.認証有効か,対象=>this.生成コードを開くAsync(対象,false),["generated","生成","コード"],アイコン:"ソース"),
            new("DB表示上","上へ移動",対象=>対象.種類=="DB"&&this.対象DB(対象) is{IsSystem:false},対象=>this.DB表示順を変更できるか(対象,-1),対象=>this.DB表示順を変更Async(対象,-1),["order","up","表示順"],グループ:"表示順（このブラウザー）",アイコン:"上"),
            new("DB表示下","下へ移動",対象=>対象.種類=="DB"&&this.対象DB(対象) is{IsSystem:false},対象=>this.DB表示順を変更できるか(対象,1),対象=>this.DB表示順を変更Async(対象,1),["order","down","表示順"],グループ:"表示順（このブラウザー）",アイコン:"下"),
            new("DB表示既定","既定の表示順に戻す",対象=>対象.種類=="DB"&&this.対象DB(対象) is{IsSystem:false},対象=>this.表示状態操作 is not null&&this.表示状態診断 is null&&this.DBの表示順.保存値.Length>0&&this.対象DB(対象) is{} DB&&this.権限があるか(DB.Name,ManagementPermission.View),_=>this.DB表示順を戻すAsync(),["order","reset","表示順"],グループ:"表示順（このブラウザー）",アイコン:"戻す"),
            new("対象表示上","上へ移動",対象=>対象.種類 is "表" or "問い合わせ" or "登録関数",対象=>this.オブジェクト表示順を変更できるか(対象,-1),対象=>this.オブジェクト表示順を変更Async(対象,-1),["order","up","表示順"],グループ:"表示順（この画面内・未保存）",アイコン:"上"),
            new("対象表示下","下へ移動",対象=>対象.種類 is "表" or "問い合わせ" or "登録関数",対象=>this.オブジェクト表示順を変更できるか(対象,1),対象=>this.オブジェクト表示順を変更Async(対象,1),["order","down","表示順"],グループ:"表示順（この画面内・未保存）",アイコン:"下"),
            new("対象表示既定","既定の表示順に戻す",対象=>対象.種類 is "表" or "問い合わせ" or "登録関数",対象=>this.オブジェクト表示順を使えるか(対象.ID),this.オブジェクト表示順を戻すAsync,["order","reset","表示順"],グループ:"表示順（この画面内・未保存）",アイコン:"戻す"),
            new("DB名変更","名前の変更",対象=>対象.種類=="DB"&&this.DB名変更対応か,対象=>this.対象DB(対象) is{} DB&&this.DB名を変更できるか(DB),対象=>{this.DB名前変更開始時(this.対象DB(対象)!);return Task.CompletedTask;},["名前","rename"],ショートカット:"F2",アイコン:"編集"),
            new("開く","開く",対象=>対象.種類=="DB",_=>this.認証有効か,対象=>this.DBを開く時Async(対象.ID),["open","表示"],アイコン:"開く"),
            new("新しいDB","新しいデータベース",対象=>対象.種類=="分類",_=>this.DB作成できるか,_=>{this.新規DB開始時();return Task.CompletedTask;},["作成","database","new"],アイコン:"DB"),
            new("外部DB取り込み","外部DBから取り込む",対象=>対象.種類=="分類",_=>this.DB作成できるか&&this.外部取り込みできるか,_=>{this.外部取り込み開始時();return Task.CompletedTask;},["取り込み","import","sqlserver"],アイコン:"DB"),
            new("更新","一覧を更新",対象=>対象.種類 is not ("作成" or "表追加"),_=>this.認証有効か,_=>this.更新Async(),["再取得","refresh"],グループ:"表示",アイコン:"更新"),
            new("計画","実行計画を表示",対象=>対象.種類=="問い合わせ",対象=>this.対象文書(対象) is not null,対象=>{this.問い合わせ表示時(this.対象文書(対象)!,対象.表!);this.クエリ計画表示時();return Task.CompletedTask;},["plan","flinq","最適化"],グループ:"表示",アイコン:"計画"),
            new("保管","下書きを保管",対象=>対象.種類 is not ("作成" or "表追加")&&this.対象文書(対象) is not null,対象=>this.対象文書(対象) is{} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Edit),対象=>this.下書きを保管Async(this.対象文書(対象)!),["保存","save","draft"],ショートカット:"",アイコン:"保存"),
            new("確認","変更を確認",対象=>対象.種類 is not ("作成" or "表追加")&&this.対象文書(対象) is not null,対象=>this.対象文書(対象) is{} 文書&&this.権限があるか(文書.DB.Name,ManagementPermission.Apply),対象=>this.メニューから変更を確認Async(this.対象文書(対象)!),["検証","プレビュー","適用","validate"],アイコン:"確認"),
            new("左へ","タブを左へ移動",対象=>対象.種類=="文書",対象=>this.タブ移動先(対象.文書ID??対象.ID,-1) is not null,対象=>{this.タブを移動(対象.文書ID??対象.ID,-1);return Task.CompletedTask;},["並べ替え","移動"],グループ:"タブ",アイコン:"左"),
            new("右へ","タブを右へ移動",対象=>対象.種類=="文書",対象=>this.タブ移動先(対象.文書ID??対象.ID,1) is not null,対象=>{this.タブを移動(対象.文書ID??対象.ID,1);return Task.CompletedTask;},["並べ替え","移動"],グループ:"タブ",アイコン:"右"),
            new("閉じる","このタブを閉じる",対象=>対象.種類=="文書",_=>true,対象=>{this.作業文書を閉じる(対象.文書ID??対象.ID);return Task.CompletedTask;},["close","文書"],グループ:"タブ",アイコン:"閉じる"),
            new("対象名前変更","名前の変更",対象=>対象.種類 is "表" or "問い合わせ",対象=>this.作成できるか(対象),対象=>this.ツリー名前変更をメニューから開始Async(対象),["名前","rename"],ショートカット:"F2",アイコン:"名前の変更"),
            new("対象コピー","コピー",対象=>対象.種類 is "表" or "問い合わせ",対象=>this.作成できるか(対象),対象=>this.ツリー操作をメニューから実行Async(対象,(操作,項目)=>操作.コピー(項目)),["copy","複製"],アイコン:"コピー"),
            new("スキーマ削除","削除",対象=>対象.種類=="スキーマ",対象=>this.作成できるか(対象),対象=>this.スキーマ削除をメニューから実行Async(対象),["delete","remove","schema"],アイコン:"削除"),
            new("対象削除","削除",対象=>対象.種類 is "表" or "問い合わせ",対象=>this.作成できるか(対象),対象=>this.ツリー操作をメニューから実行Async(対象,(操作,項目)=>操作.削除(項目)),["delete","remove"],アイコン:"削除"),
        }.Select(操作=>操作 with{表示するか=対象=>操作.表示するか(対象)&&this.公開メニュー操作か(対象,操作.ID)&&this.ツリーメニュー操作か(対象,操作.ID)}));
        await this.処理Async(async()=>{var 情報=await this.管理操作.InfoAsync(this.寿命.Token);this.表示状態インスタンスID=情報.InstanceId;this.資源計測対応か=情報.Capabilities.Contains("ResourceHeatV1",StringComparer.Ordinal);this.ロール対応か=情報.Capabilities.Contains("DatabaseAuthorizationV1",StringComparer.Ordinal);this.設定対応か=情報.Capabilities.Contains("ServerSettingsV1",StringComparer.Ordinal);this.利用者管理対応か=情報.Capabilities.Contains("UserProfilesV1",StringComparer.Ordinal);this.監査対応か=情報.Capabilities.Contains("AuditSearchV1",StringComparer.Ordinal);this.DB名変更対応か=情報.Capabilities.Contains("DatabaseRenameV1",StringComparer.Ordinal);this.ソース構成できるか=情報.Capabilities.Contains("OwnerSourceConfigurationsV1",StringComparer.Ordinal);this.ソース競合できるか=情報.Capabilities.Contains("OwnerSourceConflictsV1",StringComparer.Ordinal);this.ソース操作できるか=情報.Capabilities.Contains("OwnerSourceProjectsV1",StringComparer.Ordinal);this.外部取り込みできるか=情報.Capabilities.Contains("OwnerExternalTransferV1",StringComparer.Ordinal)&&this.管理操作 is IManagementTransferOperations;this.ソース参照できるか=情報.Capabilities.Contains("OwnerSourceDirectoryV1",StringComparer.Ordinal);this.ソース下書きできるか=情報.Capabilities.Contains("OwnerSourceDraftsV1",StringComparer.Ordinal);this.ソース成果物できるか=情報.Capabilities.Contains("OwnerSourceBuildsV1",StringComparer.Ordinal);this.ソースSDKビルドできるか=情報.Capabilities.Contains("OwnerSourceSdkBuildV1",StringComparer.Ordinal);this.ソース成果物取込できるか=情報.Capabilities.Contains("OwnerSourceBuildImportsV1",StringComparer.Ordinal)&&情報.Capabilities.Contains("OwnerSourceBuildPublicationV1",StringComparer.Ordinal);await this.更新Async();var 最初=this.DB一覧.FirstOrDefault(DB=>!DB.IsSystem);if(最初 is not null){await this.DBを開くAsync(最初.Id);}});
        this.表示状態情報読込済みか=true;
    }
    protected override async Task OnAfterRenderAsync(bool firstRender){
        try{
            if(firstRender){
                this.参照=DotNetObjectReference.Create(this);
                this.モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import","./_content/Seamless.Management.Components/interaction.js");
                this.相互操作=await this.モジュール.InvokeAsync<IJSObjectReference>("初期化",this.根,this.参照);
                this.オブジェクト順モジュール=await this.スクリプト.InvokeAsync<IJSObjectReference>("import","./_content/Seamless.Management.Components/object-order.js");
                this.オブジェクト順操作=await this.オブジェクト順モジュール.InvokeAsync<IJSObjectReference>("initialize",this.根,this.参照);
                await this.サイドバーを初期化Async();
                if(this.サイドバー操作 is null){throw new InvalidOperationException("Sidebar initialization did not complete.");}
                this.画面イベント準備済みか=true;
            }
            if(!this.画面イベント準備済みか||this.画面初期化失敗か){return;}
            if(this.保存先フォーカス待ちか&&this.操作受付中か){this.保存先フォーカス待ちか=false;await this.新保存先入力.FocusAsync();}
            await this.システム追加を反映Async();
            await this.ブラウザー表示状態を反映Async();
            if(!this.操作受付中か&&this.表示状態情報読込済みか&&!this.表示状態復元中か&&!this.破棄済みか){this.操作受付中か=true;await this.InvokeAsync(this.StateHasChanged);}
        }catch(JSDisconnectedException){}
        catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        catch(Exception 例外)when(!this.操作受付中か&&(例外 is JSException or InvalidOperationException)){
            this.画面初期化失敗か=true;this.操作受付中か=false;
            if(!this.破棄済みか){await this.InvokeAsync(this.StateHasChanged);}
        }
    }
    private bool 権限があるか(string DB,ManagementPermission 権限)=>this.認証有効か&&this.利用者 is{Disabled:false} 利用者&&利用者.Grants.Any(許可=>(許可.Database=="*"||許可.Database==DB)&&(許可.Permissions&権限)==権限);
    private CatalogDatabase? 対象DB(管理対象 対象)=>this.DB一覧.FirstOrDefault(DB=>DB.Id==対象.ID);
    private 文書状態? 対象文書(管理対象 対象){if(this.公開情報表示か(対象.ID)){return null;}if(対象.文書ID is{} ID&&this.対象文書一覧.TryGetValue(ID,out var 作業文書)){return this.対象の定義文書(作業文書);}this.文書一覧.TryGetValue(対象.ID,out var 文書);return 文書;}
    private async Task 処理Async(Func<Task> 処理){
        this.エラーか=false;this.処理中数++;
        try{await 処理();}
        catch(Exception 例外){this.エラーか=true;this.案内=例外.Message;if(例外 is ManagementException{Code:"Authentication"}){this.認証有効か=false;}}
        finally{this.処理中数--;}
    }
    private async Task 更新Async(){
        var ページ=await this.管理操作.DatabasesAsync(cancellationToken:this.寿命.Token);
        this.DB一覧=ページ.Items;this.カタログ版=ページ.VersionToken;foreach(var DB in this.DB一覧){if(this.公開情報表示か(DB.Id)){await this.公開DBを読むAsync(DB,true);}else if(this.文書一覧.TryGetValue(DB.Id,out var 文書)){文書.DB情報を更新(DB);await this.登録関数を読むAsync(DB);}}this.次カーソル=ページ.NextCursor;this.次ページがあるか=ページ.HasMore;this.案内=表示文言.取得("接続済み");
    }
    private Task 次ページ時Async()=>this.処理Async(async()=>{var ページ=await this.管理操作.DatabasesAsync(this.次カーソル,this.カタログ版,this.寿命.Token);this.DB一覧=[..this.DB一覧,..ページ.Items];this.次カーソル=ページ.NextCursor;this.次ページがあるか=ページ.HasMore;});
    private Task DBを開く時Async(string ID)=>this.処理Async(()=>this.DBを開くAsync(ID));
    private async Task DBを開くAsync(string ID){
        var DB=this.DB一覧.Single(項目=>項目.Id==ID);this.文書を選ぶ(ID);
        var 世代=this.対象選択世代;
        if(DB.IsSystem){this.案内=表示文言.取得("system · システム定義は参照専用です。");return;}
        await this.DB文書を読み込むAsync(DB);
        var 対象文書=this.対象文書を取得(ID);this.タブ順序.追加(対象文書.ID);
        if(this.選択DBID==ID&&世代==this.対象選択世代){this.選択作業文書ID=対象文書.ID;this.案内="";}
    }
    private async Task DB文書を読み込むAsync(CatalogDatabase DB){
        var ID=DB.Id;
        if(!this.権限があるか(DB.Name,ManagementPermission.Edit)){await this.公開DBを読むAsync(DB);return;}
        if(!this.文書一覧.ContainsKey(ID)){
            var 公開=await this.管理操作.GetDefinitionAsync(DB.DefinitionId,this.寿命.Token);
            var 下書き=await this.管理操作.FindDraftAsync(DB.DefinitionId,this.寿命.Token);
            var 文書=new 文書状態(DB,公開,下書き);
            if(this.ソース成果物取込できるか&&文書.下書き版>0&&this.管理操作 is IManagementSourceOperations ソース操作){
                if(await ソース操作.FindImportedSourceBuildAsync(文書.検証キー,this.寿命.Token) is{} 成果物){文書.成果物を選択(成果物);}
            }
            this.文書一覧.TryAdd(ID,文書);
        }
        if(!this.登録関数一覧.ContainsKey(ID)){await this.登録関数を読むAsync(DB);}
    }
    private void 文書を選ぶ(string ID){
        this.対象選択世代++;
        this.ツリー所属選択中か=true;
        if(this.選択DBID!=ID){this.実行問い合わせ=null;this.選択表名=null;this.保持計画=null;this.計画対象=null;this.計画対象名=null;this.最新実測=null;this.選択トレース実行ID=null;this.表示=表示種別.構造;}
        this.ロール表示か=false;this.設定表示か=false;this.利用者管理表示か=false;this.監査表示か=false;this.プロジェクト表示か=false;this.選択DBID=ID;this.選択スキーマ名="";this.作成入力を閉じる();
        this.選択作業文書ID=null;if(this.文書一覧.ContainsKey(ID)||this.公開情報表示か(ID)){this.選択作業文書ID=this.対象文書を取得(ID).ID;}
    }
    private void コード変更時(文書状態 文書,ChangeEventArgs 引数){
        if(文書.未確定操作 is not null||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){return;}
        var コード=引数.Value?.ToString()??"";
        文書.定義=文書.定義 with{Source=文書.定義.Source with{Files=文書.定義.Source.Files.Select(ファイル=>{if(ファイル.Path==文書.選択ファイル){return ファイル with{Text=コード};}return ファイル;}).ToArray()}};
        文書.プレビュー=null;
    }
    private Task 下書きを保管Async(文書状態 文書)=>this.処理Async(async()=>{
        var 結果=await this.下書き保管本体Async(文書);
        this.案内=表示文言.取得("下書きを保管しました · r")+結果.Revision;
        try{var build=await this.管理操作.BuildAsync(new(文書.定義.Id,結果.Revision),this.寿命.Token);this.保持計画=build.Plans;}catch{}
    });
    private async Task<Draft> 下書き保管本体Async(文書状態 文書){
        this.ツリーの入力を確認(文書);
        await this.定義編集を同期Async(文書);
        var 保存定義=文書.定義;
        var 結果=await this.管理操作.SaveDraftAsync(new(保存定義,文書.下書き版,文書.基準版,文書.基準版),this.寿命.Token);
        文書.保管完了(保存定義,結果);return 結果;
    }
    private async Task メニューから変更を確認Async(文書状態 文書){
        var 選択時=this.選択作業文書ID;
        await this.変更を確認Async(文書);
        if(!this.エラーか&&文書.プレビュー is not null&&選択時==this.選択作業文書ID){this.文書を選ぶ(文書.DB.Id);}
    }
    private Task 変更を確認Async(文書状態 文書)=>this.処理Async(async()=>{
        this.ツリーの入力を確認(文書);
        await this.定義編集を同期Async(文書);
        if(文書.未保管か||文書.下書き版==0){await this.下書きを保管Async(文書);if(this.エラーか){return;}}
        if(文書.未保管か){this.案内=表示文言.取得("保管中に追加の編集がありました。変更をもう一度確認してください。");return;}
        var 検証定義=文書.定義;
        var キー=文書.検証キー;ValidationDatabase? 検証=null;
        try{
            if(文書.定義.Kind==DefinitionKind.Database){検証=await this.管理操作.CreateValidationDraftAsync(キー,this.寿命.Token);}
            var 結果=await this.管理操作.PreviewAsync(キー,this.寿命.Token);
            if(文書.プレビューを設定(検証定義,結果)){this.案内=表示文言.取得("検証しました。変更内容を確認して適用してください。");}
            else{this.案内=表示文言.取得("検証中に追加の編集がありました。変更をもう一度確認してください。");}
        }
        finally{if(検証 is not null){await this.管理操作.DropValidationAsync(検証.Id);}}
    });
    private Task 変更を適用Async(文書状態 文書)=>this.処理Async(async()=>{
        this.ツリーの入力を確認(文書);
        if(文書.未保管か||文書.未確定操作 is not null){throw new InvalidOperationException("最新の変更を確認するか、操作結果を照会してください。");}
        var 確認=文書.プレビュー??throw new InvalidOperationException("変更を確認してください。");
        var 要求=new ApplyRequest(文書.定義.Id,文書.下書き版,確認.Token,Guid.NewGuid()){BundleHash=確認.BundleHash};
        文書.未確定操作=要求;
        var 結果=await this.管理操作.ApplyAsync(要求,this.寿命.Token);
        if(結果.State!=OperationState.Succeeded){throw new InvalidOperationException(結果.State+" · "+結果.Error);}
        文書.公開完了(結果.PublishedVersion!.Value);
        await this.更新Async();this.案内=表示文言.取得("変更を公開しました · v")+文書.基準版;
    });
    private Task 操作結果照会Async(文書状態 文書)=>this.処理Async(async()=>{
        var 要求=文書.未確定操作??throw new InvalidOperationException("照会対象の操作がありません。");
        var 結果=await this.管理操作.GetOperationAsync(要求.OperationId,this.寿命.Token);
        this.案内=表示文言.取得("操作状態 · ")+結果.State;
        if(結果.State==OperationState.Succeeded){文書.公開完了(結果.PublishedVersion!.Value);await this.更新Async();}
        else if(結果.State is OperationState.Failed or OperationState.Cancelled){文書.未確定操作=null;this.案内+=" · "+結果.Error;}
    });
    private void 新規DB開始時(){
        if(!this.DB作成できるか){this.案内=表示文言.取得("DB作成の権限が必要です。");return;}
        if(this.新規作成入力 is not null){this.案内=表示文言.取得("入力中の定義を下書きへ反映してから、別の追加操作を開始してください。");this.エラーか=true;return;}
        if(this.DB作成中か||this.DB名前変更中か){return;}
        if(this.DB名前変更要求 is not null){this.案内=表示文言.取得("名前変更の結果を先に照会してください。");return;}
        if(this.新DB未確定操作 is not null){this.案内=表示文言.取得("作成中の操作結果を照会してください。");return;}
        this.作成入力を閉じる();this.DB名編集中か=true;this.新DB名="";
    }
    private async Task DB名確定時Async(string 名前){
        if(!this.DB名編集中か||this.DB作成中か||this.新DB未確定操作 is not null){return;}
        if(string.IsNullOrWhiteSpace(名前)){this.DB名編集中か=false;return;}
        this.新DB名=名前;
        var 作成開始時の選択世代=this.対象選択世代;
        this.DB作成中か=true;
        await this.処理Async(async()=>{
            var ID=Guid.NewGuid();var 定義=DatabaseDesigner.Empty("db-"+ID.ToString("N"),this.新DB名);
            var 下書き=await this.管理操作.SaveDraftAsync(new(定義,0,0),this.寿命.Token);var キー=new DraftKey(定義.Id,下書き.Revision);
            var 検証=await this.管理操作.CreateValidationDraftAsync(キー,this.寿命.Token);
            try{
                var 確認=await this.管理操作.PreviewAsync(キー,this.寿命.Token);
                if(!確認.CanApply){throw new InvalidOperationException(string.Join("\n",確認.Diagnostics.Select(項目=>項目.Message)));}
                this.新DB定義ID=定義.Id;this.新DB未確定操作=new(定義.Id,下書き.Revision,確認.Token,ID);
                var 結果=await this.管理操作.ApplyAsync(this.新DB未確定操作,this.寿命.Token);
                if(結果.State!=OperationState.Succeeded){if(結果.State is OperationState.Failed or OperationState.Cancelled){this.新DB未確定操作=null;}throw new InvalidOperationException(結果.Error);}
                this.新DB未確定操作=null;
                this.DB名編集中か=false;
                await this.更新Async();
                if(this.対象選択世代==作成開始時の選択世代){
                    var DB=this.DB一覧.Single(項目=>項目.DefinitionId==定義.Id);
                    await this.DBを開くAsync(DB.Id);
                    if(this.対象選択世代==作成開始時の選択世代+1&&this.ソース操作できるか){
                        this.DB保存先指定中か=true;this.保存先フォーカス待ちか=true;this.新プロジェクト保存先="";
                        this.作成完了定義=定義;this.作成完了DB名=定義.Database;
                    }
                }
                this.案内=表示文言.取得("データベース「{0}」を作成しました。",定義.Database);
            }finally{await this.管理操作.DropValidationAsync(検証.Id);}
        });
        this.DB作成中か=false;
    }
    private async Task 保存先入力時Async(KeyboardEventArgs 引数){
        if(引数.Key=="Escape"){this.プロジェクト生成スキップ();return;}
        if(引数.Key=="Enter"&&!this.プロジェクト生成中か&&!string.IsNullOrWhiteSpace(this.新プロジェクト保存先)){await this.プロジェクト生成実行Async();}
    }
    private Task プロジェクト生成実行Async()=>this.処理Async(async()=>{
        if(this.作成完了定義 is null||string.IsNullOrWhiteSpace(this.作成完了DB名)||string.IsNullOrWhiteSpace(this.新プロジェクト保存先)){return;}
        this.プロジェクト生成中か=true;
        try{
            if(this.管理操作 is not IManagementSourceOperations sourceOps){throw new InvalidOperationException("この接続ではプロジェクトを生成できません。");}
            if(this.表入力中か||this.スキーマ入力中か||this.問い合わせ入力 is not null){throw new ManagementException("Busy","入力中の定義を下書きへ反映してからプロジェクトを保存してください。");}
            var 書出し=new DefinitionExport(this.作成完了定義,1,0);
            var 文書=this.文書一覧.Values.FirstOrDefault(候補=>候補.定義.Id==this.作成完了定義.Id);
            if(文書 is not null){
                if(文書.未確定操作 is not null){throw new ManagementException("Busy","適用操作の結果を確認してからプロジェクトを保存してください。");}
                await this.下書き保管本体Async(文書);
                if(文書.未保管か){throw new ManagementException("Conflict","保管中の追加入力を確認してからプロジェクトを保存してください。");}
                書出し=new(文書.定義.Freeze(),文書.基準版,文書.下書き版);
            }
            var request=new SourceProjectCreateRequest(this.新プロジェクト保存先,this.作成完了DB名,書出し);
            this.作成済みプロジェクト=await sourceOps.CreateSourceProjectAsync(request,this.寿命.Token);
            this.プロジェクト表示か=true;
            this.DB保存先指定中か=false;
            this.案内=表示文言.取得("データベース「{0}」の専用C#プロジェクトを保存しました。",this.作成完了DB名);
        }finally{this.プロジェクト生成中か=false;}
    });
    private void プロジェクト生成スキップ(){this.DB保存先指定中か=false;this.新プロジェクト保存先="";this.作成完了定義=null;this.作成完了DB名=null;}
    private void プロジェクト保存開始時(文書状態 文書){
        if(this.表入力中か||this.スキーマ入力中か||this.問い合わせ入力 is not null){this.案内=表示文言.取得("入力中の定義を下書きへ反映してからプロジェクトを保存してください。");this.エラーか=true;return;}
        this.文書を選ぶ(文書.DB.Id);
        this.作成完了定義=文書.定義;this.作成完了DB名=文書.DB.Name;this.新プロジェクト保存先="";
        this.DB保存先指定中か=true;this.保存先フォーカス待ちか=true;this.プロジェクト表示か=false;
    }
    private Task DB作成結果照会時Async()=>this.処理Async(async()=>{
        var 要求=this.新DB未確定操作??throw new InvalidOperationException("照会対象の操作がありません。");
        var 結果=await this.管理操作.GetOperationAsync(要求.OperationId,this.寿命.Token);this.案内=表示文言.取得("DB作成 · ")+結果.State;
        if(結果.State==OperationState.Succeeded){this.新DB未確定操作=null;this.DB名編集中か=false;await this.更新Async();await this.DBを開くAsync(this.DB一覧.Single(DB=>DB.DefinitionId==this.新DB定義ID).Id);}
        else if(結果.State is OperationState.Failed or OperationState.Cancelled){this.新DB未確定操作=null;this.案内+=" · "+結果.Error;}
    });
    private void 表データ表示時(string 表){if(this.選択DBID is{} ID&&this.公開情報表示か(ID)){var 公開項目=this.公開DB一覧[ID].オブジェクト.FirstOrDefault(項目=>項目.Kind is "Table" or "Set"&&項目.Id==表);if(公開項目 is not null){this.公開対象を開く(ID,公開項目);}return;}this.ツリー所属選択中か=false;this.選択表名=表;this.実行問い合わせ=null;this.計画対象=null;this.計画対象名=null;this.表示=表示種別.データ;if(this.現在文書?.オブジェクト.FirstOrDefault(項目=>項目.Id==表&&項目.Kind is "Table" or "Set") is{} 項目){this.対象を文書で開く(項目);}}
    private void 表作成開始時(文書状態 文書,string? 所属=null){var スキーマ=所属??this.選択スキーマ名;this.作成入力を閉じる();if(!this.作成場所を開く(文書.DB.Id,スキーマ,"Table")){return;}this.表示=表示種別.構造;this.編集前の表=null;this.表名="NewTable";this.スキーマ名=スキーマ;this.選択スキーマ名=this.スキーマ名;this.入力列.Clear();this.入力列.Add(new(){名前="Id",主キーか=true});this.入力列.Add(new(){名前="Name",型="string"});this.表入力中か=true;}
    private void 表編集開始時(文書状態 文書,string 表){
        if(!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)||文書.未確定操作 is not null){this.案内=表示文言.取得("現在このテーブルを編集できません。");return;}
        try{
            var 設計=DatabaseDesigner.Read(文書.定義,表);
            this.表示=表示種別.構造;
            this.作成入力を閉じる();this.編集前の表=設計;this.表名=設計.Name;this.スキーマ名=設計.Schema;this.選択スキーマ名=設計.Schema;this.入力列.Clear();
            this.入力列.AddRange(設計.Columns.Select(列=>new 列入力{名前=列.Name,型=列.Type,主キーか=列.PrimaryKey,元の名前=列.OriginalName,式=列.Expression}));
            this.ソース表示か=false;this.表入力中か=true;
            if(文書.オブジェクト.FirstOrDefault(項目=>項目.Id==表&&項目.Kind=="Table") is{} 項目){this.対象を文書で開く(項目,"構造");this.表入力中か=false;}
        }catch(ManagementException 例外){this.エラーか=true;this.案内=例外.Message;}
    }
    private void 表を下書きに追加時(文書状態 文書){
        if(!this.作成先が一致するか(文書)||文書.未確定操作 is not null||!this.権限があるか(文書.DB.Name,ManagementPermission.Edit)){this.案内=表示文言.取得("現在このテーブルを編集できません。");return;}
        try{文書.定義=DatabaseDesigner.Apply(文書.定義,new(this.表名,this.スキーマ名,this.入力列.Select(列=>new ColumnDesign(列.名前,列.型,列.主キーか,列.元の名前,列.式)).ToArray(),this.編集前の表?.ForeignKeys??[],this.編集前の表?.OriginalPath));文書.プレビュー=null;this.新規作成入力=null;this.表入力中か=false;this.案内=表示文言.取得("テーブルを下書きへ反映しました。稼働DBへ未適用です。");var 項目=文書.オブジェクト.Single(項目=>項目.Kind=="Table"&&項目.Id==string.Join(".",new[]{this.スキーマ名,this.表名}.Where(値=>値.Length>0)));this.対象を文書で開く(項目,"構造");}
        catch(Exception 例外){this.エラーか=true;this.案内=例外.Message;}
    }
    private async Task メニュー要求時Async(対象操作要求 要求){
        this.メニューID=要求.ID;var ID=要求.ID;var 種類="DB";string? 表=null;var 所属="";string? 追加種類=null;
        string? タブID=null;if(ID=="databases"){種類="分類";}else if(ID.StartsWith("tab:",StringComparison.Ordinal)){種類="文書";タブID=ID[4..];ID=タブID;if(this.対象文書一覧.TryGetValue(タブID,out var 作業文書)){ID=作業文書.DBID;}}
        else if(ID.StartsWith("table:",StringComparison.Ordinal)){種類="表";表=ID[6..];ID=this.選択DBID!;}
        else if(ID.StartsWith("query:",StringComparison.Ordinal)){種類="問い合わせ";表=ID[6..];if(表.StartsWith("registered:",StringComparison.Ordinal)){種類="登録関数";}ID=this.選択DBID!;}
        else if(ID.StartsWith("schema:",StringComparison.Ordinal)){種類="スキーマ";所属=ID[7..];ID=this.選択DBID!;}
        else if(ID.StartsWith("group:",StringComparison.Ordinal)){var 部分=ID.Split(':',3);種類="オブジェクト分類";追加種類=部分[1];所属=部分[2];ID=this.選択DBID!;}
        this.メニュー対象=new(種類,ID,表,所属,追加種類){文書ID=タブID};this.メニュー見出し=表示文言.取得("操作");var DB=this.対象DB(this.メニュー対象);
        if(DB is not null){
            this.メニュー見出し=DB.Name;
            if(!this.文書一覧.ContainsKey(ID)&&!DB.IsSystem&&this.権限があるか(DB.Name,ManagementPermission.Edit)){await this.処理Async(()=>this.DB文書を読み込むAsync(DB));}
        }
        if(所属.Length>0){this.メニュー見出し+=" / "+所属;}if(表 is not null){this.メニュー見出し=表;}
        this.メニュー起点=要求.起点;this.メニューX=要求.X;this.メニューY=要求.Y;this.パレットか=false;this.メニュー世代++;
    }
    private void メニューを閉じる時(){this.メニュー対象=null;this.メニューID=null;}
    private async Task コマンド選択時Async(string ID){var 対象=this.メニュー対象;this.メニューを閉じる時();if(対象 is not null&&this.コマンド is not null){await this.処理Async(()=>this.コマンド.実行Async(ID,対象));}}
    private Task 文書のメニュー時Async()=>this.メニュー要求時Async(new("tab:"+this.選択DBID,this.文書操作起点,0,0));
    [JSInvokable]public Task パレットを開くAsync(){this.メニュー対象=new("分類","databases");if(this.現在文書 is not null){this.メニュー対象=new("文書",this.選択DBID!){文書ID=this.選択作業文書ID};}this.メニュー見出し=表示文言.取得("コマンド");this.メニュー起点=this.根;this.パレットか=true;this.メニュー世代++;return this.InvokeAsync(this.StateHasChanged);}
    [JSInvokable]public Task ドラッグ開始Async(string ID){this.メニューを閉じる時();this.タブ順序.ドラッグ開始(ID[4..]);return Task.CompletedTask;}
    [JSInvokable]public Task ドロップAsync(string ID,bool 前か){this.タブ順序.移動先を設定(ID[4..],前か);this.タブ順序.ドロップ();return this.InvokeAsync(this.StateHasChanged);}
    [JSInvokable]public Task ドラッグ取消Async(){this.タブ順序.取消();return Task.CompletedTask;}
    private string? タブ移動先(string ID,int 差){
        var 表示タブ=this.表示文書タブ().ToArray();var 位置=Array.IndexOf(表示タブ,ID);var 次=位置+差;
        if(位置<0||次<0||次>=表示タブ.Length){return null;}
        return 表示タブ[次];
    }
    private void タブを移動(string ID,int 差){if(this.タブ移動先(ID,差) is not{} 次){return;}this.タブ順序.ドラッグ開始(ID);this.タブ順序.移動先を設定(次,差<0);this.タブ順序.ドロップ();}
    public async ValueTask DisposeAsync(){
        if(this.破棄済みか){return;}
        this.破棄済みか=true;
        try{this.寿命.Cancel();try{await スクリプト参照.解放Async(this.オブジェクト順操作,this.オブジェクト順モジュール);}finally{try{await スクリプト参照.解放Async(this.サイドバー操作,this.サイドバーモジュール);}finally{try{await スクリプト参照.解放Async(this.表示状態操作,this.表示状態モジュール);}finally{await スクリプト参照.解放Async(this.相互操作,this.モジュール);}}}}
        finally{this.参照?.Dispose();this.寿命.Dispose();}
    }
    private sealed record 管理対象(string 種類,string ID,string? 表=null,string スキーマ="",string? 追加種類=null){public string? 文書ID{get;init;}}
    private sealed class 列入力{public string 名前{get;set;}="Column";public string 型{get;set;}="int";public bool 主キーか{get;set;}public string? 元の名前{get;set;}public string? 式{get;set;}}
}
