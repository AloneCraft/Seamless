namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Enumerable(int 番号):Fragment(番号){
    public abstract string ElementType{ get; }
    public abstract string GetAdd{ get; }
    public abstract string GetElement { get; }
    public abstract string MoveNext { get; }
    public override bool CanExpandInLocalMethodBody => true;
    public string GetEnumerableExpression => $"new global::Seamless.Linq.Carrier.ValueEnumerable<{this.EnumeratorTypeName}, {this.ElementType}>({this.GetEnumeratorExpression})";
    protected virtual string GetEnumeratorDisposeBody => $"{this.e}.Dispose();";
    public override string GetEnumeratorStruct => this.DefineEnumeratorStructCore(this.ElementType,this.GetEnumeratorDisposeBody);
    public string If_MoveNext_goto(string label)     => $"if({this.MoveNext})goto {label}";
    public string If_Not_MoveNext_goto(string label) => $"if(!{this.MoveNext})goto {label}";
    public override string ToString(){
        return $$"""
            {{this.GetBeginGlobal}}
            {{this.GetBeginLoopOutside}}
            {{this.GetBeginLoop}}
            {{this.GetBeginLoopInside}}
            {{this.GetAdd}}
            {{this.GetEndLoopInside}}
            {{this.GetEndLoop}}
            {{this.GetEndLoopOutside}}
            {{this.GetEndGlobal}}
            {{this.result}}
            """;
    }
}
