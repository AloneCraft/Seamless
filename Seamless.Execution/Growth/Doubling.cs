namespace Seamless.Execution.Growth;

public readonly struct Doubling:IStrategy<Doubling>{
    public static string ID=>"doubling";
    public static int Version=>1;
    public static Decision Decide(ref Doubling self,in Context context){
        if(context.RequiredBytes<=context.CurrentBytes){return new(Status.Keep,context.CurrentBytes,"The current capacity is sufficient.");}
        var doubled=long.MaxValue;
        if(context.CurrentBytes<=long.MaxValue/2){doubled=context.CurrentBytes*2;}
        var target=Capacity.Round(Math.Max(context.RequiredBytes,doubled),in context);
        return new(Status.Grow,target,"Double the capacity until the required size fits.");
    }
}
