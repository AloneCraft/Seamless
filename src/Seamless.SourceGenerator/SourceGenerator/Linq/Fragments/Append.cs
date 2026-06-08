namespace Seamless.SourceGenerator.Linq.Fragments;

public class Append(int 番号, Enumerable source, string element) : Enumerable(番号) {
    private Enumerable Source => source;
    private string ElementExpression => element;
    private Enumerable RootSource => this.Source is Append append ? append.RootSource : this.Source;
    private IReadOnlyList<string> FlattenedElements {
        get {
            var list = new List<string>();
            CollectElements(this,list);
            return list;
        }
    }
    private string EnumeratorElementParameters =>
        string.Concat(this.FlattenedElements.Select((_,index) => $", {this.ElementType} element{index}"));
    private string EnumeratorElementArguments =>
        string.Concat(this.FlattenedElements.Select((value,index) => $", {value}"));
    private static void CollectElements(Append append,List<string> list) {
        if(append.Source is Append nested) {
            CollectElements(nested,list);
        }
        list.Add(append.ElementExpression);
    }
    protected override Enumerable PrimarySource => this.RootSource;
    public override string StructMethod => string.Empty;
    public override string GetEnumerableExpression => base.GetEnumerableExpression;
    public override string GetEnumeratorStruct => $$"""
        {{this.RootSource.GetEnumeratorStruct}}
        {{base.GetEnumeratorStruct}}
        """;
    public override string GetEnumeratorExpression => $"new {this.EnumeratorTypeName}({this.RootSource.GetEnumeratorExpression}{this.EnumeratorElementArguments})";
    public override string GetEnumeratorPrimaryConstructorParameters => $"TEnum {this.e}{this.EnumeratorElementParameters}";
    public override string GetEnumeratorFields => $$"""
        private byte {{this.state}};
        """;
    public override string GetTryGetNextBody {
        get {
            var builder = new StringBuilder();
            builder.AppendLine($"if(this.{this.state}==0) {{");
            builder.AppendLine($"    if({this.e}.MoveNext()) {{");
            builder.AppendLine($"        current=this.{this.currentValue}={this.e}.Current;");
            builder.AppendLine("        return true;");
            builder.AppendLine("    }");
            builder.AppendLine($"    this.{this.state}=1;");
            builder.AppendLine("}");
            for(var i = 0; i <this.FlattenedElements.Count; i++) {
                builder.AppendLine($"if(this.{this.state}=={i+1}) {{");
                builder.AppendLine($"    this.{this.state}={i+2};");
                builder.AppendLine($"    current=this.{this.currentValue}=element{i};");
                builder.AppendLine("    return true;");
                builder.AppendLine("}");
            }
            builder.AppendLine("current=default!;");
            builder.AppendLine("return false;");
            return builder.ToString();
        }
    }
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}
