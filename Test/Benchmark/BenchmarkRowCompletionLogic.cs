using UI;

namespace Test.Benchmark;

public sealed class ベンチマーク行完了ロジック {
    [Fact]
    public void 仕様確認01() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",2_000_000,2_000_000),
        };

        Assert.True(global::UI.ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            4096,
            1_000_000));
    }

    [Fact]
    public void 仕様確認02() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Any.左","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Any.右","Any","System.Array.ZLinq","",4096,"Completed",200,200),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Any.左","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Any.右","System.Array.ZLinq","",true,"行-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void 仕様確認03() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","Count=1",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","Count=1",4096,"Completed",2_000_000,2_000_000),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","Count=2",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","Count=2",true,"行-1"),
            8192,
            1_000_000));
    }

    [Fact]
    public void 仕様確認04() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",4_200_000_000,4_200_000_000),
        };
        var セル一覧 = new[] {
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
        };

        Assert.True(global::UI.ベンチマーク行完了ロジック.タブ最終完了か(
            測定一覧,
            セル一覧,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void 仕様確認05() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",200,200),
        };
        var セル一覧 = new[] {
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.タブ最終完了か(
            測定一覧,
            セル一覧,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void 仕様確認06() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4096,"Completed",100,100),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4096,"Completed",4_200_000_000,4_200_000_000),
        };
        var セル一覧 = new[] {
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Where","Left","System.Array.FLinq","",true,"行-2"),
            new ベンチマークタブ進行セル("Where","Right","System.Array.ZLinq","",true,"行-2"),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.タブ最終完了か(
            測定一覧,
            セル一覧,
            8192,
            4_096_000_000));
    }

    [Fact]
    public void 仕様確認07() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",4,"Completed",25,1_100_000),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",4,"Completed",30,900_000),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",8,"Completed",30,1_200_000),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.ループ回数最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            1_000_000));
    }

    [Fact]
    public void 仕様確認08() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",8,"Completed",25,1_100_000),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",8,"Completed",30,1_200_000),
        };

        Assert.True(global::UI.ベンチマーク行完了ロジック.ループ回数最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            1_000_000));
    }

    [Fact]
    public void 仕様確認09() {
        const int ループ回数 = 4;
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","ToHashSet","System.Array.FLinq","",ループ回数,"Completed",332_000,1_328_000),
            new ベンチマーク要素測定("Right","ToHashSet","System.Array.ZLinq","",ループ回数,"Completed",114_000,456_000),
        };

        Assert.True(global::UI.ベンチマーク行完了ロジック.ループ回数最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("ToHashSet","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("ToHashSet","Right","System.Array.ZLinq","",true,"行-1"),
            1_000_000));
    }

    [Fact]
    public void 仕様確認10() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",8,"Completed",25,1_100_000),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",8,"Failed",null,1_200_000),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.ループ回数最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            1_000_000));
    }

    [Fact]
    public void 仕様確認11() {
        var 測定一覧 = new[] {
            new ベンチマーク要素測定("Left","Any","System.Array.FLinq","",8,"Completed",2_000_000),
            new ベンチマーク要素測定("Right","Any","System.Array.ZLinq","",8,"Completed",2_000_000),
        };

        Assert.False(global::UI.ベンチマーク行完了ロジック.ループ回数最終完了か(
            測定一覧,
            new ベンチマークタブ進行セル("Any","Left","System.Array.FLinq","",true,"行-1"),
            new ベンチマークタブ進行セル("Any","Right","System.Array.ZLinq","",true,"行-1"),
            1_000_000));
    }
}
