namespace Seamless.FLinq.Linq.Fragments;

public sealed class TsavoriteHashSetIterateTerminal:ProviderIterateTerminalBase {
    private TsavoriteHashSetIterateTerminal(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        TsavoriteHashSetRoot root,
        FuncFragment? terminalPredicate = null,
        string? containsValue = null,
        string? containsComparer = null)
        : base(
            number,
            pipeline,
            terminalKind,
            "TsavoriteHashSet",
            "__tsavoriteIterateSource",
            root.SourceExpression,
            root.ElementType,
            "global::Seamless.Tsavorite.Collections.IHashSetIterateStrategy",
            terminalPredicate,
            containsValue,
            containsComparer) {
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        PipelineTerminalKind terminalKind,
        out TsavoriteHashSetIterateTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not TsavoriteHashSetRoot root) {
            return false;
        }
        if(!CanCreateFor(pipeline,terminalKind)) {
            return false;
        }

        terminal=new TsavoriteHashSetIterateTerminal(number,pipeline,ToProviderIterateTerminalKind(terminalKind),root);
        return true;
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        FuncFragment? terminalPredicate,
        string? containsValue,
        string? containsComparer,
        out TsavoriteHashSetIterateTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not TsavoriteHashSetRoot root) {
            return false;
        }
        if(!CanCreateFor(pipeline,terminalKind,terminalPredicate,containsValue)) {
            return false;
        }

        terminal=new TsavoriteHashSetIterateTerminal(number,pipeline,terminalKind,root,terminalPredicate,containsValue,containsComparer);
        return true;
    }
}
