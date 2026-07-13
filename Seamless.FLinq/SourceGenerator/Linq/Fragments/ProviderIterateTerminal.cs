namespace Seamless.FLinq.Linq.Fragments;

public sealed class ProviderIterateTerminal:ProviderNativeExecutionBase{
    private ProviderIterateTerminal(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        ProviderNativeRootBase root,
        FuncFragment? terminalPredicate=null,
        string? containsValue=null,
        string? containsComparer=null,
        FuncFragment? terminalFunc1=null,
        FuncFragment? terminalFunc2=null,
        string? terminalComparer=null)
        :base(
            number,
            pipeline,
            terminalKind,
            root.ProviderName,
            root.SourceVariablePrefix,
            root.SourceExpression,
            root.ElementType,
            root.StrategyInterfaceTypeName,
            terminalPredicate,
            containsValue,
            containsComparer,
            root.NativeOperationName,
            root.NativeExecutionMethodName,
            terminalFunc1,
            terminalFunc2,
            terminalComparer){
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        PipelineTerminalKind terminalKind,
        FuncFragment? terminalFunc1,
        FuncFragment? terminalFunc2,
        string? terminalComparer,
        out ProviderIterateTerminal terminal){
        terminal=null!;
        if(pipeline.Source is not ProviderNativeRootBase root||!CanCreateFor(pipeline,terminalKind)){
            return false;
        }
        terminal=new(number,pipeline,ToProviderIterateTerminalKind(terminalKind),root,terminalFunc1:terminalFunc1,terminalFunc2:terminalFunc2,terminalComparer:terminalComparer);
        return true;
    }

    public static bool TryCreate(
        int number,
        PipelineEnumerable pipeline,
        ProviderIterateTerminalKind terminalKind,
        FuncFragment? terminalPredicate,
        string? containsValue,
        string? containsComparer,
        out ProviderIterateTerminal terminal){
        terminal=null!;
        if(pipeline.Source is not ProviderNativeRootBase root
            ||!CanCreateFor(pipeline,terminalKind,terminalPredicate,containsValue)){
            return false;
        }
        terminal=new(number,pipeline,terminalKind,root,terminalPredicate,containsValue,containsComparer);
        return true;
    }
}
