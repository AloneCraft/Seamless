using System.Globalization;

namespace Sample.NetShop.Core;

/// <summary>起動引数から決まる設定。</summary>
public sealed record 実行設定{
    public const long 既定の決算注文件数=1_000_000;

    /// <summary>商店DBを置くフォルダ。閉じても残り、次に起動したとき続きから使う。</summary>
    public required string データフォルダ{get;init;}
    /// <summary>章が一時的に使うフォルダを作る場所。再生の終わりに中身ごと消す。</summary>
    public required string 作業フォルダ{get;init;}
    public long 決算の注文件数{get;init;}=既定の決算注文件数;
    /// <summary>メモリを最小限にしてディスク中心で動かすか。</summary>
    public bool 低メモリか{get;init;}
    /// <summary>最初の周で始める章(0始まり)。2周目からは1章から。</summary>
    public int 開始章{get;init;}

    public 資源設定 資源{
        get{
            if(this.低メモリか){
                return 資源設定.最小;
            }else{
                return 資源設定.標準;
            }
        }
    }

    /// <summary>
    /// 起動引数を読む。使える引数は --data &lt;フォルダ&gt;、--orders &lt;件数&gt;、--chapter &lt;章番号(1始まり)&gt;、--low-memory。
    /// 分からない引数は無視せず例外にする(打ち間違いに気付けるように)。
    /// </summary>
    public static 実行設定 引数から作る(IReadOnlyList<string>引数,string 既定のデータフォルダ){
        var データフォルダ=既定のデータフォルダ;
        var 件数=既定の決算注文件数;
        var 低メモリか=false;
        var 開始章=0;
        for(var 位置=0;位置<引数.Count;位置++){
            switch(引数[位置]){
                case "--data":
                    データフォルダ=次の値(引数,ref 位置);
                    break;
                case "--orders":
                    var 文字列=次の値(引数,ref 位置);
                    if(!long.TryParse(文字列,NumberStyles.Integer,CultureInfo.InvariantCulture,out 件数)||件数<1){
                        throw new ArgumentException($"--orders には1以上の整数を指定してください: {文字列}");
                    }
                    break;
                case "--chapter":
                    var 章=次の値(引数,ref 位置);
                    if(!int.TryParse(章,NumberStyles.Integer,CultureInfo.InvariantCulture,out 開始章)||開始章<1){
                        throw new ArgumentException($"--chapter には1以上の章番号を指定してください: {章}");
                    }
                    開始章--;
                    break;
                case "--low-memory":
                    低メモリか=true;
                    break;
                default:
                    throw new ArgumentException($"分からない引数です: {引数[位置]}");
            }
        }
        return new 実行設定{
            データフォルダ=Path.GetFullPath(データフォルダ),
            作業フォルダ=Path.Combine(Path.GetTempPath(),"Sample.NetShop"),
            決算の注文件数=件数,
            低メモリか=低メモリか,
            開始章=開始章,
        };
    }

    private static string 次の値(IReadOnlyList<string>引数,ref int 位置){
        if(位置+1>=引数.Count){
            throw new ArgumentException($"{引数[位置]} の後に値が必要です");
        }
        位置++;
        return 引数[位置];
    }
}
