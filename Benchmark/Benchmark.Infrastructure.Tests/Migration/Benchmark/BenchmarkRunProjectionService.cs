namespace Test.Benchmark;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class ベンチマーク実行投影サービス {
    [Fact]
    public void 仕様確認01() {
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 結果一覧 = new[] {
            結果を作成("System.Array.FLinq","Aggregate",0,10),
            結果を作成("System.Array.FLinq","Aggregate",16,20),
            結果を作成("System.Array.FLinq","Aggregate",64,30),
        };

        var 表示対象 = サービス.表示結果を取得(結果一覧,16);

        Assert.Equal([0,16],表示対象.Select(static 項目 => 項目.軸数).ToArray());
    }

    [Fact]
    public void 仕様確認02() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        // 軸128が両側とも失敗しても、軸64の完了計測は残り、指定軸の行と時間シェアが構築できる。
        // 失敗は軸パージの対象外なので（パージは完了計測が入ったときだけ働く）軸64の値が生き残る。
        セッション.結果を更新または追加(結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50));
        セッション.結果を更新または追加(失敗を作成(ソースタブ.左プロジェクト,"Aggregate",128));
        セッション.結果を更新または追加(失敗を作成(ソースタブ.右プロジェクト,"Aggregate",128));

        var 行一覧 = サービス.要素数の比較行を構築(セッション.結果を取得(ソースタブ.タブ),ソースタブ,64,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal("Aggregate",行.メソッド名);
        Assert.Equal(100,行.左.時間ナノ秒);
        Assert.Equal(50,行.右.時間ナノ秒);
        Assert.Equal(2.0/3.0,行.左右シェア.時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void 仕様確認11() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        // ループ回数タブでは1ケースにつき最新計測の軸数だけが有効で、完了計測が入ると同一ケースの
        // 他軸エントリは破棄される（ベンチマーク実行セッション.結果を更新または追加）。
        // よって右が軸128で完了したあとに軸64を指定しても、右は軸64の値を持たない。
        セッション.結果を更新または追加(結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",128,25));

        var 保持結果一覧 = セッション.結果を取得(ソースタブ.タブ);
        Assert.Equal([64],保持結果一覧.Where(項目 => 項目.プロジェクト==ソースタブ.左プロジェクト).Select(static 項目 => 項目.軸数).Order().ToArray());
        Assert.Equal([128],保持結果一覧.Where(項目 => 項目.プロジェクト==ソースタブ.右プロジェクト).Select(static 項目 => 項目.軸数).Order().ToArray());

        var 行一覧 = サービス.要素数の比較行を構築(保持結果一覧,ソースタブ,64,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(100,行.左.時間ナノ秒);
        Assert.Null(行.右.時間ナノ秒);
    }

    [Fact]
    public void 仕様確認03() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        セッション.結果を更新または追加(結果を作成(ソースタブ.左プロジェクト,"Aggregate",16,100,現在日時.AddMinutes(-3)));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",16,50,現在日時.AddMinutes(-3)));
        セッション.結果を更新または追加(結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,80,現在日時.AddMinutes(-1)));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,40,現在日時));

        var 行一覧 = サービス.最新比較行を構築(セッション.結果を取得(ソースタブ.タブ),ソースタブ,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(80,行.左.時間ナノ秒);
        Assert.Equal(40,行.右.時間ナノ秒);
        Assert.Equal(2.0/3.0,行.左右シェア.時間シェア.左シェア!.Value,6);
    }

    [Fact]
    public void 仕様確認04() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var セッション = new global::UI.ベンチマーク実行セッション();
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        セッション.結果を更新または追加(結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,80,現在日時.AddMinutes(-2)));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,40,現在日時.AddMinutes(-2)));
        セッション.結果を更新または追加(結果を作成(ソースタブ.右プロジェクト,"Aggregate",128,25,現在日時));

        var 行一覧 = サービス.最新比較行を構築(セッション.結果を取得(ソースタブ.タブ),ソースタブ,行を作成);

        // FLinq（左）は要素数64で打ち切り、ZLinq（右）は要素数128まで進んだ（高速・適応では
        // プロジェクトごとに終端要素数が異なるのが通常運転）。軸が揃わなくても両側の値を独立に
        // 採用し、時間シェアが欠落しないことを確認する。
        var 行 = Assert.Single(行一覧);
        Assert.Equal(80,行.左.時間ナノ秒);
        Assert.Equal(25,行.右.時間ナノ秒);
        Assert.False(行.左右シェア.時間シェア.欠測か);
    }

    [Fact]
    public void 仕様確認05() {
        var 配列タブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var 範囲タブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System範囲);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        // 複数タブの結果を1つの結果項目一覧に統合した状態を模す。System.Arrayは大きい要素数、
        // System.Rangeは小さい要素数で完了しており、全タブ共通で選ばれる要素数はSystem.Array側の64になる。
        var 結果一覧 = new[] {
            結果を作成(配列タブ.左プロジェクト,"Aggregate",64,100,現在日時),
            結果を作成(配列タブ.右プロジェクト,"Aggregate",64,50,現在日時),
            結果を作成(範囲タブ.左プロジェクト,"Aggregate",8,30,現在日時.AddMinutes(-1)),
            結果を作成(範囲タブ.右プロジェクト,"Aggregate",8,15,現在日時.AddMinutes(-1)),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,範囲タブ,64,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(30,行.左.時間ナノ秒);
        Assert.Equal(15,行.右.時間ナノ秒);
    }

    [Fact]
    public void 仕様確認06() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 結果一覧 = new[] {
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50),
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",128,80),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",128,40),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,ソースタブ,64,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(100,行.左.時間ナノ秒);
        Assert.Equal(50,行.右.時間ナノ秒);
    }

    [Fact]
    public void 仕様確認07() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        // 適応的ループ回数を模す: Aggregate は要素数64で確定、Any は要素数8で確定。
        // 選択要素数64でも Any の行は自身の終端要素数（8）の値で表示される。
        var 結果一覧 = new[] {
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100,現在日時),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50,現在日時),
            結果を作成(ソースタブ.左プロジェクト,"Any",8,30,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.右プロジェクト,"Any",8,15,現在日時.AddMinutes(-1)),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,ソースタブ,64,行を作成);

        Assert.Equal(2,行一覧.Count);
        var 集約 = Assert.Single(行一覧,static 行 => 行.メソッド名=="Aggregate");
        Assert.Equal(100,集約.左.時間ナノ秒);
        Assert.Equal(50,集約.右.時間ナノ秒);
        var 任意 = Assert.Single(行一覧,static 行 => 行.メソッド名=="Any");
        Assert.Equal(30,任意.左.時間ナノ秒);
        Assert.Equal(15,任意.右.時間ナノ秒);
    }

    [Fact]
    public void 仕様確認08() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        // 選択要素数の計測がある行はフォールバックせず選択要素数の値を使う（より新しい別要素数の計測があっても）。
        var 結果一覧 = new[] {
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",128,80,現在日時),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",128,40,現在日時),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,ソースタブ,64,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(100,行.左.時間ナノ秒);
        Assert.Equal(50,行.右.時間ナノ秒);
    }

    [Fact]
    public void 仕様確認09() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        // 選択要素数128には左プロジェクトのみ到達。右プロジェクトは要素数64で打ち切り
        // （高速・適応ではプロジェクトごとに終端要素数が異なるのが通常運転）。右は自身の
        // 最新（要素数64）へフォールバックし、行・時間シェアが丸ごと欠落しないことを確認する。
        var 結果一覧 = new[] {
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,100,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,50,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",128,80,現在日時),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,ソースタブ,128,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(80,行.左.時間ナノ秒);
        Assert.Equal(50,行.右.時間ナノ秒);
        Assert.False(行.左右シェア.時間シェア.欠測か);
    }

    [Fact]
    public void 仕様確認10() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.System配列);
        var サービス = new global::UI.ベンチマーク実行投影サービス();
        var 現在日時 = DateTimeOffset.UtcNow;
        var 結果一覧 = new[] {
            結果を作成(ソースタブ.左プロジェクト,"Aggregate",64,80,現在日時.AddMinutes(-1)),
            結果を作成(ソースタブ.右プロジェクト,"Aggregate",64,40,現在日時),
        };

        var 行一覧 = サービス.選択要素数の表示行を構築(結果一覧,ソースタブ,null,行を作成);

        var 行 = Assert.Single(行一覧);
        Assert.Equal(80,行.左.時間ナノ秒);
        Assert.Equal(40,行.右.時間ナノ秒);
    }

    private static global::UI.報告行[] 行を作成(
        global::UI.ベンチマークソースタブ定義 ソースタブ,
        IReadOnlyList<global::UI.ベンチマーク結果項目> 結果一覧) =>
        結果一覧.Select(static 項目 => new global::UI.報告行(
            項目.プロジェクト,
            項目.メソッド,
            項目.型完全名,
            項目.メソッド,
            項目.状態,
            項目.平均,
            項目.誤差ナノ秒?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            項目.標準偏差ナノ秒?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            項目.割り当てバイト数値?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            項目.平均ナノ秒??0,
            項目.誤差ナノ秒??0,
            項目.標準偏差ナノ秒??0,
            項目.割り当てバイト数値??0,
            0,
            0,
            項目.割り当て論理CPU?.ToString(global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            string.Empty,
            string.Empty,
            項目.開始日時?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            項目.開始日時?.ToString("O",global::System.Globalization.CultureInfo.InvariantCulture)??string.Empty,
            項目.成果物フォルダー,
            項目.終了コード,
            項目.引数キー,
            項目.引数一覧)).ToArray();

    private static global::UI.ベンチマーク結果項目 結果を作成(
        string プロジェクト,
        string メソッド,
        int 要素数,
        double 平均,
        DateTimeOffset? 開始日時 = null) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列;
        var ループ回数 = global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ,要素数);
        return new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            メソッド名=メソッド,
            型完全名="Sample.Type",
            ソースタブ=ソースタブ,
            要素数=global::UI.ベンチマーク作業軸.要素数を解決(ソースタブ,要素数),
            ループ回数=ループ回数,
            状態=global::UI.ジョブ状態.完了,
            平均ナノ秒=保存合計へ変換(ソースタブ,ループ回数,平均),
            割り当てバイト数=保存合計へ変換(ソースタブ,ループ回数,128),
            開始日時=開始日時??DateTimeOffset.UtcNow,
            終了コード=0,
        });
    }

    private static global::UI.ベンチマーク結果項目 失敗を作成(string プロジェクト,string メソッド,int 要素数) {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(プロジェクト)?.タブ??global::UI.ベンチマークソースタブ.System配列;
        return new(new global::UI.ベンチマークジョブ {
            プロジェクト=プロジェクト,
            メソッド名=メソッド,
            型完全名="Sample.Type",
            ソースタブ=ソースタブ,
            要素数=global::UI.ベンチマーク作業軸.要素数を解決(ソースタブ,要素数),
            ループ回数=global::UI.ベンチマーク作業軸.ループ回数を解決(ソースタブ,要素数),
            状態=global::UI.ジョブ状態.失敗,
            開始日時=DateTimeOffset.UtcNow,
            終了コード=1,
        });
    }

    private static double 保存合計へ変換(global::UI.ベンチマークソースタブ ソースタブ,int ループ回数,double 操作単位) =>
        global::UI.ベンチマーク作業軸.ループ回数を使うか(ソースタブ)&&ループ回数>1 ? 操作単位*ループ回数 : 操作単位;

    private static long 保存合計へ変換(global::UI.ベンチマークソースタブ ソースタブ,int ループ回数,long 操作単位) =>
        global::UI.ベンチマーク作業軸.ループ回数を使うか(ソースタブ)&&ループ回数>1 ? 操作単位*ループ回数 : 操作単位;
}

