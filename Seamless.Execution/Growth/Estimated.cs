namespace Seamless.Execution.Growth;

public readonly struct Estimated:IStrategy<Estimated>{
    public static string ID=>"estimated";
    public static int Version=>1;
    public static Decision Decide(ref Estimated self,in Context context){
        var estimate=context.RequiredBytes;
        if(context.ExpectedCount>0&&double.IsFinite(context.BytesPerItem)&&context.BytesPerItem>0){
            var bytes=context.ExpectedCount*context.BytesPerItem;
            if(bytes>=long.MaxValue){estimate=long.MaxValue;}
            else{estimate=Math.Max(estimate,(long)Math.Ceiling(bytes));}
        }
        estimate=Math.Max(estimate,context.RecommendedBytes);
        if(estimate<=context.CurrentBytes){return new(Status.Keep,context.CurrentBytes,"The current capacity satisfies the estimate.");}
        var maximum=context.LimitBytes/context.AllocationUnit*context.AllocationUnit;
        if(context.PowerOfTwo){
            maximum=0;
            if(context.LimitBytes>=context.AllocationUnit){maximum=context.AllocationUnit;while(maximum<=context.LimitBytes/2){maximum*=2;}}
        }
        if(maximum<context.RequiredBytes){return new(Status.Rejected,context.CurrentBytes,"The required size exceeds the resource limit.");}
        var target=Capacity.Round(Math.Min(estimate,maximum),in context);
        if(target>maximum){target=maximum;}
        var reason="Use the required size, observed item size and measured profile recommendation.";
        if(estimate>maximum){reason="The forecast is bounded by the resource limit; the required size still fits.";}
        return new(Status.Grow,target,reason);
    }
}
