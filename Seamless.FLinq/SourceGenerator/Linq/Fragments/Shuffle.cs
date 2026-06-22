namespace Seamless.FLinq.Linq.Fragments;

public class Shuffle(int 番号, Enumerable source) : BufferedSequence(番号, source.ElementType) {
    protected override Enumerable PrimarySource => source;
    protected override string StructSources => source.GetEnumeratorStruct;
    public override string StructMethod => string.Empty;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({source.GetEnumeratorExpression})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum source";
    protected override string BuildExpression => "Build(source)";
    protected override string BuildMembers => $$"""
        private static global::System.Collections.Generic.List<{{source.ElementType}}> Build(TEnum source) {
            global::System.Collections.Generic.List<{{source.ElementType}}> result;
            if(source.TryGetNonEnumeratedCount(out var __count)) {
                result = new global::System.Collections.Generic.List<{{source.ElementType}}>(__count);
            } else {
                result = new global::System.Collections.Generic.List<{{source.ElementType}}>();
            }
            try {
                while(source.TryGetNext(out var {{this.item}})) {
                    result.Add({{this.item}});
                }
            } finally {
                source.Dispose();
            }
            global::System.Random.Shared.Shuffle(global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result));
            return result;
        }
        """;
}
