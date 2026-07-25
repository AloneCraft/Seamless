namespace Test.Benchmark;

public sealed class ベンチマークソースタブテスト {
    [Fact]
    public void 仕様確認01() {
        Assert.Equal(
            ["書き換え","パイプライン融合","述語プッシュダウン","Array","IEnumerable","Range","List","Persisted(Seamless)","Persisted(System)","Tsavorite(Seamless)","Tsavorite(System)"],
            global::UI.ベンチマークソースタブ一覧.標準全て.Select(static タブ => タブ.表示名));
        Assert.Equal(
            ["Optimization.Rewrite","Optimization.PipelineFusion","Optimization.PredicatePushdown","System.Array","System.Collections.Generic.IEnumerable<>","System.Range","System.List","Seamless.Persisted.Seamless","Seamless.Persisted.System","Seamless.Tsavorite.Seamless","Seamless.Tsavorite.System"],
            global::UI.ベンチマークソースタブ一覧.標準全て.Select(static タブ => タブ.ID));
    }

    [Fact]
    public void 仕様確認02() {
        Assert.Equal(
            ["Persisted.Set","Tsavorite.Set","Persisted.Dictionary","Tsavorite.Dictionary","Persisted.List"],
            global::UI.ベンチマークソースタブ一覧.スケーラブル全て.Select(static タブ => タブ.表示名));
        Assert.Equal(
            ["Scalable.Persisted.Set","Scalable.Tsavorite.Set","Scalable.Persisted.Dictionary","Scalable.Tsavorite.Dictionary","Scalable.Persisted.List"],
            global::UI.ベンチマークソースタブ一覧.スケーラブル全て.Select(static タブ => タブ.ID));
    }

    [Fact]
    public void 仕様確認03() {
        var パス部分 = global::UI.ベンチマークソースタブ一覧.パス部分に変換(global::UI.ベンチマークソースタブ.SystemIEnumerable);

        Assert.Equal("System.Collections.Generic.IEnumerable__",パス部分);
        Assert.DoesNotContain(パス部分,static 文字 => 文字 is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*');
    }

    [Fact]
    public void 仕様確認04() {
        foreach(var タブ in global::UI.ベンチマークソースタブ一覧.標準全て.Concat(global::UI.ベンチマークソースタブ一覧.スケーラブル全て)) {
            Assert.False(string.IsNullOrWhiteSpace(global::UI.ベンチマークソースタブ一覧.パス部分に変換(タブ.タブ)));
        }
    }

    [Fact]
    public void 仕様確認05() {
        var 永続化 = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.Seamless永続化);
        var Tsavorite = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.SeamlessTsavorite);

        Assert.Equal(永続化,global::UI.ベンチマークソースタブ一覧.プロジェクトから取得を試みる(永続化.右プロジェクト));
        Assert.Equal(永続化,global::UI.ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(永続化.右プロジェクト,永続化.右アセンブリ名));
        Assert.Equal(Tsavorite,global::UI.ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(Tsavorite.右プロジェクト,Tsavorite.右アセンブリ名));
        Assert.Null(global::UI.ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得を試みる(永続化.右プロジェクト,"Wrong.Assembly"));
        Assert.Throws<InvalidDataException>(() => global::UI.ベンチマークソースタブ一覧.プロジェクトとアセンブリから取得(永続化.右プロジェクト,"Wrong.Assembly"));
    }

    [Fact]
    public void 仕様確認06() {
        var ソースタブ = global::UI.ベンチマークソースタブ一覧.取得(global::UI.ベンチマークソースタブ.最適化書き換え);

        Assert.True(ソースタブ.ケースを含むか(new(
            ソースタブ.左プロジェクト,
            ソースタブ.左アセンブリ名,
            ソースタブ.左型接頭辞+"ClassInt32",
            "Aggregate")));
        Assert.False(ソースタブ.ケースを含むか(new(
            ソースタブ.左プロジェクト,
            "Wrong.Assembly",
            ソースタブ.左型接頭辞+"ClassInt32",
            "Aggregate")));
        Assert.False(ソースタブ.ケースを含むか(new(
            ソースタブ.左プロジェクト,
            ソースタブ.左アセンブリ名,
            "Wrong.Type",
            "Aggregate")));
    }
}
