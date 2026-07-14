namespace UI;

internal static class ベンチマーク作業軸{
    public const int 標準要素数=4096;
    public const int 既定ループ回数=1;

    public static bool ループ回数を使うか(ベンチマークソースタブ ソースタブ)=>
        !スケーラブルソースタブか(ソースタブ);

    public static bool スケーラブルソースタブか(ベンチマークソースタブ ソースタブ)=>
        ソースタブ is ベンチマークソースタブ.スケーラブルセット
            or ベンチマークソースタブ.スケーラブル辞書
            or ベンチマークソースタブ.スケーラブル永続化リスト
            or ベンチマークソースタブ.スケーラブル永続化セット
            or ベンチマークソースタブ.スケーラブルTsavoriteセット
            or ベンチマークソースタブ.スケーラブル永続化辞書
            or ベンチマークソースタブ.スケーラブルTsavorite辞書;

    public static int 要素数を解決(ベンチマークソースタブ ソースタブ,int 軸数){
        if(軸数<=0){
            return 0;
        }
        if(ループ回数を使うか(ソースタブ)){
            return 標準要素数;
        }
        return 軸数;
    }

    public static int ループ回数を解決(ベンチマークソースタブ ソースタブ,int 軸数){
        if(軸数<=0){
            return 0;
        }
        if(ループ回数を使うか(ソースタブ)){
            return 軸数;
        }
        return 既定ループ回数;
    }

    public static IReadOnlyList<int>準備済み要素数を解決(ベンチマークソースタブ ソースタブ,IEnumerable<int>軸数一覧){
        if(ループ回数を使うか(ソースタブ)){
            return [標準要素数];
        }
        return 軸数一覧.Where(static 数=>数>0).Distinct().Order().ToArray();
    }

    public static int 軸数を取得(ベンチマークジョブ ジョブ){
        if(ループ回数を使うか(ジョブ.ソースタブ)&&ジョブ.ループ回数>0){
            return ジョブ.ループ回数;
        }
        return ジョブ.要素数;
    }

    public static int 軸数を取得(ベンチマーク結果項目 結果){
        if(ループ回数を使うか(結果.ソースタブ)&&結果.ループ回数>0){
            return 結果.ループ回数;
        }
        return 結果.要素数;
    }

    public static double?操作単位に正規化(ベンチマークソースタブ ソースタブ,int ループ回数,double?合計){
        if(合計 is { } 値&&ループ回数を使うか(ソースタブ)&&ループ回数>1){
            return 値/ループ回数;
        }
        return 合計;
    }

    public static long?操作単位に正規化(ベンチマークソースタブ ソースタブ,int ループ回数,long?合計){
        if(合計 is { } 値&&ループ回数を使うか(ソースタブ)&&ループ回数>1){
            return 値/ループ回数;
        }
        return 合計;
    }
}
