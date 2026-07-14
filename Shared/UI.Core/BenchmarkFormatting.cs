using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
public static class ベンチマークフォーマッター{
    public static string ナノ秒を整形(double?ナノ秒){
        if(ナノ秒 is not { } 値||!double.IsFinite(値)||値<0){
            return string.Empty;
        }

        if(値>=1_000_000_000){
            return (値/1_000_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" s";
        }

        if(値>=1_000_000){
            return (値/1_000_000).ToString("0.##",CultureInfo.InvariantCulture)+" ms";
        }

        if(値>=1_000){
            return (値/1_000).ToString("0.##",CultureInfo.InvariantCulture)+" μs";
        }

        return 値.ToString("0.##",CultureInfo.InvariantCulture)+" ns";
    }

    public static string バイト数を整形(long?バイト数){
        if(バイト数 is not { } 値||値<0){
            return string.Empty;
        }

        if(値>=1024L*1024L*1024L){
            return (値/(1024d*1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" GB";
        }

        if(値>=1024L*1024L){
            return (値/(1024d*1024d)).ToString("0.##",CultureInfo.InvariantCulture)+" MB";
        }

        if(値>=1024L){
            return (値/1024d).ToString("0.##",CultureInfo.InvariantCulture)+" KB";
        }

        return 値.ToString(CultureInfo.InvariantCulture)+" B";
    }

    public static string 名前を短縮(string 名前){
        const int 最大長=48;
        if(名前.Length<=最大長){
            return 名前;
        }
        return 名前[..(最大長-1)]+"...";
    }
}
