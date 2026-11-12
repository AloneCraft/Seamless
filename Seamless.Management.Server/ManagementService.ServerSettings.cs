using System.Text.Json;

namespace Seamless.Management.Server;

internal sealed record StoredServerSettings(long Version,ManagementServerSettings Settings);
internal sealed record ServerSettingsHost(ManagementServerSettings Bootstrap,ManagementServerSettings Running,string[] Overrides,
    string Certificate,string? ConfigurationFile,int ActualTcpPort,string ManagerOrigin){
    public ManagementExecutionMode Mode{get;init;}=ManagementExecutionMode.Server;
}

public sealed partial class ManagementService{
    private ServerSettingsHost? serverSettingsHost;
    internal void AttachServerSettings(ServerSettingsHost host){
        ArgumentNullException.ThrowIfNull(host);
        host=host with{Bootstrap=host.Bootstrap.Validate(),Running=host.Running.Validate(),Overrides=host.Overrides.ToArray()};
        lock(this.store.Gate){if(this.serverSettingsHost is not null){throw new InvalidOperationException("起動設定は接続済みです。");}this.serverSettingsHost=host;}
    }
    internal static StoredServerSettings? SavedServerSettings(ManagementStore store)=>store.Read<StoredServerSettings>("serverSettings","active");
    internal static ManagementServerSettings OverlayServerSettings(ManagementServerSettings value,ManagementServerSettings source,IEnumerable<string> properties){
        var result=JsonSerializer.SerializeToNode(value,ManagementJson.Options)!.AsObject();var other=JsonSerializer.SerializeToNode(source,ManagementJson.Options)!.AsObject();
        foreach(var property in properties){var key=ManagementJson.Options.PropertyNamingPolicy?.ConvertName(property)??property;result[key]=other[key]?.DeepClone();}
        return result.Deserialize<ManagementServerSettings>(ManagementJson.Options)!;
    }
    public ServerSettingsSnapshot ServerSettings(UserAccount actor){
        lock(this.store.Gate){actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);return this.ServerSettingsSnapshot();}
    }
    public ServerSettingsSnapshot SaveServerSettings(UserAccount actor,ServerSettingsEdit request){
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Settings);
        var settings=request.Settings.Validate();
        lock(this.store.Gate){
            actor=this.CurrentActor(actor);Require(actor,"*",ManagementPermission.Manage);
            var host=this.serverSettingsHost??throw new ManagementException("Unsupported","管理サーバーの起動設定が接続されていません。");
            var saved=SavedServerSettings(this.store);var raw=saved?.Settings??host.Bootstrap;
            if(request.ExpectedVersion!=(saved?.Version??0)){throw new ManagementException("Conflict","設定が更新されています。入力を確認し、最新の設定を取得してください。");}
            if(OverlayServerSettings(settings,host.Running,host.Overrides)!=settings){throw new ManagementException("ConfigurationOverride","起動引数で指定した項目は、この画面から変更できません。");}
            if(OverlayServerSettings(settings,raw,UnavailableSettings(host.Mode))!=settings){throw new ManagementException("ExecutionMode","この項目はサーバー型で編集してください。");}
            if(settings.ReadAuditEnabled&&this.store.Format<5){throw new ManagementException("MigrationRequired","参照監査は管理領域を形式5へ明示更新してから設定してください。");}
            // 起動引数の一時指定を保存値へ取り込まず、次回も引数 > 保存値 > 起動ファイルの順で解決する。
            var updated=new StoredServerSettings(checked((saved?.Version??0)+1),OverlayServerSettings(settings,raw,host.Overrides));
            this.store.Write(()=>{this.store.Put("serverSettings","active",updated);this.store.AddHistory(actor.Name,"*","ServerSettingsChanged","server-settings",updated.Version);});
            return this.ServerSettingsSnapshot();
        }
    }
    private ServerSettingsSnapshot ServerSettingsSnapshot(){
        var host=this.serverSettingsHost??throw new ManagementException("Unsupported","管理サーバーの起動設定が接続されていません。");
        var saved=SavedServerSettings(this.store);var next=OverlayServerSettings(saved?.Settings??host.Bootstrap,host.Running,host.Overrides);
        return new(saved?.Version??0,host.Running,next,next!=host.Running,host.Overrides.ToArray(),this.store.Root,host.Certificate,host.ConfigurationFile,Environment.ProcessId,host.ActualTcpPort,host.ManagerOrigin){ExecutionMode=host.Mode,UnavailableProperties=UnavailableSettings(host.Mode)};
    }
    private static string[] UnavailableSettings(ManagementExecutionMode mode){
        if(mode==ManagementExecutionMode.Embedded){return[nameof(ManagementServerSettings.Address),nameof(ManagementServerSettings.Port),nameof(ManagementServerSettings.ExternalManagerOrigin),nameof(ManagementServerSettings.ExternalManagerAddress),nameof(ManagementServerSettings.MaximumBufferedRequestBytes),nameof(ManagementServerSettings.MaximumPendingWriteBytes),nameof(ManagementServerSettings.ResponseWriteTimeout)];}return[];
    }
    internal static ReadAuditOptions? AuditOptions(ManagementStore store,ManagementServerSettings settings){
        if(!settings.ReadAuditEnabled){return null;}
        return ReadAuditOptions.ForServer(store.Format,settings.ReadAuditWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),settings.ReadAuditSaveSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
    }
}
