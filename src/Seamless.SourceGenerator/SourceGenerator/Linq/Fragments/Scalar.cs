namespace Seamless.SourceGenerator.Linq.Fragments;
public abstract class Scalar(int 番号):Fragment(番号){
    protected virtual string GetEnumeratorDisposeBody => string.Empty;
    protected string DefineScalarEnumeratorStruct(string resultType) => this.DefineEnumeratorStructCore(resultType,this.GetEnumeratorDisposeBody);
    public override string ToString(){
        return $$"""
            {{this.GetBeginGlobal}}
            {{this.GetBeginLoopOutside}}
            {{this.GetBeginLoop}}
            {{this.GetBeginLoopInside}}
            {{this.GetEndLoopInside}}
            {{this.GetEndLoop}}
            {{this.GetEndLoopOutside}}
            {{this.GetEndGlobal}}
            {{this.result}}
            """;
    }
}
