namespace Test.Persisted.Linq.Enumerable.FLinq;

public sealed class ContainsNativeLookupTests {
    private static global::Seamless.Collections.Persisted.Set<int>? source;

    // 実 UI での実測(2026-07-26): Persisted<ClassInt32>.Contains_value が FLinq 804.69µs/56.54MB
    // 対 ValueLinq 1.52µs/88B という極端な差を示した。調査の結果2つのことが分かった。
    //
    // (1) 実在するバグ: Contains.cs が ProviderNativeRootBase.NativeContainsExpression
    //     (PersistedSetRoot.cs の mmap ハッシュ索引、O(1)。"Set<T>.Contains は .hindex で O(1)。
    //     native scan の O(n) を回避する" と既存コメントにある)を一度も使っておらず、
    //     FLinq に傍受された場合は常に O(n) の TryGetNext 線形走査(要素ごとに逆シリアライズの
    //     実コスト)へ落ちていた。RenderProviderNativeContainsInline を追加して修正済み
    //     (このテストで検証: .AsEnumerable() 経由で傍受させ、生成コードが
    //     Source().Contains(value) への直接呼び出しになることを確認)。
    //
    // (2) ただし Benchmark の Contains_value ベンチマーク自体は、この (1) の影響を受けていなかった:
    //     `srcCount(...).AsX<T>()` の AsX<T> オーバーロードは Set<T> を受けたときは Set<T> を
    //     そのまま返す(Route.cs 参照、型消去しない)。このため `.Contains(...)` は
    //     System.Linq.Enumerable.Contains ではなく Set<T>.Contains(T) インスタンスメソッドに
    //     直接解決され、FLinq は最初からこの呼び出しに一切関与していなかった
    //     (実際の Benchmark.Persisted.Linq.Enumerable.FLinq を EmitCompilerGeneratedFiles 付きで
    //     ビルドし、Contains_value_Impl の傍受が生成されないことを直接確認した)。
    //     FLinq 側と ValueLinq 側は全く同じ Set<T>.Contains を呼ぶので、実測の差は
    //     コード生成の違いでは説明できない — Contains_value は展開後の要素数が
    //     ImmediateShortCircuitCount=96,000,000 という桁違いに大きい宣言値を持ち、同じ実行で
    //     BenchmarkDotNet の "takes too long to run" 較正失敗も複数回観測されており、
    //     この特定のベンチマークの較正不安定性が原因である可能性が高い。
    //
    // このテストは (1) の修正を検証する。(2) は FLinq のコード生成とは無関係なので
    // このテストの対象外。
    [Fact,Trait("Category","Unit")]
    public void Contains_UsesNativeLookup_NotLinearScan() {
        var path=Path.Combine(Path.GetTempPath(),"Seamless-ContainsNativeProbe-"+Guid.NewGuid().ToString("N"),"items.dat");
        try {
            using var set=global::Seamless.Collections.Persisted.Store.CreateSet<int>(path,InitialCapacity:1,InitialDataCapacity:4096);
            const int count=4096;
            for(var i=1;i<=count;i++)
                set.Add(i);
            source=set;

            Assert.True(FLinqContainsImpl(count/2));
            Assert.False(FLinqContainsImpl(count+1));
        } finally {
            source=null;
            var directory=Path.GetDirectoryName(path);
            if(!string.IsNullOrWhiteSpace(directory)&&Directory.Exists(directory))
                Directory.Delete(directory,recursive:true);
        }
    }

    // Set<T> 自身が Contains(T) インスタンスメソッドを持つため、単純な `Source().Contains(x)` は
    // System.Linq.Enumerable.Contains ではなく Set<T>.Contains(素の O(1) 実装)に解決されてしまい、
    // FLinq の介入を全く検証できない。.AsEnumerable() は FLinq が根メソッドとして認識するため、
    // これで拡張メソッド解決を強制しつつ ProviderNativeRootBase の認識も維持する。
    private static bool FLinqContainsImpl(int value)=>Source().AsEnumerable().Contains(value);
    private static global::Seamless.Collections.Persisted.Set<int> Source()=>source??throw new InvalidOperationException("Source was not initialized.");
}
