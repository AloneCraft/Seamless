using System.Globalization;

namespace Seamless.Management.Components;

public partial class 監査ビュー{
    private enum 監査種別{イベント,参照集計,転記状態}
    private 監査種別 表示種類;
    private string 検索語="",DB条件="",利用者条件="";
    private DateTime? 開始日時,終了日時;
    private AuditAction? 選択操作;
    private AuditOutcome? 選択結果;
    private AuditQuery? 現在条件;
    private AuditEvent[] イベント一覧=[];
    private AuditReadEntry[] 集計一覧=[];
    private AuditDeliveryState[] 転記一覧=[];
    private AuditReadBufferStatus? 参照状態;
    private bool 読込中か,エラーか,続きありか,転記中か;
    private string 案内="";
    private string? 転記エラー;
    private long 次位置,上限位置,読込世代;
    private int 走査件数;
    private CancellationTokenSource? 読込取消;
    private int 表示件数=>this.表示種類 switch{監査種別.イベント=>this.イベント一覧.Length,監査種別.参照集計=>this.集計一覧.Length,_=>this.転記一覧.Length};

    protected override Task OnInitializedAsync()=>this.検索Async();
    public Task 検索Async()=>this.読込Async(false);
    public Task 続き取得Async(){if(this.読込中か||!this.続きありか){return Task.CompletedTask;}return this.読込Async(true);}
    private Task 表示変更時Async(監査種別 種類){
        if(this.表示種類==種類){return Task.CompletedTask;}
        this.表示種類=種類;this.イベント一覧=[];this.集計一覧=[];this.転記一覧=[];this.参照状態=null;this.続きありか=false;
        return this.検索Async();
    }
    private async Task 読込Async(bool 続きか){
        this.読込取消?.Cancel();this.読込取消?.Dispose();this.読込取消=new();
        var 取消=this.読込取消.Token;var 世代=++this.読込世代;
        this.読込中か=true;this.エラーか=false;this.案内="";this.StateHasChanged();
        try{
            var 条件=new AuditQuery(UTC(this.開始日時),UTC(this.終了日時),空なら無指定(this.DB条件),空なら無指定(this.利用者条件),this.選択操作,this.選択結果,Text:空なら無指定(this.検索語));
            if(続きか&&this.現在条件 is{} 前条件){条件=前条件 with{Before=this.次位置,Ceiling=this.上限位置};}
            switch(this.表示種類){
                case 監査種別.イベント:{
                    var 結果=await this.管理操作.AuditEventsAsync(条件,取消);if(世代!=this.読込世代){return;}
                    this.イベント一覧=結果.Items;this.次位置=結果.NextBefore;this.上限位置=結果.Ceiling;this.続きありか=結果.HasMore;this.走査件数=結果.Examined;break;
                }
                case 監査種別.参照集計:{
                    var 結果=await this.管理操作.ReadSummariesAsync(条件,取消);
                    var 状態=await this.管理操作.ReadAuditStatusAsync(取消);if(世代!=this.読込世代){return;}
                    this.集計一覧=結果.Items;this.参照状態=状態;this.次位置=結果.NextBefore;this.上限位置=結果.Ceiling;this.続きありか=結果.HasMore;this.走査件数=結果.Examined;break;
                }
                case 監査種別.転記状態:{
                    long 位置=0;if(続きか){位置=this.次位置;}
                    var 結果=await this.管理操作.AuditDeliveryStatusAsync(位置,取消);if(世代!=this.読込世代){return;}
                    this.転記一覧=結果.Items;this.転記中か=結果.Running;this.転記エラー=結果.ErrorCode;this.次位置=結果.NextCursor;this.続きありか=結果.HasMore;this.走査件数=0;break;
                }
            }
            this.現在条件=条件;
        }catch(OperationCanceledException)when(取消.IsCancellationRequested){
        }catch(Exception 例外){
            if(世代==this.読込世代){
                if(例外 is ManagementException{Code:"Authentication" or "Permission"}){
                    this.イベント一覧=[];this.集計一覧=[];this.転記一覧=[];this.参照状態=null;
                    this.現在条件=null;this.続きありか=false;this.次位置=0;this.上限位置=0;this.走査件数=0;
                    this.転記中か=false;this.転記エラー=null;
                }
                this.エラーか=true;this.案内=表示文言.取得("監査の取得に失敗しました。")+例外.Message;
            }
        }
        finally{if(世代==this.読込世代){this.読込中か=false;this.StateHasChanged();}}
    }
    private static string? 空なら無指定(string 値){if(string.IsNullOrWhiteSpace(値)){return null;}return 値.Trim();}
    private static DateTimeOffset? UTC(DateTime? 値){if(値 is null){return null;}return new DateTimeOffset(DateTime.SpecifyKind(値.Value,DateTimeKind.Utc));}
    private static string 時刻(DateTimeOffset? 値)=>値?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fff",CultureInfo.InvariantCulture)??"—";
    private static string 操作名(AuditAction 操作)=>表示文言.文脈("監査",操作 switch{AuditAction.Read=>"参照",AuditAction.DataChange=>"データ更新",AuditAction.SchemaChange=>"定義変更",AuditAction.PermissionChange=>"権限変更",AuditAction.AccountChange=>"アカウント変更",AuditAction.SourceRead=>"ソース参照",AuditAction.Validation=>"検証",_=>"その他"});
    private static string 結果名(AuditOutcome 結果)=>表示文言.文脈("監査",結果 switch{AuditOutcome.Succeeded=>"成功",AuditOutcome.Failed=>"失敗",AuditOutcome.Cancelled=>"取消",AuditOutcome.Rejected=>"拒否",_=>"不明"});
    public void Dispose(){this.読込世代++;using var 取消=this.読込取消;this.読込取消=null;取消?.Cancel();}
}
