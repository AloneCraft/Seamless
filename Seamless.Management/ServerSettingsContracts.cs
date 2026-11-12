namespace Seamless.Management;

public enum ManagementExecutionMode{Embedded,Server}
public sealed record ManagementServerSettings{
    public string Address{get;init;}="127.0.0.1";
    public int Port{get;init;}=14336;
    public int ManagerPort{get;init;}
    public string? ExternalManagerOrigin{get;init;}
    public string ExternalManagerAddress{get;init;}="0.0.0.0";
    public long? MaximumBufferedRequestBytes{get;init;}
    public long? MaximumPendingWriteBytes{get;init;}
    public TimeSpan? ResponseWriteTimeout{get;init;}
    public bool ReadAuditEnabled{get;init;}=true;
    public int ReadAuditWindowSeconds{get;init;}=60;
    public double ReadAuditSaveSeconds{get;init;}=5;

    public ManagementServerSettings Validate(){
        if(!System.Net.IPAddress.TryParse(this.Address,out var address)||this.Port is <0 or >65535||this.ManagerPort is <0 or >65535||
            !System.Net.IPAddress.TryParse(this.ExternalManagerAddress,out var externalAddress)){
            throw new ManagementException("Validation","IPアドレスと0〜65535のポートを指定してください。0は自動割り当てです。");
        }
        if(this.MaximumBufferedRequestBytes is<=0||this.MaximumPendingWriteBytes is<=0||this.ResponseWriteTimeout is{} timeout&&(timeout<=TimeSpan.Zero||timeout.TotalMilliseconds>uint.MaxValue-1)){
            throw new ManagementException("Validation","通信予算と応答期限には正の値を指定してください。");
        }
        if(this.ReadAuditWindowSeconds is <1 or >86400||!double.IsFinite(this.ReadAuditSaveSeconds)||this.ReadAuditSaveSeconds is <0.01 or >86400){
            throw new ManagementException("Validation","参照監査の集計期間は1〜86400秒、保存間隔は0.01〜86400秒で指定してください。");
        }
        string? origin=null;
        if(this.ExternalManagerOrigin is{} value){
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Port is <1 or >65535||uri.AbsolutePath!="/"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0){
                throw new ManagementException("Validation","外部ManagerにはHTTPSの公開起点を指定してください。");
            }
            origin=uri.AbsoluteUri;
        }
        return this with{Address=address.ToString(),ExternalManagerAddress=externalAddress.ToString(),ExternalManagerOrigin=origin};
    }
}
public sealed record ServerSettingsSnapshot(long Version,ManagementServerSettings Running,ManagementServerSettings Next,bool RestartRequired,
    string[] CommandLineOverrides,string Root,string Certificate,string? ConfigurationFile,int ProcessId,int ActualTcpPort,string ManagerOrigin){
    public ManagementExecutionMode ExecutionMode{get;init;}=ManagementExecutionMode.Server;
    public string[] UnavailableProperties{get;init;}=[];
}
public sealed record ServerSettingsEdit(long ExpectedVersion,ManagementServerSettings Settings);
