namespace Test.SourceGenerator {
    // FLinq の fallback 0 検証。
    // 1. 実行時診断: LinqCounter.EnumerableFallback が展開済みチェーン実行後に 0 のままであること。
    // 2. 生成コード検査: 生成インターセプター内に未展開の明示例外が残っていないこと。
    public sealed class FallbackDiagnostics {
        [Fact, Trait("Category","Unit")]
        public void EnumerableFallbackCounter() {
            {
                //加算と詳細集計
                var counter = new LinqCounter();
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.IncrementEnumerableFallback("Other.Method");
                Assert.Equal(3,counter.EnumerableFallback);
                Assert.Equal(2,counter.EnumerableFallbackDetails["Sample.Method"]);
            }
            {
                //Reset で全消去
                var counter = new LinqCounter();
                counter.IncrementEnumerableFallback("Sample.Method");
                counter.Reset();
                Assert.Equal(0,counter.EnumerableFallback);
                Assert.Empty(counter.EnumerableFallbackDetails);
            }
        }

        [Fact, Trait("Category","Unit")]
        public void ExpandedChains_RaiseNoRuntimeFallback() {
            using var scope = LinqCounterScope.Start();
            var sum = FallbackProbeTargets.WhereSelectSum([1,2,3,4,5]);
            var list = FallbackProbeTargets.SkipTakeToList([1,2,3,4,5]);
            Assert.Equal(2*2+4*2,sum);
            Assert.Equal([2,3],list);
            Assert.Equal(0,scope.Counter.EnumerableFallback);
        }

        [Fact, Trait("Category","Unit")]
        public void GeneratedInterceptors_ContainNoFallbackFailure() {
            var generatedRoot = FindGeneratedRoot();
            if(generatedRoot is null) {
                //生成物出力が無い環境(初回ビルド前)では検査対象なし
                return;
            }

            var failures = new List<string>();
            foreach(var file in Directory.EnumerateFiles(generatedRoot,"*.cs",SearchOption.AllDirectories)) {
                if(!file.Contains("FLinq",StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                var text = File.ReadAllText(file);
                if(!text.Contains("InterceptsLocation",StringComparison.Ordinal)) {
                    continue;
                }
                var index = 0;
                foreach(var marker in new[]{"LinqInterceptorFailure(","UnsupportedLinqExpansion("}) {
                    index=0;
                    while((index=text.IndexOf(marker,index,StringComparison.Ordinal))>=0) {
                        var lineNumber = CountLines(text,index);
                        failures.Add($"{file}:{lineNumber}:{marker}");
                        index+=1;
                    }
                }
            }

            Assert.True(failures.Count==0,
                "生成インターセプターに未展開フォールバックが残っています:\n"+string.Join("\n",failures));
        }

        private static string? FindGeneratedRoot() {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while(directory is not null) {
                var candidate = Path.Combine(directory.FullName,"Work","Generated","Test");
                if(Directory.Exists(candidate)) {
                    return candidate;
                }
                directory=directory.Parent;
            }
            return null;
        }

        private static int CountLines(string text,int position) {
            var lines = 1;
            for(var index = 0; index<position; index++) {
                if(text[index]=='\n') {
                    lines++;
                }
            }
            return lines;
        }
    }

    // fallback 検証用の展開対象。呼び出し箇所が intercept され、全段 Fragment 展開される想定のチェーン。
    [global::Seamless.FLinq.LinqIntercept(true)]
    public static class FallbackProbeTargets {
        public static int WhereSelectSum(int[] source) =>
            source.Where(static value => value%2==0).Select(static value => value*2).Sum();

        public static List<int> SkipTakeToList(int[] source) =>
            source.Skip(1).Take(2).ToList();
    }
}
