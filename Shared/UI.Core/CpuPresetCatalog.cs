namespace UI;

internal sealed record CPUプリセット定義(string キー,string ラベル,IReadOnlyList<int>論理プロセッサインデックス一覧);

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class CPUプリセットカタログ{
    public static IReadOnlyList<CPUプリセット定義>構築(IReadOnlyList<CPUコア情報>CPUコア一覧){
        if(CPUコア一覧.Count==0){
            return [];
        }

        var 並び順=CPUコア一覧
            .OrderBy(static CPU=>CPU.論理プロセッサインデックス)
            .ToArray();
        var プリセット一覧=new List<CPUプリセット定義>{
            プリセットを作成("ALL","すべて",並び順.Select(static CPU=>CPU.論理プロセッサインデックス)),
            プリセットを作成("CLEAR","クリア",[]),
        };

        スレッドプリセットを追加(プリセット一覧,並び順,CPUコア種別.高性能,"P");
        スレッドプリセットを追加(プリセット一覧,並び順,CPUコア種別.高効率,"E");

        return プリセット一覧;
    }

    private static void スレッドプリセットを追加(
        List<CPUプリセット定義>プリセット一覧,
        IEnumerable<CPUコア情報>CPUコア一覧,
        CPUコア種別 コア種別,
        string キー接頭辞){
        var コアグループ一覧=CPUコア一覧
            .Where(CPU=>CPU.コア種別==コア種別)
            .GroupBy(static CPU=>CPU.物理コアインデックス)
            .OrderBy(static グループ=>グループ.Key)
            .Select(static グループ=>グループ
                .OrderBy(static CPU=>CPU.論理プロセッサインデックス)
                .ToArray())
            .Where(static グループ=>グループ.Length>0)
            .ToArray();
        if(コアグループ一覧.Length==0){
            return;
        }

        var 最大スレッド数=コアグループ一覧.Max(static グループ=>グループ.Length);
        for(var スレッド数=1;スレッド数<=最大スレッド数;スレッド数++){
            var キー=$"{キー接頭辞}{スレッド数}T";
            プリセット一覧.Add(プリセットを作成(
                キー,
                キー,
                コアグループ一覧.SelectMany(グループ=>グループ
                    .Take(スレッド数)
                    .Select(static CPU=>CPU.論理プロセッサインデックス))));
        }
    }

    private static CPUプリセット定義 プリセットを作成(string キー,string ラベル,IEnumerable<int>論理プロセッサインデックス一覧){
        return new CPUプリセット定義(
            キー,
            ラベル,
            論理プロセッサインデックス一覧
                .Distinct()
                .Order()
                .ToArray());
    }
}
