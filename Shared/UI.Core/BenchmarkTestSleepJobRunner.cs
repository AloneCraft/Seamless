using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace UI;

public sealed class ベンチマークテスト待機ジョブ実行器(TimeSpan 待機時間,bool キャンセルまで実行するか=false):Iベンチマークジョブ実行器{
    public async Task<ベンチマークジョブ実行結果>実行Async(
        ベンチマークジョブ ジョブ,
        string 実行ルート,
        int 論理CPU,
        TimeSpan タイムアウト,
        CancellationToken キャンセルトークン){
        var ベンチマーク=ジョブ.ベンチマークケースに変換();
        var ループ回数=Math.Max(1,ジョブ.ループ回数);
        var 成果物フォルダー=成果物フォルダーを作成(実行ルート,ベンチマーク,ジョブ.ソースタブ,ジョブ.要素数,ループ回数);
        var 有効待機時間=TimeSpan.FromMilliseconds(1);
        if(待機時間>TimeSpan.Zero){
            有効待機時間=待機時間;
        }
        var ストップウォッチ=Stopwatch.StartNew();
        if(キャンセルまで実行するか){
            await Task.Delay(Timeout.InfiniteTimeSpan,キャンセルトークン);
        }else{
            await Task.Delay(有効待機時間,キャンセルトークン);
        }
        ストップウォッチ.Stop();
        var 平均ナノ秒=ストップウォッチ.Elapsed.TotalMilliseconds*1_000_000;
        var ログ="テスト用ベンチマークジョブの待機時間: "+ベンチマークフォーマッター.ナノ秒を整形(平均ナノ秒)+"。";
        テストログを書き込む(成果物フォルダー,ログ);
        return new ベンチマークジョブ実行結果(
            ベンチマーク.プロジェクト,
            ベンチマーク.メソッド名,
            ベンチマーク.型完全名,
            ジョブ.要素数,
            ジョブ状態名.保存値に変換(ジョブ状態.完了),
            ベンチマークフォーマッター.ナノ秒を整形(平均ナノ秒),
            string.Empty,
            string.Empty,
            平均ナノ秒,
            null,
            null,
            0,
            0,
            成果物フォルダー,
            ログ,
            null,
            "test",
            ベンチマーク.引数キー,
            ベンチマーク.引数一覧,
            ループ回数,
            ジョブ.実行項目インデックス);
    }

    private static string 成果物フォルダーを作成(string 実行ルート,ベンチマークケース ベンチマーク,ベンチマークソースタブ ソースタブ,int 要素数,int ループ回数){
        var 引数部分="default";
        if(!string.IsNullOrWhiteSpace(ベンチマーク.引数キー)){
            引数部分=パス部分を無害化(ベンチマーク.引数キー);
        }
        var 成果物フォルダー=Path.Combine(
            実行ルート,
            ベンチマークソースタブ一覧.パス部分に変換(ソースタブ),
            "N"+要素数.ToString(CultureInfo.InvariantCulture),
            "Loop"+Math.Max(1,ループ回数).ToString(CultureInfo.InvariantCulture),
            パス部分を無害化(ベンチマーク.プロジェクト),
            パス部分を無害化(ベンチマーク.型完全名),
            パス部分を無害化(ベンチマーク.メソッド名),
            引数部分);
        Directory.CreateDirectory(成果物フォルダー);
        return 成果物フォルダー;
    }

    private static string パス部分を無害化(string 値){
        var 無効文字一覧=Path.GetInvalidFileNameChars();
        var ビルダー=new StringBuilder(値.Length);
        foreach(var 文字 in 値){
            if(無効文字一覧.Contains(文字)){
                ビルダー.Append('_');
            }else{
                ビルダー.Append(文字);
            }
        }

        if(ビルダー.Length==0){
            return "_";
        }
        return ビルダー.ToString();
    }

    private static void テストログを書き込む(string 成果物フォルダー,string 文字列){
        try{
            Directory.CreateDirectory(成果物フォルダー);
            File.WriteAllText(Path.Combine(成果物フォルダー,"test.log"),文字列,Encoding.UTF8);
        }catch(IOException 例外){
            System.Diagnostics.Debug.WriteLine("テスト用ベンチマークログの書き込みに失敗しました。"+例外);
        }catch(UnauthorizedAccessException 例外){
            System.Diagnostics.Debug.WriteLine("テスト用ベンチマークログの書き込みに失敗しました。"+例外);
        }
    }
}
