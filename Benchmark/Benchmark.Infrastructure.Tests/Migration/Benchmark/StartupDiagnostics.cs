#if NET10_0_OR_GREATER && WINDOWS
namespace Test.Benchmark;

public sealed class 起動診断 {
    [Fact]
    public void パンくず循環バッファは直近の記録を保持する() {
        var 接頭辞 = "パンくず循環-"+Guid.NewGuid().ToString("N",global::System.Globalization.CultureInfo.InvariantCulture)+"-";
        for(var インデックス = 0;インデックス<130;インデックス++) {
            global::UI.起動診断.パンくずを追加(接頭辞+インデックス.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        }

        var 文字列 = global::UI.起動診断.クラッシュ状況文字列を取得("テスト");

        Assert.Contains(接頭辞+"129",文字列);
        Assert.DoesNotContain(接頭辞+"0",文字列);
    }

    [Fact]
    public void UI処理はクラッシュ状況を記録して例外を再送出する() {
        var 状況 = "UI処理-"+Guid.NewGuid().ToString("N",global::System.Globalization.CultureInfo.InvariantCulture);

        Assert.Throws<InvalidOperationException>(() => global::UI.起動診断.UI処理を実行(
            状況,
            static () => throw new InvalidOperationException("テスト例外")));

        var 文字列 = global::UI.起動診断.クラッシュ状況文字列を取得("テスト");
        Assert.Contains("UIディスパッチ開始: "+状況,文字列);
    }

    [Fact]
    public void 書き込みはUIディスパッチ予約失敗時に購読通知へ再入しない() {
        var 印 = "ディスパッチ再入-"+Guid.NewGuid().ToString("N",global::System.Globalization.CultureInfo.InvariantCulture);
        var 受信回数 = 0;
        using var 購読 = global::UI.起動診断.購読(文字列 => {
            if(!文字列.Contains(印,StringComparison.Ordinal)) {
                return;
            }

            受信回数++;
            global::UI.起動診断.メインスレッドで呼び出し開始(印,static () => {
            });
        });

        global::UI.起動診断.書き込む(印);

        Assert.Equal(1,受信回数);
    }
}
#endif
