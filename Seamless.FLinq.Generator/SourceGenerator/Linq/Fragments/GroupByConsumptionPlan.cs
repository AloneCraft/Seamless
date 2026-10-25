namespace Seamless.FLinq.Generator;

internal sealed record GroupByConsumptionPlan(bool Fuse,CountExpr? Limit,bool OutputValuesRequired,bool OrderSensitive,string Reason){
    public static GroupByConsumptionPlan Create(Demand demand){
        switch(demand.Window.Kind){
            case DemandWindowKind.All:return new(true,null,demand.ValuesUsed,demand.OrderSensitive,"All groups are consumed; preserve key and element evaluation.");
            case DemandWindowKind.None:return new(true,CountExpr.Constant(0),demand.ValuesUsed,demand.OrderSensitive,"No aggregate result is consumed.");
            case DemandWindowKind.Prefix:return new(true,demand.Window.End,demand.ValuesUsed,demand.OrderSensitive,"Aggregate only the demanded prefix; preserve group order.");
            default:return new(false,null,demand.ValuesUsed,demand.OrderSensitive,"Demand is not an exact prefix; preserve lazy group evaluation.");
        }
    }
}
