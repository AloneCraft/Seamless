using System.Diagnostics;
using System.Globalization;

namespace UI;

internal readonly record struct ベンチマーク実行予測値(
    TimeSpan 平均実行時間,
    TimeSpan 残り時間,
    DateTimeOffset 終了予定日時);

internal sealed class ベンチマーク実行予測時計 {
    private readonly Stopwatch 時計=new();

    public TimeSpan 経過時間=>this.時計.Elapsed;

    public void 開始()=>this.時計.Restart();

    public void 再開()=>this.時計.Start();

    public void 一時停止()=>this.時計.Stop();

    public void 終了()=>this.時計.Stop();

    public void クリア()=>this.時計.Reset();
}

internal static class ベンチマーク実行予測ロジック {
    public const string 予測なし文字列="平均 --/件・残り --・終了予定 --";

    public static ベンチマーク実行予測値? 計算(
        TimeSpan 経過時間,
        int 終端件数,
        int 合計件数,
        DateTimeOffset 現在日時) {
        if(経過時間<TimeSpan.Zero||終端件数<=0||合計件数<=0) {
            return null;
        }

        var 有効終端件数=Math.Min(終端件数,合計件数);
        var 平均Ticks=Math.Max(1L,経過時間.Ticks/有効終端件数);
        var 残件数=Math.Max(0,合計件数-有効終端件数);
        long 残りTicks;
        if(残件数==0) {
            残りTicks=0;
        }else{
            残りTicks=(long)Math.Min(TimeSpan.MaxValue.Ticks,平均Ticks*(double)残件数);
        }
        var 平均=TimeSpan.FromTicks(平均Ticks);
        var 残り=TimeSpan.FromTicks(残りTicks);
        return new(平均,残り,現在日時.Add(残り));
    }

    public static string 表示文字列(
        TimeSpan 経過時間,
        int 終端件数,
        int 合計件数,
        DateTimeOffset 現在日時) {
        var 予測=計算(経過時間,終端件数,合計件数,現在日時);
        if(予測 is null) {
            return 予測なし文字列;
        }

        var 終了予定=string.Empty;
        if(予測.Value.残り時間==TimeSpan.Zero) {
            終了予定="終了済み";
        }else{
            終了予定=予測.Value.終了予定日時.ToLocalTime().ToString("HH:mm",CultureInfo.InvariantCulture);
        }
        return "平均 "+時間文字列(予測.Value.平均実行時間)+"/件・残り "+時間文字列(予測.Value.残り時間)+"・終了予定 "+終了予定;
    }

    private static string 時間文字列(TimeSpan 時間) {
        if(時間>=TimeSpan.FromDays(1)) {
            return ((int)時間.TotalDays)+"日"+時間.Hours.ToString(CultureInfo.InvariantCulture)+"時間";
        }
        if(時間>=TimeSpan.FromHours(1)) {
            return ((int)時間.TotalHours)+"時間"+時間.Minutes.ToString(CultureInfo.InvariantCulture)+"分";
        }
        if(時間>=TimeSpan.FromMinutes(1)) {
            return ((int)時間.TotalMinutes)+"分"+時間.Seconds.ToString(CultureInfo.InvariantCulture)+"秒";
        }
        if(時間>=TimeSpan.FromSeconds(1)) {
            return 時間.TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+"秒";
        }
        if(時間>=TimeSpan.FromMilliseconds(1)) {
            return 時間.TotalMilliseconds.ToString("0",CultureInfo.InvariantCulture)+"ms";
        }
        return "0秒";
    }
}
