using UI;

namespace Test.BenchmarkWorker;

public sealed class ベンチマーク状態画像ロジック {
    [Fact]
    public void 仕様確認01() {
        var 種別 = global::UI.ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            ケースありか: true,
            実行中か: false,
            失敗か: false,
            測定値ありか: true,
            測定なし完了か: false,
            実行予定か: true,
            行最終完了か: false));

        Assert.Equal(ベンチマーク状態画像種別.完了,種別);
        Assert.Equal(ベンチマーク状態画像名.完了,ベンチマーク状態画像名.種別から取得(種別));
    }

    [Fact]
    public void 仕様確認02() {
        var 種別 = global::UI.ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            ケースありか: true,
            実行中か: false,
            失敗か: false,
            測定値ありか: true,
            測定なし完了か: false,
            実行予定か: true,
            行最終完了か: true));

        Assert.Equal(ベンチマーク状態画像種別.最終完了,種別);
        Assert.Equal(ベンチマーク状態画像名.最終完了,ベンチマーク状態画像名.種別から取得(種別));
    }

    [Fact]
    public void 仕様確認03() {
        var 種別 = global::UI.ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            ケースありか: true,
            実行中か: false,
            失敗か: true,
            測定値ありか: true,
            測定なし完了か: false,
            実行予定か: true,
            行最終完了か: true));

        Assert.Equal(ベンチマーク状態画像種別.失敗,種別);
    }

    [Fact]
    public void 仕様確認04() {
        var 種別 = global::UI.ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            ケースありか: true,
            実行中か: true,
            失敗か: false,
            測定値ありか: false,
            測定なし完了か: true,
            実行予定か: true,
            行最終完了か: false));

        Assert.Equal(ベンチマーク状態画像種別.実行中,種別);
        Assert.Equal(ベンチマーク状態画像名.CPU実行中,ベンチマーク状態画像名.種別から取得(種別));
    }

    [Fact]
    public void 仕様確認05() {
        var 種別 = global::UI.ベンチマーク状態画像ロジック.プロジェクト画像種別を取得(new ベンチマーク状態画像入力(
            ケースありか: true,
            実行中か: true,
            失敗か: false,
            測定値ありか: true,
            測定なし完了か: false,
            実行予定か: true,
            行最終完了か: true));

        Assert.Equal(ベンチマーク状態画像種別.実行中,種別);
        Assert.Equal(ベンチマーク状態画像名.CPU実行中,ベンチマーク状態画像名.種別から取得(種別));
    }

    [Fact]
    public void 仕様確認06() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Aggregate","","",4096,"Completed",900_000,900_000),
            new ベンチマーク要素測定("Right","Aggregate","","",4096,"Completed",1_500_000,1_500_000),
        };

        Assert.True(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            "Aggregate",
            "Left",
            "Right",
            8192,
            1_000_000));

        Assert.False(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            "Aggregate",
            "Left",
            "Right",
            8192,
            2_000_000));

        測定一覧= [
            new ベンチマーク要素測定("Left","Aggregate","","",8192,"Completed",900_000,900_000),
            new ベンチマーク要素測定("Right","Aggregate","","",8192,"Completed",950_000,950_000),
        ];

        Assert.True(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            "Aggregate",
            "Left",
            "Right",
            8192,
            2_000_000));
    }

    [Fact]
    public void 仕様確認07() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
        };

        Assert.True(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            4096,
            1_000_000));

        Assert.False(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","Count=2",true,"行-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void 仕様確認08() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",200,200),
        };

        Assert.False(ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void 仕様確認09() {
        Assert.Equal(ベンチマーク状態画像名.CPU待機中,ベンチマークCPU状態画像ロジック.画像ソースを取得("Pending"));
        Assert.Equal(ベンチマーク状態画像名.CPU実行中,ベンチマークCPU状態画像ロジック.画像ソースを取得(ジョブ状態名.保存値に変換(ジョブ状態.実行中)));
        Assert.Equal(ベンチマーク状態画像名.完了,ベンチマークCPU状態画像ロジック.画像ソースを取得("Completed"));
        Assert.Equal(ベンチマーク状態画像名.失敗,ベンチマークCPU状態画像ロジック.画像ソースを取得("Failed"));
        Assert.Equal(ベンチマークCPUコア画像名.高性能,ベンチマークCPUコア画像名.コア種別から取得(CPUコア種別.高性能));
        Assert.Equal(ベンチマークCPUコア画像名.高効率,ベンチマークCPUコア画像名.コア種別から取得(CPUコア種別.高効率));
        Assert.EndsWith(".png",ベンチマーク状態画像名.待機中,StringComparison.Ordinal);
        Assert.EndsWith(".png",ベンチマークCPUコア画像名.高性能,StringComparison.Ordinal);
    }

    [Fact]
    public void 仕様確認10() {
        var ツールチップ = ベンチマーク失敗表示ロジック.ツールチップ理由を作成(
            "\r\nSystem.MissingMethodException: Method 'CaptureBenchmarkMethodResult' not found.\r\n   at Sample.Type.メソッド()");

        Assert.Equal("\u7406\u7531: System.MissingMethodException: Method 'CaptureBenchmarkMethodResult' not found.",ツールチップ);
    }

    [Fact]
    public void 仕様確認11() {
        var ツールチップ = ベンチマーク失敗表示ロジック.ツールチップ理由を作成(" \r\n ");

        Assert.Equal("\u7406\u7531: "+ベンチマーク失敗表示ロジック.既定失敗理由,ツールチップ);
    }

    [Fact]
    public void 仕様確認12() {
        var 理由 = "System.InvalidOperationException: boom\r\n   at Sample.Type.メソッド()";
        var ログ = ベンチマーク失敗表示ロジック.ログ文を作成(
            "Scalable.Tsavorite.Dictionary",
            "Add",
            65536,
            0,
            "artifact-folder",
            理由,
            "Benchmark.Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32");

        Assert.Contains("ベンチマークに失敗しました。",ログ,StringComparison.Ordinal);
        Assert.Contains("プロジェクト=Scalable.Tsavorite.Dictionary",ログ,StringComparison.Ordinal);
        Assert.Contains("型=Benchmark.Scalable.Persisted.Collections.Dictionary.DictionaryStringInt32",ログ,StringComparison.Ordinal);
        Assert.Contains("要素数=65536",ログ,StringComparison.Ordinal);
        Assert.Contains("CPU=0",ログ,StringComparison.Ordinal);
        Assert.Contains("成果物=artifact-folder",ログ,StringComparison.Ordinal);
        Assert.Contains(理由,ログ,StringComparison.Ordinal);
    }

    [Fact]
    public void 仕様確認13() {
        Assert.Equal(string.Empty,ベンチマークCPU状態画像ロジック.セル状態文を取得(ジョブ状態名.保存値に変換(ジョブ状態.保留),string.Empty));
        Assert.Equal("Count",ベンチマークCPU状態画像ロジック.セル状態文を取得("Running","Count"));
        Assert.Equal(ベンチマーク状態画像名.CPU待機中,ベンチマークCPU状態画像ロジック.画像ソースを取得(ジョブ状態名.保存値に変換(ジョブ状態.保留)));
    }

}
