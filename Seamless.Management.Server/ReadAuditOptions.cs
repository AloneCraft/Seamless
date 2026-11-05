namespace Seamless.Management.Server;

public sealed record ReadAuditOptions(TimeSpan Window,TimeSpan? SaveInterval=null,int MaximumKeys=65536,long MaximumBytes=64L<<20,int MaximumBatch=1024){
    internal TimeSpan Interval=>this.SaveInterval??TimeSpan.FromSeconds(5);
    internal void Validate(){if(this.Window<TimeSpan.FromSeconds(1)||this.Window>TimeSpan.FromDays(1)||this.Interval<TimeSpan.FromMilliseconds(10)||this.Interval>TimeSpan.FromDays(1)||this.MaximumKeys is <1 or >1000000||this.MaximumBytes is <1024 or >1L<<30||this.MaximumBatch is <1 or >1024){throw new ArgumentException("集計期間・保存間隔・バッファ上限が不正です。");}}
}
