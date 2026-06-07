using System.Text.RegularExpressions;

namespace Test.Seamless.Intercept.System.Linq;

internal static class GeneratedInterceptorAssert {
    internal static string SourceFor<TMarker>(string methodName) {
        var generatedRoot = GeneratedSourceRoot<TMarker>();
        Assert.True(Directory.Exists(generatedRoot),$"Generated source directory does not exist: {generatedRoot}");

        // メソッド名はクラス名プレフィックスを外したため複数ファイルで衝突する。
        // マーカークラス TMarker に対応する生成ファイルだけへ絞り込んでから判定する。
        // Chain1 と FragmentCoverage には同名クラス（例 ElementAt）があるため、
        // 生成名前空間（`namespace Intercept.{TMarker の名前空間}`）とラッパー
        // `class {Name}` 宣言の両方で一意に特定する。素の型名だと `System.Index`
        // 等に誤マッチするため `class {Name}` 宣言で判定する。
        var markerName = typeof(TMarker).Name;
        var markerNamespace = typeof(TMarker).Namespace ?? string.Empty;
        var matches = new List<string>();
        foreach(var file in Directory.EnumerateFiles(generatedRoot,"*.g.cs",SearchOption.AllDirectories)) {
            var text = File.ReadAllText(file);
            if(!text.Contains($"namespace Intercept.{markerNamespace}",StringComparison.Ordinal)) {
                continue;
            }
            if(!Regex.IsMatch(text,$@"(?<![A-Za-z0-9_])class {Regex.Escape(markerName)}(?![A-Za-z0-9_])")) {
                continue;
            }
            if(!Regex.IsMatch(text,$@"(?<![A-Za-z0-9_]){Regex.Escape(methodName)}")) {
                continue;
            }
            matches.Add(text);
        }

        Assert.Single(matches);
        return matches[0];
    }

    internal static void ContainsMethod<TMarker>(string methodName) {
        var generated = SourceFor<TMarker>(methodName);
        Assert.Contains(methodName,generated);
    }

    private static string GeneratedSourceRoot<TMarker>() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(TMarker).Assembly.Location)
            ?? throw new InvalidOperationException("Could not resolve test assembly directory.");
        var targetFrameworkDirectory = new DirectoryInfo(assemblyDirectory);
        var configurationDirectory = targetFrameworkDirectory.Parent
            ?? throw new InvalidOperationException("Could not resolve configuration directory.");
        var projectDirectory = configurationDirectory.Parent?.Parent
            ?? throw new InvalidOperationException("Could not resolve project directory.");

        return Path.Combine(
            projectDirectory.FullName,
            "obj",
            configurationDirectory.Name,
            targetFrameworkDirectory.Name,
            "generated");
    }
}
