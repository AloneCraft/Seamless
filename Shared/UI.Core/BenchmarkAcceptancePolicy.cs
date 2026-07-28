namespace UI;

public static class ベンチマーク受け入れ方針{
    public static bool 最適化タブか(ベンチマークソースタブ タブ)=>タブ is
        ベンチマークソースタブ.最適化書き換え or
        ベンチマークソースタブ.最適化パイプライン融合 or
        ベンチマークソースタブ.最適化述語押し下げ or
        ベンチマークソースタブ.最適化射影融合;

    public static bool 回帰か(ベンチマークソースタブ タブ,シェア値 時間シェア){
        if(時間シェア.左シェア is not { } 左シェア){
            return false;
        }
        if(最適化タブか(タブ)){
            return 左シェア>1.02/2.02;
        }
        return タブ is ベンチマークソースタブ.System配列 or ベンチマークソースタブ.SystemIEnumerable or ベンチマークソースタブ.Systemリスト or ベンチマークソースタブ.System範囲
            &&左シェア>0.95/1.95;
    }
}
