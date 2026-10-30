namespace Seamless.FLinq.Generator;

internal enum JoinLookupKind{Range,KeyCounts,Elements}
internal enum ResultEvaluation{Preserve}

// 意味判断のスナップショット。値が不要でも既存の評価・Anchor・例外を削除しない。
internal sealed record JoinConsumptionPlan(bool OutputValuesRequired,ResultEvaluation Evaluation,bool OrderSensitive,
    JoinLookupKind Lookup,bool RequiresLookupDispose,string Reason){
    public static JoinConsumptionPlan Create(Demand demand,bool probesRange,bool countsOnly,bool containsAnchor){
        var lookup=JoinLookupKind.Elements;
        if(probesRange){lookup=JoinLookupKind.Range;}
        else if(countsOnly){lookup=JoinLookupKind.KeyCounts;}
        var reason="Preserve result-selector evaluation, key order, empty-outer laziness and disposal.";
        if(containsAnchor){reason+=" Anchor evaluation is required.";}
        return new(demand.ValuesUsed,ResultEvaluation.Preserve,demand.OrderSensitive,lookup,lookup==JoinLookupKind.Elements,reason);
    }
}
