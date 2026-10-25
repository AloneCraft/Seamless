namespace Seamless.Execution.Growth;

public readonly struct Estimated:IStrategy<Estimated>{
    public static string ID=>"estimated";
    public static int Version=>1;
    public static Decision Decide(ref Estimated self,in Context context){
        var estimate=context.RequiredBytes;
        if(context.ExpectedCount>0&&double.IsFinite(context.BytesPerItem)&&context.BytesPerItem>0){
            var bytes=context.ExpectedCount*context.BytesPerItem;
            if(bytes>=long.MaxValue){return new(Status.Rejected,context.CurrentBytes,"The estimated size is not representable.");}
            estimate=Math.Max(estimate,(long)Math.Ceiling(bytes));
        }
        estimate=Math.Max(estimate,context.RecommendedBytes);
        if(estimate<=context.CurrentBytes){return new(Status.Keep,context.CurrentBytes,"The current capacity satisfies the estimate.");}
        return new(Status.Grow,Capacity.Round(estimate,in context),"Use the required size, observed item size and measured profile recommendation.");
    }
}
