using System.Globalization;

namespace UI;

[global::Seamless.FLinq.LinqIntercept(false)]
internal static class ベンチマーク比較フォーマッター{
    public static readonly string[] プロジェクト順=
        ベンチマークソースタブ一覧.全て
            .SelectMany(static タブ=>タブ.プロジェクト順)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<ベンチマーク比較行>比較行を構築(IReadOnlyList<報告行>行一覧){
        var タブ=行一覧
            .Select(行=>ベンチマークソースタブ一覧.プロジェクトから取得を試みる(行.プロジェクト))
            .FirstOrDefault(static 項目=>項目 is not null)
            ??ベンチマークソースタブ一覧.既定;
        return 比較行を構築(行一覧,タブ);
    }

    public static IReadOnlyList<ベンチマーク比較行>比較行を構築(
        IReadOnlyList<報告行>行一覧,
        ベンチマークソースタブ定義 ソースタブ,
        int 要素数=0){
        var メソッド別行=new Dictionary<string,ベンチマーク比較バケット>(StringComparer.OrdinalIgnoreCase);
        var 重複プロジェクトメソッド=行一覧
            .GroupBy(static 行=>行.プロジェクト+"|"+行.メソッド,StringComparer.OrdinalIgnoreCase)
            .Where(static グループ=>グループ.Select(static 行=>行.型完全名).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1)
            .Select(static グループ=>グループ.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var 行 in 行一覧){
            if(!ソースタブ.プロジェクトを含むか(行.プロジェクト)){
                throw new InvalidDataException($"{ソースタブ.ID} に未知のベンチマークプロジェクトがあります: {行.プロジェクト}");
            }

            var 型識別子=型識別子を作成(ソースタブ,行,重複プロジェクトメソッド);
            var キー=比較キーを作成(行.メソッド,型識別子,行.引数キー);
            if(!メソッド別行.TryGetValue(キー,out var バケット)){
                バケット=new ベンチマーク比較バケット(行.メソッド,型識別子,行.型完全名,行.引数キー,行.引数一覧);
                メソッド別行.Add(キー,バケット);
            }

            バケット.プロジェクト別測定値[プロジェクト名を正規化(行.プロジェクト,ソースタブ)]=測定値を作成(行);
        }

        return メソッド別行
            .OrderBy(static 項目=>項目.Value.メソッド,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static 項目=>項目.Value.型完全名,StringComparer.OrdinalIgnoreCase)
            .ThenBy(static 項目=>項目.Value.引数キー,StringComparer.OrdinalIgnoreCase)
            .Select(項目=>比較行を作成(
                ベンチマーク名を作成(項目.Value,ソースタブ,要素数),
                項目.Value.メソッド,
                項目.Value.型完全名,
                項目.Value.プロジェクト別測定値,
                ソースタブ,
                項目.Value.引数キー,
                項目.Value.引数一覧))
            .ToArray();
    }

    public static シェア値 時間シェアを計算(プロジェクト測定値 左,プロジェクト測定値 右){
        if(!左.測定値ありか||!右.測定値ありか||左.時間ナノ秒 is not { } 左値||右.時間ナノ秒 is not { } 右値){
            return シェア値.欠測;
        }

        return シェア値.時間から作成(左値,右値);
    }

    public static シェア値 メモリシェアを計算(プロジェクト測定値 左,プロジェクト測定値 右){
        if(!左.測定値ありか||!右.測定値ありか||左.割り当てバイト数 is not { } 左値||右.割り当てバイト数 is not { } 右値){
            return シェア値.欠測;
        }
        return シェア値.メモリから作成(左値,右値);
    }

    public static シェア比較 シェア比較を作成(ベンチマーク比較行 行,比較ペア定義 ペア){
        var 左=行.プロジェクトを取得(ペア.左プロジェクト);
        var 右=行.プロジェクトを取得(ペア.右プロジェクト);
        return new シェア比較(
            ペア,
            左,
            右,
            時間シェアを計算(左,右),
            メモリシェアを計算(左,右));
    }

    private static ベンチマーク比較行 比較行を作成(
        string ベンチマーク,
        string メソッド,
        string 型完全名,
        IReadOnlyDictionary<string,プロジェクト測定値>測定値一覧,
        ベンチマークソースタブ定義 ソースタブ,
        string 引数キー="",
        IReadOnlyList<ベンチマーク引数>?引数一覧=null){
        var 左=測定値一覧.GetValueOrDefault(ソースタブ.左プロジェクト);
        var 右=測定値一覧.GetValueOrDefault(ソースタブ.右プロジェクト);
        var 行=new ベンチマーク比較行(
            ベンチマーク,
            ソースタブ,
            左,
            右);
        return 行 with{
            メソッド名=メソッド,
            型完全名=型完全名,
            引数キー=引数キー,
            引数一覧=引数一覧??[],
            左右シェア=シェア比較を作成(行,new 比較ペア定義(
                ソースタブ.ID,
                ソースタブ.表示名,
                ソースタブ.左プロジェクト+"/"+ソースタブ.右プロジェクト,
                ソースタブ.左プロジェクト,
                ソースタブ.右プロジェクト)),
        };
    }

    private static string ベンチマーク名を作成(
        ベンチマーク比較バケット バケット,
        ベンチマークソースタブ定義 ソースタブ,
        int 要素数)=>
        ベンチマーク名を作成する本体(バケット,ソースタブ);

    private static string ベンチマーク名を作成する本体(
        ベンチマーク比較バケット バケット,
        ベンチマークソースタブ定義 ソースタブ){
        if(!スケーラブルベンチマーク名フォーマッター.型付きベンチマーク名を使うか(ソースタブ.タブ)){
            return バケット.メソッド;
        }
        if(!スケーラブルベンチマーク名フォーマッター.スケーラブルソースタブか(ソースタブ.タブ)
            &&!string.IsNullOrWhiteSpace(バケット.型完全名)){
            return "<"+バケット.型完全名+">."+バケット.メソッド;
        }
        return スケーラブルベンチマーク名フォーマッター.ベンチマーク名を作成(バケット.元の型完全名,バケット.メソッド);
    }

    private static string 型識別子を作成(
        ベンチマークソースタブ定義 ソースタブ,
        報告行 行,
        System.Collections.Generic.ISet<string>重複プロジェクトメソッド){
        if(スケーラブルベンチマーク名フォーマッター.型付きベンチマーク名を使うか(ソースタブ.タブ)){
            if(スケーラブルベンチマーク名フォーマッター.スケーラブルソースタブか(ソースタブ.タブ)){
                return スケーラブルベンチマーク名フォーマッター.型短名(行.型完全名);
            }
            var メソッド1型=スケーラブルベンチマーク名フォーマッター.従来メソッド1比較型表示を作成(行.型完全名);
            if(!string.IsNullOrWhiteSpace(メソッド1型)){
                return メソッド1型;
            }
            if(重複プロジェクトメソッド.Contains(行.プロジェクト+"|"+行.メソッド)){
                return スケーラブルベンチマーク名フォーマッター.型短名(行.型完全名);
            }
            return string.Empty;
        }
        if(重複プロジェクトメソッド.Contains(行.プロジェクト+"|"+行.メソッド)){
            return スケーラブルベンチマーク名フォーマッター.型短名(行.型完全名);
        }
        return string.Empty;
    }

    private static string 比較キーを作成(string メソッド,string 型完全名,string 引数キー)=>
        メソッド+"|"+型完全名+"|"+引数キー;

    private sealed class ベンチマーク比較バケット{
        public ベンチマーク比較バケット(
            string メソッド,
            string 型完全名,
            string 元の型完全名,
            string 引数キー,
            IReadOnlyList<ベンチマーク引数>引数一覧){
            this.メソッド=メソッド;
            this.型完全名=型完全名;
            this.元の型完全名=元の型完全名;
            this.引数キー=引数キー;
            this.引数一覧=引数一覧;
        }

        public string メソッド{get;}
        public string 型完全名{get;}
        public string 元の型完全名{get;}
        public string 引数キー{get;}
        public IReadOnlyList<ベンチマーク引数>引数一覧{get;}
        public Dictionary<string,プロジェクト測定値>プロジェクト別測定値{get;}=new(StringComparer.OrdinalIgnoreCase);
    }

    private static プロジェクト測定値 測定値を作成(報告行 行){
        if(!string.Equals(行.状態,ジョブ状態名.保存値に変換(ジョブ状態.完了),StringComparison.OrdinalIgnoreCase)){
            return プロジェクト測定値.欠測;
        }

        double?時間=null;
        if(行.平均ナノ秒>0){
            時間=行.平均ナノ秒;
        }
        long?割り当て=null;
        if(!string.IsNullOrWhiteSpace(行.割り当てバイト数文字列)){
            割り当て=行.割り当てバイト数値;
        }
        return プロジェクト測定値.測定から作成(時間,割り当て);
    }

    private static string プロジェクト名を正規化(string プロジェクト,ベンチマークソースタブ定義 ソースタブ){
        if(string.Equals(プロジェクト,ソースタブ.左プロジェクト,StringComparison.OrdinalIgnoreCase)){
            return ソースタブ.左プロジェクト;
        }

        if(string.Equals(プロジェクト,ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase)){
            return ソースタブ.右プロジェクト;
        }

        return プロジェクト;
    }
}

public readonly record struct プロジェクト測定値(double?時間ナノ秒,long?割り当てバイト数,bool 値ありか){
    public static readonly プロジェクト測定値 欠測=new(null,null,false);

    public bool 測定値ありか=>this.値ありか&&this.時間ナノ秒 is not null;

    public static プロジェクト測定値 測定から作成(double?時間ナノ秒,long?割り当てバイト数){
        if(時間ナノ秒 is null){
            return 欠測;
        }
        return new プロジェクト測定値(時間ナノ秒,割り当てバイト数,true);
    }
}

public readonly record struct シェア値(
    double?左シェア,
    double?右シェア,
    string 表示文字列,
    シェア判定 判定){
    public static readonly シェア値 欠測=new(null,null,"-",シェア判定.なし);

    public bool 欠測か=>this.左シェア is null||this.右シェア is null;

    public static シェア値 時間から作成(double 左値,double 右値)=>
        作成(左値,右値,true);

    public static シェア値 メモリから作成(long 左値,long 右値)=>
        作成(左値,右値,false);

    public static シェア値 左シェアから作成(double 左シェア){
        if(!double.IsFinite(左シェア)||左シェア<0||左シェア>1){
            return 欠測;
        }

        return シェアから作成(左シェア,1-左シェア,厳密判定(左シェア));
    }

    private static シェア値 作成(double 左値,double 右値,bool 時間判定か){
        if(!double.IsFinite(左値)||!double.IsFinite(右値)||左値<0||右値<0){
            return 欠測;
        }

        var 合計=左値+右値;
        if(!double.IsFinite(合計)){
            return 欠測;
        }
        if(合計==0){
            return new シェア値(0,0,"0.00:0.00",シェア判定.同等);
        }

        var 左シェア=左値/合計;
        var 右シェア=右値/合計;
        シェア判定 判定;
        if(時間判定か){
            判定=時間判定を取得(左値,右値);
        }else{
            判定=厳密判定(左シェア);
        }
        return シェアから作成(左シェア,右シェア,判定);
    }

    private static シェア値 シェアから作成(double 左シェア,double 右シェア,シェア判定 判定){
        var 表示左=Math.Round(左シェア,2,MidpointRounding.AwayFromZero);
        double 表示右;
        if(左シェア+右シェア==0){
            表示右=0;
        }else{
            表示右=1-表示左;
        }
        return new シェア値(
            左シェア,
            右シェア,
            表示左.ToString("0.00",CultureInfo.InvariantCulture)+":"+表示右.ToString("0.00",CultureInfo.InvariantCulture),
            判定);
    }

    private static シェア判定 時間判定を取得(double 左値,double 右値){
        if(左値==0&&右値==0){
            return シェア判定.同等;
        }
        if(右値==0){
            return シェア判定.右優位;
        }

        var 比=左値/右値;
        if(比<0.95){
            return シェア判定.左優位;
        }
        if(比>1.05){
            return シェア判定.右優位;
        }
        return シェア判定.同等;
    }

    private static シェア判定 厳密判定(double 左シェア){
        if(左シェア<0.5){
            return シェア判定.左優位;
        }
        if(左シェア>0.5){
            return シェア判定.右優位;
        }
        return シェア判定.同等;
    }
}

public readonly record struct ベンチマークシェアゲージ状態(double 左割合,double 右割合,bool 色付きバーがあるか){
    public static ベンチマークシェアゲージ状態 シェアから作成(シェア値 シェア){
        if(シェア.欠測か||シェア.左シェア is not { } 左||シェア.右シェア is not { } 右||左+右<=0){
            return new(0,0,false);
        }
        return new(Math.Clamp(左,0,1),Math.Clamp(右,0,1),true);
    }
}

public readonly record struct シェア比較(
    比較ペア定義 定義,
    プロジェクト測定値 左,
    プロジェクト測定値 右,
    シェア値 時間シェア,
    シェア値 メモリシェア);

public readonly record struct ベンチマーク比較行(
    string ベンチマーク,
    ベンチマークソースタブ定義 ソースタブ,
    プロジェクト測定値 左,
    プロジェクト測定値 右){
    public string メソッド名{get;init;}=ベンチマーク;
    public string 型完全名{get;init;}=string.Empty;
    public string 引数キー{get;init;}=string.Empty;
    public IReadOnlyList<ベンチマーク引数>引数一覧{get;init;}=[];
    public シェア比較 左右シェア{get;init;}

    public プロジェクト測定値 プロジェクトを取得(string プロジェクト){
        if(this.ソースタブ is null){
            return プロジェクト測定値.欠測;
        }

        if(string.Equals(プロジェクト,this.ソースタブ.左プロジェクト,StringComparison.OrdinalIgnoreCase)){
            return this.左;
        }

        if(string.Equals(プロジェクト,this.ソースタブ.右プロジェクト,StringComparison.OrdinalIgnoreCase)){
            return this.右;
        }

        return プロジェクト測定値.欠測;
    }
}

public readonly record struct 比較ペア定義(
    string キー,
    string 表示名,
    string ツールチップ文字列,
    string 左プロジェクト,
    string 右プロジェクト);

internal static class ベンチマーク比較ペア{
    public static IReadOnlyList<string>?ペアプロジェクトを取得(string プロジェクト)=>
        ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.プロジェクト順;
}

public enum シェア判定{
    なし,
    左優位,
    右優位,
    同等,
}
