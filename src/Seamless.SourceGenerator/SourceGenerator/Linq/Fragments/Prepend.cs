namespace Seamless.SourceGenerator.Linq.Fragments;
public class Prepend(int 番号, Enumerable source, string element) : Enumerable(番号) {
    private Enumerable Source => source;
    private string ElementExpression => element;
    private Enumerable RootSource => Source is Prepend prepend ? prepend.RootSource : Source;
    private global::System.Collections.Generic.IReadOnlyList<string> FlattenedElements {
        get {
            var list = new global::System.Collections.Generic.List<string>();
            CollectElements(this,list);
            return list;
        }
    }
    private string EnumeratorElementParameters =>
        string.Concat(FlattenedElements.Select((_,index) => $", {ElementType} element{index}"));
    private string EnumeratorElementArguments =>
        string.Concat(FlattenedElements.Select((value,index) => $", {value}"));
    private static void CollectElements(Prepend prepend,global::System.Collections.Generic.List<string> list) {
        list.Add(prepend.ElementExpression);
        if(prepend.Source is Prepend nested) {
            CollectElements(nested,list);
        }
    }
    protected override Enumerable PrimarySource => RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression => base.GetEnumerableExpression;
    public override string GetEnumeratorStruct => $$"""
        {{RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {EnumeratorTypeName}({RootSource.GetEnumeratorExpression}{EnumeratorElementArguments})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {e}{EnumeratorElementParameters}";
    public override string GetEnumeratorFields => $$"""
        private byte {{state}};
        """;
    public override string GetTryGetNextBody {
        get {
            var builder = new global::System.Text.StringBuilder();
            for(var i = 0; i < FlattenedElements.Count; i++) {
                builder.AppendLine($"if(this.{state}=={i}) {{");
                builder.AppendLine($"    this.{state}={i+1};");
                builder.AppendLine($"    current=this.{currentValue}=element{i};");
                builder.AppendLine("    return true;");
                builder.AppendLine("}");
            }
            builder.AppendLine($"if({e}.MoveNext()) {{");
            builder.AppendLine($"    current=this.{currentValue}={e}.Current;");
            builder.AppendLine("    return true;");
            builder.AppendLine("}");
            builder.AppendLine("current=default!;");
            builder.AppendLine("return false;");
            return builder.ToString();
        }
    }
    public override string GetCurrentExpression => currentValue;
    public override string ElementType => source.ElementType;
}
