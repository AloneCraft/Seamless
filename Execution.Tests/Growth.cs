using Seamless.Execution.Growth;

namespace Execution.Tests;

public sealed class Growth{
    [Fact]
    public void Decide(){
        var context=new Context(Resource.Index,64,65,1024);
        Assert.Equal(64,new Policy<Fixed>().CreateController().Decide(context).TargetBytes);
        Assert.Equal(128,new Policy<Doubling>().CreateController().Decide(context).TargetBytes);
        Assert.Equal(Status.Rejected,new Policy<Doubling>().CreateController().Decide(context with{LimitBytes=64}).Status);
        Assert.Equal(Status.Deferred,new Policy<Doubling>().CreateController().Decide(context with{IsSafePoint=false}).Status);
        Assert.Equal(512,new Policy<Estimated>().CreateController().Decide(context with{RecommendedBytes=512}).TargetBytes);
    }

    [Fact]
    public void CustomStateIsOwnedByEachController(){
        var policy=new Policy<Custom>();
        var first=policy.CreateController();
        var second=policy.CreateController();
        var context=new Context(Resource.Index,64,65,1024);
        Assert.Equal("1",first.Decide(context).Reason);
        Assert.Equal("2",first.Decide(context).Reason);
        Assert.Equal("1",second.Decide(context).Reason);
    }

    private struct Custom:IStrategy<Custom>{
        private int calls;
        public static string ID=>"test-custom";
        public static int Version=>1;
        public static Decision Decide(ref Custom self,in Context context){
            self.calls++;
            return new(Status.Grow,128,self.calls.ToString());
        }
    }
}
