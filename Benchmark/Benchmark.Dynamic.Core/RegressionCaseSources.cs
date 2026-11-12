namespace Benchmark.Dynamic.Core;

public sealed record RegressionSourceDocument(string Path,string Text);
public sealed record RegressionSourceBundle(string ID,IReadOnlyList<RegressionSourceDocument> Documents);
public sealed record RegressionCaseDescriptor(string ID,string TypeName,string MethodName,int Row,string? Skip){
    public IReadOnlyList<string> SourcePaths{get;init;}=[];
    public string Arguments{get;init;}=string.Empty;
    public IReadOnlyList<string> Comparisons{get;init;}=[];
    public string DisplayName{
        get{
            if(this.Arguments.Length==0){return this.ID;}
            return this.ID+" ("+this.Arguments+")";
        }
    }
}
public sealed record RegressionCaseResult(string ID,string Status,string Output,string? Error,long FallbackCount);

/// <summary>GUIと自動検証が共有する、独自型・fixtureを含む回帰ソース一式。</summary>
public static class RegressionCaseSources{
    public static RegressionSourceBundle Legacy{get;}=Load();
    public static IReadOnlyList<string> Paths=>Legacy.Documents.Select(item=>item.Path).ToArray();
    private static RegressionSourceBundle Load(){
        var assembly=typeof(RegressionCaseSources).Assembly;
        var documents=new List<RegressionSourceDocument>();
        foreach(var name in assembly.GetManifestResourceNames().Where(name=>name.StartsWith("RegressionSources/",StringComparison.Ordinal)).Order()){
            using var reader=new StreamReader(assembly.GetManifestResourceStream(name)!);
            documents.Add(new(name["RegressionSources/".Length..].Replace('\\','/'),reader.ReadToEnd()));
        }
        if(documents.Count==0){throw new InvalidDataException("回帰ソースが組み込まれていません。");}
        return new("legacy-behavior-v1",documents);
    }
}
