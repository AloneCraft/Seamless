namespace Seamless.FLinq.Linq.Fragments;

public sealed class PersistedSetIterateTerminal:ProviderIterateTerminalBase {
    private PersistedSetIterateTerminal(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        PersistedSetRoot root,
        FuncFragment? terminalPredicate = null,
        string? containsValue = null,
        string? containsComparer = null)
        : base(
            number,
            pipeline,
            terminalKind,
            "PersistedSet",
            "__persistedIterateSource",
            root.SourceExpression,
            root.ElementType,
            "global::Seamless.Persisted.Collections.ISetIterateStrategy",
            terminalPredicate,
            containsValue,
            containsComparer) {
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        PipelineTerminalKind terminalKind,
        out PersistedSetIterateTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not PersistedSetRoot root) {
            return false;
        }
        if(!CanCreateFor(pipeline,terminalKind)) {
            return false;
        }

        terminal=new PersistedSetIterateTerminal(number,pipeline,ToProviderIterateTerminalKind(terminalKind),root);
        return true;
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        FuncFragment? terminalPredicate,
        string? containsValue,
        string? containsComparer,
        out PersistedSetIterateTerminal terminal) {
        terminal=null!;
        if(pipeline.Source is not PersistedSetRoot root) {
            return false;
        }
        if(!CanCreateFor(pipeline,terminalKind,terminalPredicate,containsValue)) {
            return false;
        }

        terminal=new PersistedSetIterateTerminal(number,pipeline,terminalKind,root,terminalPredicate,containsValue,containsComparer);
        return true;
    }
}
