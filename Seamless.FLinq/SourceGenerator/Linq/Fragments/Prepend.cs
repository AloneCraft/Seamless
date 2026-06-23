namespace Seamless.FLinq.Linq.Fragments;
/*
Prepend は source.Prepend(value) を、追加要素を先に返してから source を列挙する状態機械へ変換する。

入力例:
  source.Prepend(a).Prepend(b)

生成例:
  if(state == 0) return b;
  if(state == 1) return a;
  if(source.MoveNext()) return source.Current;
  return false;

設計意図:
  連続 Prepend は先頭側から FlattenedElements に畳み込み、source の enumerator は1つだけ保持する。
*/
public class Prepend(int 番号, Enumerable source, string element) : Enumerable(番号) {
    private Enumerable Source => source;
    private string ElementExpression => element;
    private Enumerable RootSource => Source is Prepend prepend ? prepend.RootSource : Source;
    private IReadOnlyList<string> FlattenedElements {
        get {
            var list = new List<string>();
            CollectElements(this,list);
            return list;
        }
    }
    private string EnumeratorElementParameters =>
        string.Concat(FlattenedElements.Select((_,index) => $", {ElementType} element{index}"));
    private string EnumeratorElementArguments =>
        string.Concat(FlattenedElements.Select((value,index) => $", {value}"));
    private static void CollectElements(Prepend prepend,List<string> list) {
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
            var builder = new StringBuilder();
            for(var i = 0; i <FlattenedElements.Count; i++) {
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
