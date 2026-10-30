using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Test.SourceGenerator;

public class MultiStatementInterceptionTests(ITestOutputHelper output) {
    [Fact]
    public void MultiStatement_InSameMethod_BothAreInterceptedIndependently() {
        const string source = """
            using System.Collections.Generic;
            using System.Linq;

            namespace Sample;

            public class MultiQueryService {
                public int ExecuteBoth(int[] array1, int[] array2) {
                    var count = array1.Where(x => x > 10).Select(x => x * 2).Count();
                    var sum = array2.Where(x => x < count).Sum();
                    return sum;
                }

                public int Run(int[] array1, int[] array2) => ExecuteBoth(array1, array2);
            }
            """;

        var (compilation,generated)=ExecutableGeneration.Generate(source,"MultiStatementInterception",1);
        Assert.Contains("Intercepts <c>ExecuteBoth()",generated);
        Assert.DoesNotContain("UnsupportedLinqExpansion(",generated);
        Assert.Contains("pipelineSpan", generated);
        Assert.Contains("pipelineSpan2", generated);
        Assert.Contains("var count=countResult;", generated);
        Assert.Contains("var sum=sumResult;", generated);
        ExecutableGeneration.Run(compilation,"Sample.MultiQueryService",type=>{
            var instance=Activator.CreateInstance(type)!;
            var expected=type.GetMethod("ExecuteBoth")!;
            var actual=type.GetMethod("Run")!;
            foreach(var input in new[]{
                (new[]{11,12,3},new[]{-3,0,1,2,3}),
                (Array.Empty<int>(),new[]{-2,0,1}),
                (new[]{11,12,13},Array.Empty<int>()),
                (new[]{11},new[]{int.MinValue,-1})}){
                (object? value,Type? exception) Invoke(global::System.Reflection.MethodInfo method){
                    try{return(method.Invoke(instance,[input.Item1,input.Item2]),null);}
                    catch(global::System.Reflection.TargetInvocationException exception){return(null,exception.InnerException?.GetType());}
                }
                Assert.Equal(Invoke(expected),Invoke(actual));
            }
            return true;
        });
    }
}
