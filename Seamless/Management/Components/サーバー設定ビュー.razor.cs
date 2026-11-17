using Microsoft.AspNetCore.Components;

namespace Seamless.Management.Components;

public partial class サーバー設定ビュー{
    [Parameter]public bool 表示中か{get;set;}
    [Parameter]public EventCallback<bool> 未保存変更時{get;set;}
    private ServerSettingsSnapshot? 情報;
    private ManagementServerSettings 編集値=new();
    private double? 応答期限ミリ秒;
    private bool 読込中か,保存中か,エラーか;
    private string 案内="";
    private long 世代;
    private readonly CancellationTokenSource 寿命=new();
    private bool 処理中か=>this.読込中か||this.保存中か;
    private bool 編集ありか=>this.情報 is{} 元&&(this.編集値!=元.Next||this.応答期限ミリ秒!=元.Next.ResponseWriteTimeout?.TotalMilliseconds);
    private bool 固定か(string 項目)=>this.情報 is{} 元&&(元.CommandLineOverrides.Contains(項目,StringComparer.Ordinal)||元.UnavailableProperties.Contains(項目,StringComparer.Ordinal));
    protected override async Task OnParametersSetAsync(){if(this.表示中か&&this.情報 is null){await this.再取得Async();}}
    public async Task 再取得Async(){
        if(this.処理中か){return;}
        if(this.編集ありか){this.エラーか=true;this.案内=表示文言.取得("編集を保存するか取り消してから再取得してください。");return;}
        var 現在世代=++this.世代;this.読込中か=true;this.エラーか=false;this.案内="";
        try{var 結果=await this.管理操作.ServerSettingsAsync(this.寿命.Token);if(現在世代!=this.世代){return;}this.情報=結果;await this.入力復元Async();}
        catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        catch(Exception 例外){if(現在世代==this.世代){this.エラーか=true;this.案内=表示文言.取得("設定の取得に失敗しました。")+例外.Message;}}
        finally{if(現在世代==this.世代){this.読込中か=false;this.StateHasChanged();}}
    }
    private Task 値変更時Async(ManagementServerSettings 値){this.編集値=値;return this.変更通知Async();}
    private Task 外部変更時Async(ChangeEventArgs 引数){string? 起点=null;if(引数.Value is true){起点="https://localhost:443/";}return this.値変更時Async(this.編集値 with{ExternalManagerOrigin=起点});}
    private Task 変更通知Async()=>this.未保存変更時.InvokeAsync(this.編集ありか);
    private Task 入力復元Async(){if(this.情報 is{} 元){this.編集値=元.Next;this.応答期限ミリ秒=元.Next.ResponseWriteTimeout?.TotalMilliseconds;}return this.変更通知Async();}
    private Task 取消時Async(){this.案内="";this.エラーか=false;return this.入力復元Async();}
    public async Task 保存Async(){
        if(this.処理中か||!this.編集ありか||this.情報 is null){return;}
        var 現在世代=this.世代;this.保存中か=true;this.案内="";this.エラーか=false;
        try{
            TimeSpan? 期限=null;if(this.応答期限ミリ秒 is{} 値){if(!double.IsFinite(値)||値<=0||値>uint.MaxValue-1){throw new ManagementException("Validation","応答送信期限が設定範囲外です。");}期限=TimeSpan.FromMilliseconds(値);}
            var 要求=new ServerSettingsEdit(this.情報.Version,this.編集値 with{ResponseWriteTimeout=期限});
            var 結果=await this.管理操作.SaveServerSettingsAsync(要求,this.寿命.Token);if(現在世代!=this.世代){return;}
            this.情報=結果;await this.入力復元Async();this.案内=表示文言.取得("設定を保存しました。");if(結果.RestartRequired){this.案内+=表示文言.取得("次回起動時に反映されます。");}
        }catch(OperationCanceledException)when(this.寿命.IsCancellationRequested){}
        catch(Exception 例外){if(現在世代==this.世代){this.エラーか=true;this.案内=表示文言.取得("保存に失敗しました。入力は保持しています。")+例外.Message;}}
        finally{if(現在世代==this.世代){this.保存中か=false;this.StateHasChanged();}}
    }
    private static string 項目名(string 名前)=>名前 switch{
        nameof(ManagementServerSettings.Address)=>"管理TCPのIP",nameof(ManagementServerSettings.Port)=>"管理TCPのポート",nameof(ManagementServerSettings.ManagerPort)=>"ローカルManagerのポート",
        nameof(ManagementServerSettings.ExternalManagerOrigin)=>"外部Managerの起点",nameof(ManagementServerSettings.ExternalManagerAddress)=>"外部ManagerのIP",
        nameof(ManagementServerSettings.MaximumBufferedRequestBytes)=>"受信予算",nameof(ManagementServerSettings.MaximumPendingWriteBytes)=>"送信待ち予算",nameof(ManagementServerSettings.ResponseWriteTimeout)=>"応答期限",
        nameof(ManagementServerSettings.ReadAuditEnabled)=>"参照監査の有効設定",nameof(ManagementServerSettings.ReadAuditWindowSeconds)=>"集計期間",nameof(ManagementServerSettings.ReadAuditSaveSeconds)=>"保存間隔",_=>名前};
    public void Dispose(){this.世代++;this.寿命.Cancel();this.寿命.Dispose();}
}
