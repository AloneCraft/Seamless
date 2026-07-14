using Seamless.FLinq;

using System.Text.RegularExpressions;

namespace Test.System.Linq.Intercept;

internal static class GeneratedInterceptorAssert {
    internal static string SourceFor<TMarker>(string methodName) {
        // メソッド名はクラス名プレフィックスを外したため複数ファイルで衝突する。
        // マーカークラス TMarker に対応する生成ファイルだけへ絞り込んでから判定する。
        // Chain1 と FragmentCoverage には同名クラス（例 ElementAt）があるため、
        // 生成名前空間（`namespace Intercept.{TMarker の名前空間}`）とラッパー
        // `class {Name}` 宣言の両方で一意に特定する。素の型名だと `System.Index`
        // 等に誤マッチするため `class {Name}` 宣言で判定する。
        var markerName = typeof(TMarker).Name;
        var markerNamespace = typeof(TMarker).Namespace??string.Empty;
        var matches = new List<(string Text,DateTime LastWriteTimeUtc)>();
        var existingRoots = GeneratedSourceRoots<TMarker>().Where(Directory.Exists).ToArray();
        Assert.NotEmpty(existingRoots);
        foreach(var generatedRoot in existingRoots) {
            foreach(var file in Directory.EnumerateFiles(generatedRoot,"*.g.cs",SearchOption.AllDirectories)) {
                var text = File.ReadAllText(file);
                if(!text.Contains($"namespace Intercept.{markerNamespace}",StringComparison.Ordinal)) {
                    continue;
                }
                if(!Regex.IsMatch(text,$@"(?<![A-Za-z0-9_])class {Regex.Escape(markerName)}(?![A-Za-z0-9_])")) {
                    continue;
                }
                if(!text.Contains($"<c>{methodName}()</c>",StringComparison.Ordinal)) {
                    continue;
                }
                matches.Add((text,File.GetLastWriteTimeUtc(file)));
            }
        }

        if(matches.Count==0) {
            Assert.Fail("Generated source file does not exist for "+typeof(TMarker).FullName+"."+methodName);
        }

        return matches
            .OrderByDescending(static match => match.LastWriteTimeUtc)
            .Select(static match => match.Text)
            .First();
    }

    internal static void ContainsMethod<TMarker>(string methodName) {
        var generated = SourceFor<TMarker>(methodName);
        Assert.Contains(methodName,generated);
    }

    private static string[] GeneratedSourceRoots<TMarker>() {
        var assemblyDirectory = Path.GetDirectoryName(typeof(TMarker).Assembly.Location)
            ??throw new InvalidOperationException("Could not resolve test assembly directory.");
        var targetFrameworkDirectory = new DirectoryInfo(assemblyDirectory);
        var configurationDirectory = targetFrameworkDirectory.Parent
            ??throw new InvalidOperationException("Could not resolve configuration directory.");
        var projectDirectory = configurationDirectory.Parent?.Parent
            ??throw new InvalidOperationException("Could not resolve project directory.");

        var generatorAssemblyName = typeof(FLinqIncrementalGenerator).Assembly.GetName().Name
            ??throw new InvalidOperationException("Could not resolve generator assembly name.");
        var generatorTypeName = typeof(FLinqIncrementalGenerator).FullName
            ??throw new InvalidOperationException("Could not resolve generator type name.");

        return [
            Path.Combine(
                projectDirectory.FullName,
                "obj",
                configurationDirectory.Name,
                targetFrameworkDirectory.Name,
                "generated",
                generatorAssemblyName,
                generatorTypeName),
            Path.Combine(
                projectDirectory.Parent?.FullName
                    ?? throw new InvalidOperationException("Could not resolve solution directory."),
                "Work",
                "Generated",
                typeof(TMarker).Assembly.GetName().Name ?? "Test",
                configurationDirectory.Name,
                targetFrameworkDirectory.Name,
                generatorAssemblyName,
                generatorTypeName),
        ];
    }
}
