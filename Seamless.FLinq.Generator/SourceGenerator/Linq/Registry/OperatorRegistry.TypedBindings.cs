namespace Seamless.FLinq.Generator;

internal static partial class OperatorRegistry {
    private static Aggregate_seed_func_resultSelectorArguments? BindAggregate_seed_func_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"seed",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Aggregate_seed_func_resultSelectorArguments(number, value0, value1, value2, value3);
    }
    private static Aggregate_seed_funcArguments? BindAggregate_seed_func(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"seed",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Aggregate_seed_funcArguments(number, value0, value1, value2);
    }
    private static Aggregate_funcArguments? BindAggregate_func(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Aggregate_funcArguments(number, value0, value1);
    }
    private static AggregateByArguments? BindAggregateBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"seed",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.TryGetResultElementType(out var value4)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AggregateByArguments(number, value0, value1, value2, value3, value4);
    }
    private static All_predicateArguments? BindAll_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new All_predicateArguments(number, value0, value1);
    }
    private static AnonymousExitToListArguments? BindAnonymousExitToList(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AnonymousExitToListArguments(number, value0);
    }
    private static Any_indexPredicateArguments? BindAny_indexPredicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Any_indexPredicateArguments(number, value0, value1);
    }
    private static Any_predicateArguments? BindAny_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Any_predicateArguments(number, value0, value1);
    }
    private static AnyArguments? BindAny(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AnyArguments(number, value0);
    }
    private static AppendArguments? BindAppend(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"element",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AppendArguments(number, value0, value1);
    }
    private static AsEnumerableArguments? BindAsEnumerable(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AsEnumerableArguments(number, value0);
    }
    private static Average_selectorArguments? BindAverage_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Average_selectorArguments(number, value0, value1, value2);
    }
    private static AverageArguments? BindAverage(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new AverageArguments(number, value0, value1);
    }
    private static CastArguments? BindCast(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryGetResultElementType(out var value1)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new CastArguments(number, value0, value1);
    }
    private static ChunkArguments? BindChunk(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"size",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ChunkArguments(number, value0, value1);
    }
    private static ConcatArguments? BindConcat(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ConcatArguments(number, value0, value1);
    }
    private static Contains_comparerArguments? BindContains_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"value",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Contains_comparerArguments(number, value0, value1, value2, value3);
    }
    private static ContainsArguments? BindContains(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"value",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ContainsArguments(number, value0, value1, value2);
    }
    private static Count_predicateArguments? BindCount_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Count_predicateArguments(number, value0, value1, value2);
    }
    private static CountArguments? BindCount(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new CountArguments(number, value0, value1);
    }
    private static CountByArguments? BindCountBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new CountByArguments(number, value0, value1);
    }
    private static DefaultIfEmpty_defaultValueArguments? BindDefaultIfEmpty_defaultValue(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"defaultValue",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new DefaultIfEmpty_defaultValueArguments(number, value0, value1);
    }
    private static DefaultIfEmptyArguments? BindDefaultIfEmpty(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new DefaultIfEmptyArguments(number, value0);
    }
    private static Distinct_comparerArguments? BindDistinct_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Distinct_comparerArguments(number, value0, value1);
    }
    private static DistinctArguments? BindDistinct(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new DistinctArguments(number, value0);
    }
    private static DistinctBy_comparerArguments? BindDistinctBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new DistinctBy_comparerArguments(number, value0, value1, value2);
    }
    private static DistinctByArguments? BindDistinctBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new DistinctByArguments(number, value0, value1);
    }
    private static ElementAtArguments? BindElementAt(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"index",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ElementAtArguments(number, value0, value1, value2);
    }
    private static ElementAt_indexArguments? BindElementAt_index(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"index",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ElementAt_indexArguments(number, value0, value1, value2);
    }
    private static ElementAtOrDefaultArguments? BindElementAtOrDefault(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"index",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ElementAtOrDefaultArguments(number, value0, value1, value2);
    }
    private static ElementAtOrDefault_indexArguments? BindElementAtOrDefault_index(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"index",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ElementAtOrDefault_indexArguments(number, value0, value1, value2);
    }
    private static Except_comparerArguments? BindExcept_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Except_comparerArguments(number, value0, value1, value2);
    }
    private static ExceptArguments? BindExcept(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ExceptArguments(number, value0, value1);
    }
    private static ExceptBy_comparerArguments? BindExceptBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value3,out var consumed3)) return null;
        if(consumed3) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ExceptBy_comparerArguments(number, value0, value1, value2, value3);
    }
    private static ExceptByArguments? BindExceptBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ExceptByArguments(number, value0, value1, value2);
    }
    private static First_predicateArguments? BindFirst_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new First_predicateArguments(number, value0, value1, value2);
    }
    private static FirstArguments? BindFirst(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new FirstArguments(number, value0, value1);
    }
    private static FirstOrDefault_defaultValueArguments? BindFirstOrDefault_defaultValue(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"defaultValue",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new FirstOrDefault_defaultValueArguments(number, value0, value1, value2);
    }
    private static FirstOrDefault_predicateArguments? BindFirstOrDefault_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new FirstOrDefault_predicateArguments(number, value0, value1, value2);
    }
    private static FirstOrDefaultArguments? BindFirstOrDefault(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new FirstOrDefaultArguments(number, value0, value1);
    }
    private static GroupBy_keySelector_elementSelector_resultSelectorArguments? BindGroupBy_keySelector_elementSelector_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new GroupBy_keySelector_elementSelector_resultSelectorArguments(number, value0, value1, value2, value3);
    }
    private static GroupBy_keySelector_elementSelectorArguments? BindGroupBy_keySelector_elementSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new GroupBy_keySelector_elementSelectorArguments(number, value0, value1, value2);
    }
    private static GroupBy_keySelector_resultSelectorArguments? BindGroupBy_keySelector_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new GroupBy_keySelector_resultSelectorArguments(number, value0, value1, value2);
    }
    private static GroupBy_keySelectorArguments? BindGroupBy_keySelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new GroupBy_keySelectorArguments(number, value0, value1);
    }
    private static GroupJoinArguments? BindGroupJoin(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value4)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value5,out var consumed5)) return null;
        if(consumed5) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new GroupJoinArguments(number, value0, value1, value2, value3, value4, value5);
    }
    private static IndexArguments? BindIndex(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new IndexArguments(number, value0);
    }
    private static Intersect_comparerArguments? BindIntersect_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Intersect_comparerArguments(number, value0, value1, value2);
    }
    private static IntersectArguments? BindIntersect(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new IntersectArguments(number, value0, value1);
    }
    private static IntersectBy_comparerArguments? BindIntersectBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value3,out var consumed3)) return null;
        if(consumed3) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new IntersectBy_comparerArguments(number, value0, value1, value2, value3);
    }
    private static IntersectByArguments? BindIntersectBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new IntersectByArguments(number, value0, value1, value2);
    }
    private static JoinArguments? BindJoin(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value4)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value5,out var consumed5)) return null;
        if(consumed5) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new JoinArguments(number, value0, value1, value2, value3, value4, value5);
    }
    private static Last_predicateArguments? BindLast_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Last_predicateArguments(number, value0, value1, value2);
    }
    private static LastArguments? BindLast(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LastArguments(number, value0, value1);
    }
    private static LastOrDefault_defaultValueArguments? BindLastOrDefault_defaultValue(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"defaultValue",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LastOrDefault_defaultValueArguments(number, value0, value1, value2);
    }
    private static LastOrDefault_predicateArguments? BindLastOrDefault_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LastOrDefault_predicateArguments(number, value0, value1, value2);
    }
    private static LastOrDefaultArguments? BindLastOrDefault(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LastOrDefaultArguments(number, value0, value1);
    }
    private static LeftJoinArguments? BindLeftJoin(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value4)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value5,out var consumed5)) return null;
        if(consumed5) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LeftJoinArguments(number, value0, value1, value2, value3, value4, value5);
    }
    private static LongCount_predicateArguments? BindLongCount_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LongCount_predicateArguments(number, value0, value1, value2);
    }
    private static LongCountArguments? BindLongCount(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new LongCountArguments(number, value0, value1);
    }
    private static Max_selectorArguments? BindMax_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Max_selectorArguments(number, value0, value1, value2);
    }
    private static MaxArguments? BindMax(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new MaxArguments(number, value0, value1);
    }
    private static MaxBy_keySelectorArguments? BindMaxBy_keySelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new MaxBy_keySelectorArguments(number, value0, value1, value2);
    }
    private static Min_selectorArguments? BindMin_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Min_selectorArguments(number, value0, value1, value2);
    }
    private static MinArguments? BindMin(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new MinArguments(number, value0, value1);
    }
    private static MinBy_keySelectorArguments? BindMinBy_keySelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new MinBy_keySelectorArguments(number, value0, value1, value2);
    }
    private static OfTypeArguments? BindOfType(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryGetResultElementType(out var value1)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new OfTypeArguments(number, value0, value1);
    }
    private static OrderBy_comparerArguments? BindOrderBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new OrderBy_comparerArguments(number, value0, value1, value2);
    }
    private static OrderByArguments? BindOrderBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new OrderByArguments(number, value0, value1);
    }
    private static OrderByDescending_comparerArguments? BindOrderByDescending_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new OrderByDescending_comparerArguments(number, value0, value1, value2);
    }
    private static OrderByDescendingArguments? BindOrderByDescending(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new OrderByDescendingArguments(number, value0, value1);
    }
    private static PrependArguments? BindPrepend(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"element",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new PrependArguments(number, value0, value1);
    }
    private static ReverseArguments? BindReverse(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ReverseArguments(number, value0);
    }
    private static RightJoinArguments? BindRightJoin(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value3)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value4)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value5,out var consumed5)) return null;
        if(consumed5) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new RightJoinArguments(number, value0, value1, value2, value3, value4, value5);
    }
    private static Select_indexSelectorArguments? BindSelect_indexSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Select_indexSelectorArguments(number, value0, value1);
    }
    private static Select_selectorArguments? BindSelect_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Select_selectorArguments(number, value0, value1);
    }
    private static SelectMany_indexSelector_resultSelectorArguments? BindSelectMany_indexSelector_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SelectMany_indexSelector_resultSelectorArguments(number, value0, value1, value2);
    }
    private static SelectMany_selector_resultSelectorArguments? BindSelectMany_selector_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SelectMany_selector_resultSelectorArguments(number, value0, value1, value2);
    }
    private static SelectMany_WithInlinedInnerArguments? BindSelectMany_WithInlinedInner(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"outerParamName",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SelectMany_WithInlinedInnerArguments(number, value0, value1, value2);
    }
    private static SelectMany_indexSelectorArguments? BindSelectMany_indexSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SelectMany_indexSelectorArguments(number, value0, value1);
    }
    private static SelectMany_selectorArguments? BindSelectMany_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SelectMany_selectorArguments(number, value0, value1);
    }
    private static SequenceEqual_comparerArguments? BindSequenceEqual_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SequenceEqual_comparerArguments(number, value0, value1, value2, value3);
    }
    private static SequenceEqualArguments? BindSequenceEqual(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SequenceEqualArguments(number, value0, value1, value2);
    }
    private static ShuffleArguments? BindShuffle(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ShuffleArguments(number, value0);
    }
    private static Single_predicateArguments? BindSingle_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Single_predicateArguments(number, value0, value1, value2);
    }
    private static SingleArguments? BindSingle(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SingleArguments(number, value0, value1);
    }
    private static SingleOrDefault_defaultValueArguments? BindSingleOrDefault_defaultValue(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"defaultValue",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SingleOrDefault_defaultValueArguments(number, value0, value1, value2);
    }
    private static SingleOrDefault_predicateArguments? BindSingleOrDefault_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SingleOrDefault_predicateArguments(number, value0, value1, value2);
    }
    private static SingleOrDefaultArguments? BindSingleOrDefault(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SingleOrDefaultArguments(number, value0, value1);
    }
    private static SkipArguments? BindSkip(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"skipCount",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SkipArguments(number, value0, value1);
    }
    private static SkipLastArguments? BindSkipLast(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"skipCount",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SkipLastArguments(number, value0, value1);
    }
    private static SkipWhile_indexPredicateArguments? BindSkipWhile_indexPredicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SkipWhile_indexPredicateArguments(number, value0, value1);
    }
    private static SkipWhile_predicateArguments? BindSkipWhile_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SkipWhile_predicateArguments(number, value0, value1);
    }
    private static Sum_selectorArguments? BindSum_selector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Sum_selectorArguments(number, value0, value1, value2);
    }
    private static SumArguments? BindSum(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new SumArguments(number, value0, value1);
    }
    private static TakeArguments? BindTake(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"takeCount",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new TakeArguments(number, value0, value1);
    }
    private static TakeLastArguments? BindTakeLast(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"takeCount",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new TakeLastArguments(number, value0, value1);
    }
    private static TakeWhile_indexPredicateArguments? BindTakeWhile_indexPredicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new TakeWhile_indexPredicateArguments(number, value0, value1);
    }
    private static TakeWhile_predicateArguments? BindTakeWhile_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new TakeWhile_predicateArguments(number, value0, value1);
    }
    private static ThenBy_comparerArguments? BindThenBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ThenBy_comparerArguments(number, value0, value1, value2);
    }
    private static ThenByArguments? BindThenBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ThenByArguments(number, value0, value1);
    }
    private static ThenByDescending_comparerArguments? BindThenByDescending_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ThenByDescending_comparerArguments(number, value0, value1, value2);
    }
    private static ThenByDescendingArguments? BindThenByDescending(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ThenByDescendingArguments(number, value0, value1);
    }
    private static ToArrayArguments? BindToArray(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToArrayArguments(number, value0, value1);
    }
    private static ToDictionary_keySelector_elementSelector_comparerArguments? BindToDictionary_keySelector_elementSelector_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value3,out var consumed3)) return null;
        if(consumed3) argumentIndex++;
        var value4=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToDictionary_keySelector_elementSelector_comparerArguments(number, value0, value1, value2, value3, value4);
    }
    private static ToDictionary_keySelector_comparerArguments? BindToDictionary_keySelector_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToDictionary_keySelector_comparerArguments(number, value0, value1, value2, value3);
    }
    private static ToDictionary_keySelector_elementSelectorArguments? BindToDictionary_keySelector_elementSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToDictionary_keySelector_elementSelectorArguments(number, value0, value1, value2, value3);
    }
    private static ToDictionary_keySelectorArguments? BindToDictionary_keySelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToDictionary_keySelectorArguments(number, value0, value1, value2);
    }
    private static ToHashSet_comparerArguments? BindToHashSet_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value1,out var consumed1)) return null;
        if(consumed1) argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToHashSet_comparerArguments(number, value0, value1, value2);
    }
    private static ToHashSetArguments? BindToHashSet(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToHashSetArguments(number, value0, value1);
    }
    private static ToListArguments? BindToList(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        var value1=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToListArguments(number, value0, value1);
    }
    private static ToLookup_keySelector_elementSelector_comparerArguments? BindToLookup_keySelector_elementSelector_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value3,out var consumed3)) return null;
        if(consumed3) argumentIndex++;
        var value4=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToLookup_keySelector_elementSelector_comparerArguments(number, value0, value1, value2, value3, value4);
    }
    private static ToLookup_keySelector_comparerArguments? BindToLookup_keySelector_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToLookup_keySelector_comparerArguments(number, value0, value1, value2, value3);
    }
    private static ToLookup_keySelector_elementSelectorArguments? BindToLookup_keySelector_elementSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        var value3=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToLookup_keySelector_elementSelectorArguments(number, value0, value1, value2, value3);
    }
    private static ToLookup_keySelectorArguments? BindToLookup_keySelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        var value2=context.ResultType;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ToLookup_keySelectorArguments(number, value0, value1, value2);
    }
    private static Union_comparerArguments? BindUnion_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value2,out var consumed2)) return null;
        if(consumed2) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Union_comparerArguments(number, value0, value1, value2);
    }
    private static UnionArguments? BindUnion(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new UnionArguments(number, value0, value1);
    }
    private static UnionBy_comparerArguments? BindUnionBy_comparer(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.TryBuildExpressionArgument(argumentIndex,"comparer",out var value3,out var consumed3)) return null;
        if(consumed3) argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new UnionBy_comparerArguments(number, value0, value1, value2, value3);
    }
    private static UnionByArguments? BindUnionBy(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new UnionByArguments(number, value0, value1, value2);
    }
    private static Where_indexPredicateArguments? BindWhere_indexPredicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Where_indexPredicateArguments(number, value0, value1);
    }
    private static Where_predicateArguments? BindWhere_predicate(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Where_predicateArguments(number, value0, value1);
    }
    private static Zip_resultSelectorArguments? BindZip_resultSelector(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildFunctionArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Zip_resultSelectorArguments(number, value0, value1, value2);
    }
    private static Zip3Arguments? BindZip3(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value2)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new Zip3Arguments(number, value0, value1, value2);
    }
    private static ZipArguments? BindZip(FLinqIncrementalGenerator.FragmentFactoryContext context) {
        var number=context.TakeUniqueNumber();
        var argumentIndex=0;
        if(!context.TryBuildSource(out var value0)) return null;
        if(!context.TryBuildEnumerableArgument(argumentIndex,out var value1)) return null;
        argumentIndex++;
        if(!context.HasExactlyArguments(argumentIndex)) return null;
        return new ZipArguments(number, value0, value1);
    }
    private static EmptyRootArguments? BindEmptyRoot(FLinqIncrementalGenerator.RootFactoryContext context) {
        var number=context.TakeUniqueNumber();
        if(!context.TryGetResultElementType(out var value0)) return null;
        return new EmptyRootArguments(number, value0);
    }
    private static InfiniteSequenceRootArguments? BindInfiniteSequenceRoot(FLinqIncrementalGenerator.RootFactoryContext context) {
        var number=context.TakeUniqueNumber();
        if(!context.TryBuildExpressionArgument(0,"start",out var value0,out var consumed0)) return null;
        if(!context.TryBuildExpressionArgument(1,"next",out var value1,out var consumed1)) return null;
        if(!context.TryGetResultElementType(out var value2)) return null;
        return new InfiniteSequenceRootArguments(number, value0, value1, value2);
    }
    private static RangeRootArguments? BindRangeRoot(FLinqIncrementalGenerator.RootFactoryContext context) {
        var number=context.TakeUniqueNumber();
        if(!context.TryBuildExpressionArgument(0,"start",out var value0,out var consumed0)) return null;
        if(!context.TryBuildExpressionArgument(1,"count",out var value1,out var consumed1)) return null;
        return new RangeRootArguments(number, value0, value1);
    }
    private static RepeatRootArguments? BindRepeatRoot(FLinqIncrementalGenerator.RootFactoryContext context) {
        var number=context.TakeUniqueNumber();
        if(!context.TryBuildExpressionArgument(0,"element",out var value0,out var consumed0)) return null;
        if(!context.TryBuildExpressionArgument(1,"count",out var value1,out var consumed1)) return null;
        if(!context.TryGetResultElementType(out var value2)) return null;
        return new RepeatRootArguments(number, value0, value1, value2);
    }
    private static SequenceRootArguments? BindSequenceRoot(FLinqIncrementalGenerator.RootFactoryContext context) {
        var number=context.TakeUniqueNumber();
        if(!context.TryBuildExpressionArgument(0,"start",out var value0,out var consumed0)) return null;
        if(!context.TryBuildExpressionArgument(1,"next",out var value1,out var consumed1)) return null;
        if(!context.TryBuildExpressionArgument(2,"condition",out var value2,out var consumed2)) return null;
        if(!context.TryGetResultElementType(out var value3)) return null;
        return new SequenceRootArguments(number, value0, value1, value2, value3);
    }
    private static Aggregate_funcArguments? DirectBindAggregate_func(DirectFragmentArguments arguments) => Aggregate_funcArguments.FromDirect(arguments);
    private static Aggregate_seed_funcArguments? DirectBindAggregate_seed_func(DirectFragmentArguments arguments) => Aggregate_seed_funcArguments.FromDirect(arguments);
    private static Aggregate_seed_func_resultSelectorArguments? DirectBindAggregate_seed_func_resultSelector(DirectFragmentArguments arguments) => Aggregate_seed_func_resultSelectorArguments.FromDirect(arguments);
    private static AggregateByArguments? DirectBindAggregateBy(DirectFragmentArguments arguments) => AggregateByArguments.FromDirect(arguments);
    private static All_predicateArguments? DirectBindAll_predicate(DirectFragmentArguments arguments) => All_predicateArguments.FromDirect(arguments);
    private static AnonymousExitToListArguments? DirectBindAnonymousExitToList(DirectFragmentArguments arguments) => AnonymousExitToListArguments.FromDirect(arguments);
    private static AnyArguments? DirectBindAny(DirectFragmentArguments arguments) => AnyArguments.FromDirect(arguments);
    private static Any_indexPredicateArguments? DirectBindAny_indexPredicate(DirectFragmentArguments arguments) => Any_indexPredicateArguments.FromDirect(arguments);
    private static Any_predicateArguments? DirectBindAny_predicate(DirectFragmentArguments arguments) => Any_predicateArguments.FromDirect(arguments);
    private static AppendArguments? DirectBindAppend(DirectFragmentArguments arguments) => AppendArguments.FromDirect(arguments);
    private static AsEnumerableArguments? DirectBindAsEnumerable(DirectFragmentArguments arguments) => AsEnumerableArguments.FromDirect(arguments);
    private static AverageArguments? DirectBindAverage(DirectFragmentArguments arguments) => AverageArguments.FromDirect(arguments);
    private static Average_selectorArguments? DirectBindAverage_selector(DirectFragmentArguments arguments) => Average_selectorArguments.FromDirect(arguments);
    private static CastArguments? DirectBindCast(DirectFragmentArguments arguments) => CastArguments.FromDirect(arguments);
    private static ChunkArguments? DirectBindChunk(DirectFragmentArguments arguments) => ChunkArguments.FromDirect(arguments);
    private static ConcatArguments? DirectBindConcat(DirectFragmentArguments arguments) => ConcatArguments.FromDirect(arguments);
    private static ContainsArguments? DirectBindContains(DirectFragmentArguments arguments) => ContainsArguments.FromDirect(arguments);
    private static Contains_comparerArguments? DirectBindContains_comparer(DirectFragmentArguments arguments) => Contains_comparerArguments.FromDirect(arguments);
    private static CountArguments? DirectBindCount(DirectFragmentArguments arguments) => CountArguments.FromDirect(arguments);
    private static Count_predicateArguments? DirectBindCount_predicate(DirectFragmentArguments arguments) => Count_predicateArguments.FromDirect(arguments);
    private static CountByArguments? DirectBindCountBy(DirectFragmentArguments arguments) => CountByArguments.FromDirect(arguments);
    private static DefaultIfEmptyArguments? DirectBindDefaultIfEmpty(DirectFragmentArguments arguments) => DefaultIfEmptyArguments.FromDirect(arguments);
    private static DefaultIfEmpty_defaultValueArguments? DirectBindDefaultIfEmpty_defaultValue(DirectFragmentArguments arguments) => DefaultIfEmpty_defaultValueArguments.FromDirect(arguments);
    private static DistinctArguments? DirectBindDistinct(DirectFragmentArguments arguments) => DistinctArguments.FromDirect(arguments);
    private static Distinct_comparerArguments? DirectBindDistinct_comparer(DirectFragmentArguments arguments) => Distinct_comparerArguments.FromDirect(arguments);
    private static DistinctByArguments? DirectBindDistinctBy(DirectFragmentArguments arguments) => DistinctByArguments.FromDirect(arguments);
    private static DistinctBy_comparerArguments? DirectBindDistinctBy_comparer(DirectFragmentArguments arguments) => DistinctBy_comparerArguments.FromDirect(arguments);
    private static ElementAtArguments? DirectBindElementAt(DirectFragmentArguments arguments) => ElementAtArguments.FromDirect(arguments);
    private static ElementAt_indexArguments? DirectBindElementAt_index(DirectFragmentArguments arguments) => ElementAt_indexArguments.FromDirect(arguments);
    private static ElementAtOrDefaultArguments? DirectBindElementAtOrDefault(DirectFragmentArguments arguments) => ElementAtOrDefaultArguments.FromDirect(arguments);
    private static ElementAtOrDefault_indexArguments? DirectBindElementAtOrDefault_index(DirectFragmentArguments arguments) => ElementAtOrDefault_indexArguments.FromDirect(arguments);
    private static EmptyRootArguments? DirectBindEmptyRoot(DirectFragmentArguments arguments) => EmptyRootArguments.FromDirect(arguments);
    private static ExceptArguments? DirectBindExcept(DirectFragmentArguments arguments) => ExceptArguments.FromDirect(arguments);
    private static Except_comparerArguments? DirectBindExcept_comparer(DirectFragmentArguments arguments) => Except_comparerArguments.FromDirect(arguments);
    private static ExceptByArguments? DirectBindExceptBy(DirectFragmentArguments arguments) => ExceptByArguments.FromDirect(arguments);
    private static ExceptBy_comparerArguments? DirectBindExceptBy_comparer(DirectFragmentArguments arguments) => ExceptBy_comparerArguments.FromDirect(arguments);
    private static FirstArguments? DirectBindFirst(DirectFragmentArguments arguments) => FirstArguments.FromDirect(arguments);
    private static First_predicateArguments? DirectBindFirst_predicate(DirectFragmentArguments arguments) => First_predicateArguments.FromDirect(arguments);
    private static FirstOrDefaultArguments? DirectBindFirstOrDefault(DirectFragmentArguments arguments) => FirstOrDefaultArguments.FromDirect(arguments);
    private static FirstOrDefault_defaultValueArguments? DirectBindFirstOrDefault_defaultValue(DirectFragmentArguments arguments) => FirstOrDefault_defaultValueArguments.FromDirect(arguments);
    private static FirstOrDefault_predicateArguments? DirectBindFirstOrDefault_predicate(DirectFragmentArguments arguments) => FirstOrDefault_predicateArguments.FromDirect(arguments);
    private static GroupBy_keySelectorArguments? DirectBindGroupBy_keySelector(DirectFragmentArguments arguments) => GroupBy_keySelectorArguments.FromDirect(arguments);
    private static GroupBy_keySelector_elementSelectorArguments? DirectBindGroupBy_keySelector_elementSelector(DirectFragmentArguments arguments) => GroupBy_keySelector_elementSelectorArguments.FromDirect(arguments);
    private static GroupBy_keySelector_elementSelector_resultSelectorArguments? DirectBindGroupBy_keySelector_elementSelector_resultSelector(DirectFragmentArguments arguments) => GroupBy_keySelector_elementSelector_resultSelectorArguments.FromDirect(arguments);
    private static GroupBy_keySelector_resultSelectorArguments? DirectBindGroupBy_keySelector_resultSelector(DirectFragmentArguments arguments) => GroupBy_keySelector_resultSelectorArguments.FromDirect(arguments);
    private static GroupJoinArguments? DirectBindGroupJoin(DirectFragmentArguments arguments) => GroupJoinArguments.FromDirect(arguments);
    private static IndexArguments? DirectBindIndex(DirectFragmentArguments arguments) => IndexArguments.FromDirect(arguments);
    private static InfiniteSequenceRootArguments? DirectBindInfiniteSequenceRoot(DirectFragmentArguments arguments) => InfiniteSequenceRootArguments.FromDirect(arguments);
    private static IntersectArguments? DirectBindIntersect(DirectFragmentArguments arguments) => IntersectArguments.FromDirect(arguments);
    private static Intersect_comparerArguments? DirectBindIntersect_comparer(DirectFragmentArguments arguments) => Intersect_comparerArguments.FromDirect(arguments);
    private static IntersectByArguments? DirectBindIntersectBy(DirectFragmentArguments arguments) => IntersectByArguments.FromDirect(arguments);
    private static IntersectBy_comparerArguments? DirectBindIntersectBy_comparer(DirectFragmentArguments arguments) => IntersectBy_comparerArguments.FromDirect(arguments);
    private static JoinArguments? DirectBindJoin(DirectFragmentArguments arguments) => JoinArguments.FromDirect(arguments);
    private static LastArguments? DirectBindLast(DirectFragmentArguments arguments) => LastArguments.FromDirect(arguments);
    private static Last_predicateArguments? DirectBindLast_predicate(DirectFragmentArguments arguments) => Last_predicateArguments.FromDirect(arguments);
    private static LastOrDefaultArguments? DirectBindLastOrDefault(DirectFragmentArguments arguments) => LastOrDefaultArguments.FromDirect(arguments);
    private static LastOrDefault_defaultValueArguments? DirectBindLastOrDefault_defaultValue(DirectFragmentArguments arguments) => LastOrDefault_defaultValueArguments.FromDirect(arguments);
    private static LastOrDefault_predicateArguments? DirectBindLastOrDefault_predicate(DirectFragmentArguments arguments) => LastOrDefault_predicateArguments.FromDirect(arguments);
    private static LeftJoinArguments? DirectBindLeftJoin(DirectFragmentArguments arguments) => LeftJoinArguments.FromDirect(arguments);
    private static LongCountArguments? DirectBindLongCount(DirectFragmentArguments arguments) => LongCountArguments.FromDirect(arguments);
    private static LongCount_predicateArguments? DirectBindLongCount_predicate(DirectFragmentArguments arguments) => LongCount_predicateArguments.FromDirect(arguments);
    private static MaxArguments? DirectBindMax(DirectFragmentArguments arguments) => MaxArguments.FromDirect(arguments);
    private static Max_selectorArguments? DirectBindMax_selector(DirectFragmentArguments arguments) => Max_selectorArguments.FromDirect(arguments);
    private static MaxBy_keySelectorArguments? DirectBindMaxBy_keySelector(DirectFragmentArguments arguments) => MaxBy_keySelectorArguments.FromDirect(arguments);
    private static MinArguments? DirectBindMin(DirectFragmentArguments arguments) => MinArguments.FromDirect(arguments);
    private static Min_selectorArguments? DirectBindMin_selector(DirectFragmentArguments arguments) => Min_selectorArguments.FromDirect(arguments);
    private static MinBy_keySelectorArguments? DirectBindMinBy_keySelector(DirectFragmentArguments arguments) => MinBy_keySelectorArguments.FromDirect(arguments);
    private static OfTypeArguments? DirectBindOfType(DirectFragmentArguments arguments) => OfTypeArguments.FromDirect(arguments);
    private static OrderByArguments? DirectBindOrderBy(DirectFragmentArguments arguments) => OrderByArguments.FromDirect(arguments);
    private static OrderBy_comparerArguments? DirectBindOrderBy_comparer(DirectFragmentArguments arguments) => OrderBy_comparerArguments.FromDirect(arguments);
    private static OrderByDescendingArguments? DirectBindOrderByDescending(DirectFragmentArguments arguments) => OrderByDescendingArguments.FromDirect(arguments);
    private static OrderByDescending_comparerArguments? DirectBindOrderByDescending_comparer(DirectFragmentArguments arguments) => OrderByDescending_comparerArguments.FromDirect(arguments);
    private static PrependArguments? DirectBindPrepend(DirectFragmentArguments arguments) => PrependArguments.FromDirect(arguments);
    private static RangeRootArguments? DirectBindRangeRoot(DirectFragmentArguments arguments) => RangeRootArguments.FromDirect(arguments);
    private static RepeatRootArguments? DirectBindRepeatRoot(DirectFragmentArguments arguments) => RepeatRootArguments.FromDirect(arguments);
    private static ReverseArguments? DirectBindReverse(DirectFragmentArguments arguments) => ReverseArguments.FromDirect(arguments);
    private static RightJoinArguments? DirectBindRightJoin(DirectFragmentArguments arguments) => RightJoinArguments.FromDirect(arguments);
    private static Select_indexSelectorArguments? DirectBindSelect_indexSelector(DirectFragmentArguments arguments) => Select_indexSelectorArguments.FromDirect(arguments);
    private static Select_selectorArguments? DirectBindSelect_selector(DirectFragmentArguments arguments) => Select_selectorArguments.FromDirect(arguments);
    private static SelectMany_indexSelectorArguments? DirectBindSelectMany_indexSelector(DirectFragmentArguments arguments) => SelectMany_indexSelectorArguments.FromDirect(arguments);
    private static SelectMany_indexSelector_resultSelectorArguments? DirectBindSelectMany_indexSelector_resultSelector(DirectFragmentArguments arguments) => SelectMany_indexSelector_resultSelectorArguments.FromDirect(arguments);
    private static SelectMany_selectorArguments? DirectBindSelectMany_selector(DirectFragmentArguments arguments) => SelectMany_selectorArguments.FromDirect(arguments);
    private static SelectMany_selector_resultSelectorArguments? DirectBindSelectMany_selector_resultSelector(DirectFragmentArguments arguments) => SelectMany_selector_resultSelectorArguments.FromDirect(arguments);
    private static SelectMany_WithInlinedInnerArguments? DirectBindSelectMany_WithInlinedInner(DirectFragmentArguments arguments) => SelectMany_WithInlinedInnerArguments.FromDirect(arguments);
    private static SequenceEqualArguments? DirectBindSequenceEqual(DirectFragmentArguments arguments) => SequenceEqualArguments.FromDirect(arguments);
    private static SequenceEqual_comparerArguments? DirectBindSequenceEqual_comparer(DirectFragmentArguments arguments) => SequenceEqual_comparerArguments.FromDirect(arguments);
    private static SequenceRootArguments? DirectBindSequenceRoot(DirectFragmentArguments arguments) => SequenceRootArguments.FromDirect(arguments);
    private static ShuffleArguments? DirectBindShuffle(DirectFragmentArguments arguments) => ShuffleArguments.FromDirect(arguments);
    private static SingleArguments? DirectBindSingle(DirectFragmentArguments arguments) => SingleArguments.FromDirect(arguments);
    private static Single_predicateArguments? DirectBindSingle_predicate(DirectFragmentArguments arguments) => Single_predicateArguments.FromDirect(arguments);
    private static SingleOrDefaultArguments? DirectBindSingleOrDefault(DirectFragmentArguments arguments) => SingleOrDefaultArguments.FromDirect(arguments);
    private static SingleOrDefault_defaultValueArguments? DirectBindSingleOrDefault_defaultValue(DirectFragmentArguments arguments) => SingleOrDefault_defaultValueArguments.FromDirect(arguments);
    private static SingleOrDefault_predicateArguments? DirectBindSingleOrDefault_predicate(DirectFragmentArguments arguments) => SingleOrDefault_predicateArguments.FromDirect(arguments);
    private static SkipArguments? DirectBindSkip(DirectFragmentArguments arguments) => SkipArguments.FromDirect(arguments);
    private static SkipLastArguments? DirectBindSkipLast(DirectFragmentArguments arguments) => SkipLastArguments.FromDirect(arguments);
    private static SkipWhile_indexPredicateArguments? DirectBindSkipWhile_indexPredicate(DirectFragmentArguments arguments) => SkipWhile_indexPredicateArguments.FromDirect(arguments);
    private static SkipWhile_predicateArguments? DirectBindSkipWhile_predicate(DirectFragmentArguments arguments) => SkipWhile_predicateArguments.FromDirect(arguments);
    private static SumArguments? DirectBindSum(DirectFragmentArguments arguments) => SumArguments.FromDirect(arguments);
    private static Sum_selectorArguments? DirectBindSum_selector(DirectFragmentArguments arguments) => Sum_selectorArguments.FromDirect(arguments);
    private static TakeArguments? DirectBindTake(DirectFragmentArguments arguments) => TakeArguments.FromDirect(arguments);
    private static TakeLastArguments? DirectBindTakeLast(DirectFragmentArguments arguments) => TakeLastArguments.FromDirect(arguments);
    private static TakeWhile_indexPredicateArguments? DirectBindTakeWhile_indexPredicate(DirectFragmentArguments arguments) => TakeWhile_indexPredicateArguments.FromDirect(arguments);
    private static TakeWhile_predicateArguments? DirectBindTakeWhile_predicate(DirectFragmentArguments arguments) => TakeWhile_predicateArguments.FromDirect(arguments);
    private static ThenByArguments? DirectBindThenBy(DirectFragmentArguments arguments) => ThenByArguments.FromDirect(arguments);
    private static ThenBy_comparerArguments? DirectBindThenBy_comparer(DirectFragmentArguments arguments) => ThenBy_comparerArguments.FromDirect(arguments);
    private static ThenByDescendingArguments? DirectBindThenByDescending(DirectFragmentArguments arguments) => ThenByDescendingArguments.FromDirect(arguments);
    private static ThenByDescending_comparerArguments? DirectBindThenByDescending_comparer(DirectFragmentArguments arguments) => ThenByDescending_comparerArguments.FromDirect(arguments);
    private static ToArrayArguments? DirectBindToArray(DirectFragmentArguments arguments) => ToArrayArguments.FromDirect(arguments);
    private static ToDictionary_keySelectorArguments? DirectBindToDictionary_keySelector(DirectFragmentArguments arguments) => ToDictionary_keySelectorArguments.FromDirect(arguments);
    private static ToDictionary_keySelector_comparerArguments? DirectBindToDictionary_keySelector_comparer(DirectFragmentArguments arguments) => ToDictionary_keySelector_comparerArguments.FromDirect(arguments);
    private static ToDictionary_keySelector_elementSelectorArguments? DirectBindToDictionary_keySelector_elementSelector(DirectFragmentArguments arguments) => ToDictionary_keySelector_elementSelectorArguments.FromDirect(arguments);
    private static ToDictionary_keySelector_elementSelector_comparerArguments? DirectBindToDictionary_keySelector_elementSelector_comparer(DirectFragmentArguments arguments) => ToDictionary_keySelector_elementSelector_comparerArguments.FromDirect(arguments);
    private static ToHashSetArguments? DirectBindToHashSet(DirectFragmentArguments arguments) => ToHashSetArguments.FromDirect(arguments);
    private static ToHashSet_comparerArguments? DirectBindToHashSet_comparer(DirectFragmentArguments arguments) => ToHashSet_comparerArguments.FromDirect(arguments);
    private static ToListArguments? DirectBindToList(DirectFragmentArguments arguments) => ToListArguments.FromDirect(arguments);
    private static ToLookup_keySelectorArguments? DirectBindToLookup_keySelector(DirectFragmentArguments arguments) => ToLookup_keySelectorArguments.FromDirect(arguments);
    private static ToLookup_keySelector_comparerArguments? DirectBindToLookup_keySelector_comparer(DirectFragmentArguments arguments) => ToLookup_keySelector_comparerArguments.FromDirect(arguments);
    private static ToLookup_keySelector_elementSelectorArguments? DirectBindToLookup_keySelector_elementSelector(DirectFragmentArguments arguments) => ToLookup_keySelector_elementSelectorArguments.FromDirect(arguments);
    private static ToLookup_keySelector_elementSelector_comparerArguments? DirectBindToLookup_keySelector_elementSelector_comparer(DirectFragmentArguments arguments) => ToLookup_keySelector_elementSelector_comparerArguments.FromDirect(arguments);
    private static UnionArguments? DirectBindUnion(DirectFragmentArguments arguments) => UnionArguments.FromDirect(arguments);
    private static Union_comparerArguments? DirectBindUnion_comparer(DirectFragmentArguments arguments) => Union_comparerArguments.FromDirect(arguments);
    private static UnionByArguments? DirectBindUnionBy(DirectFragmentArguments arguments) => UnionByArguments.FromDirect(arguments);
    private static UnionBy_comparerArguments? DirectBindUnionBy_comparer(DirectFragmentArguments arguments) => UnionBy_comparerArguments.FromDirect(arguments);
    private static Where_indexPredicateArguments? DirectBindWhere_indexPredicate(DirectFragmentArguments arguments) => Where_indexPredicateArguments.FromDirect(arguments);
    private static Where_predicateArguments? DirectBindWhere_predicate(DirectFragmentArguments arguments) => Where_predicateArguments.FromDirect(arguments);
    private static ZipArguments? DirectBindZip(DirectFragmentArguments arguments) => ZipArguments.FromDirect(arguments);
    private static Zip_resultSelectorArguments? DirectBindZip_resultSelector(DirectFragmentArguments arguments) => Zip_resultSelectorArguments.FromDirect(arguments);
    private static Zip3Arguments? DirectBindZip3(DirectFragmentArguments arguments) => Zip3Arguments.FromDirect(arguments);
}
