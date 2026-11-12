namespace Seamless.Management.Server;

public sealed record ReadAuditOptions(TimeSpan Window,TimeSpan? SaveInterval=null,int MaximumKeys=65536,long MaximumBytes=64L<<20,int MaximumBatch=1024){
    public static TimeSpan DefaultWindow=>TimeSpan.FromMinutes(1);
    public static TimeSpan DefaultSaveInterval=>TimeSpan.FromSeconds(5);
    public ReadAuditOptions():this(DefaultWindow){}
    internal TimeSpan Interval=>this.SaveInterval??DefaultSaveInterval;
    internal static ReadAuditOptions? ForServer(int format,string? window,string? interval,bool disabled=false){
        if(disabled){if(window is not null||interval is not null){throw new ArgumentException("--no-read-auditと集計期間・保存間隔は同時に指定できません。");}return null;}
        if(format<5){if(window is not null||interval is not null){throw new ManagementException("MigrationRequired","通常読み取りの集計には形式5が必要です。CLIのupgrade-auditで明示更新してください。");}return null;}
        var options=new ReadAuditOptions();
        if(window is not null){if(!int.TryParse(window,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out var seconds)||seconds is <1 or >86400){throw new ArgumentException("集計期間は1〜86400秒の整数で指定してください。");}options=options with{Window=TimeSpan.FromSeconds(seconds)};}
        if(interval is not null){if(!double.TryParse(interval,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var seconds)||!double.IsFinite(seconds)||seconds<0.01||seconds>86400){throw new ArgumentException("保存間隔は0.01〜86400秒で指定してください。");}options=options with{SaveInterval=TimeSpan.FromSeconds(seconds)};}options.Validate();return options;
    }
    internal void Validate(){if(this.Window<TimeSpan.FromSeconds(1)||this.Window>TimeSpan.FromDays(1)||this.Interval<TimeSpan.FromMilliseconds(10)||this.Interval>TimeSpan.FromDays(1)||this.MaximumKeys is <1 or >1000000||this.MaximumBytes is <1024 or >1L<<30||this.MaximumBatch is <1 or >1024){throw new ArgumentException("集計期間・保存間隔・バッファ上限が不正です。");}}
}
