namespace Seamless.FLinq.Linq.Fragments;
public class Prepend(int 番号, Enumerable source, string element) : Enumerable(番号) {
    private Enumerable Source => source;
    private string ElementExpression => element;
    private Enumerable RootSource => this.Source is Prepend prepend ? prepend.RootSource : this.Source;
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
    private static void CollectElements(Prepend prepend,List<string> list) {
        list.Add(prepend.ElementExpression);
        if(prepend.Source is Prepend nested) {
            CollectElements(nested,list);
        }
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
            for(var i = 0; i <this.FlattenedElements.Count; i++) {
                builder.AppendLine($"if(this.{this.state}=={i}) {{");
                builder.AppendLine($"    this.{this.state}={i+1};");
                builder.AppendLine($"    current=this.{this.currentValue}=element{i};");
                builder.AppendLine("    return true;");
                builder.AppendLine("}");
            }
            builder.AppendLine($"if({this.e}.MoveNext()) {{");
            builder.AppendLine($"    current=this.{this.currentValue}={this.e}.Current;");
            builder.AppendLine("    return true;");
            builder.AppendLine("}");
            builder.AppendLine("current=default!;");
            builder.AppendLine("return false;");
            return builder.ToString();
        }
    }
    public override string GetCurrentExpression => this.currentValue;
    public override string ElementType => source.ElementType;
}
