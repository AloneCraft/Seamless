using System.Linq;
using SystemEnumerable = System.Linq.Enumerable;

namespace Test.Seamless.Intercept.System.Linq;

public sealed class Enumerable3 {
    private readonly int instanceOffset = 10;

    [Fact, Trait("Category","Unit")]
    public void S000_Aggregate_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M000_Aggregate_func(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I000_Aggregate_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M000_Aggregate_func(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S001_Aggregate_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M001_Aggregate_func(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I001_Aggregate_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M001_Aggregate_func(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S002_Aggregate_seed_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M002_Aggregate_seed_func(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I002_Aggregate_seed_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M002_Aggregate_seed_func(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S003_Aggregate_seed_func_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M003_Aggregate_seed_func_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I003_Aggregate_seed_func_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M003_Aggregate_seed_func_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S004_AggregateBy_keySelector_seed_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M004_AggregateBy_keySelector_seed_func(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I004_AggregateBy_keySelector_seed_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M004_AggregateBy_keySelector_seed_func(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S005_AggregateBy_keySelector_seedSelector_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M005_AggregateBy_keySelector_seedSelector_func(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I005_AggregateBy_keySelector_seedSelector_func() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M005_AggregateBy_keySelector_seedSelector_func(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S006_All_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M006_All_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I006_All_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M006_All_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S007_Any() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M007_Any(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I007_Any() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M007_Any(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S008_Any_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M008_Any_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I008_Any_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M008_Any_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S009_Append_element() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M009_Append_element(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I009_Append_element() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M009_Append_element(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S010_AsEnumerable() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M010_AsEnumerable(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I010_AsEnumerable() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M010_AsEnumerable(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S011_Average_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M011_Average_int(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I011_Average_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M011_Average_int(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S012_Average_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M012_Average_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I012_Average_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M012_Average_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S013_Cast() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M013_Cast(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I013_Cast() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M013_Cast(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S014_Chunk_size() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M014_Chunk_size(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I014_Chunk_size() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M014_Chunk_size(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S015_Concat_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M015_Concat_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I015_Concat_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M015_Concat_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S016_Contains_value() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M016_Contains_value(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I016_Contains_value() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M016_Contains_value(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S017_Contains_value_comparer() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M017_Contains_value_comparer(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I017_Contains_value_comparer() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M017_Contains_value_comparer(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S018_Count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M018_Count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I018_Count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M018_Count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S019_Count_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M019_Count_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I019_Count_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M019_Count_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S020_CountBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M020_CountBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I020_CountBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M020_CountBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S021_DefaultIfEmpty() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M021_DefaultIfEmpty(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I021_DefaultIfEmpty() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M021_DefaultIfEmpty(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S022_DefaultIfEmpty_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M022_DefaultIfEmpty_defaultValue(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I022_DefaultIfEmpty_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M022_DefaultIfEmpty_defaultValue(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S023_Distinct() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M023_Distinct(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I023_Distinct() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M023_Distinct(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S024_Distinct_comparer() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M024_Distinct_comparer(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I024_Distinct_comparer() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M024_Distinct_comparer(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S025_DistinctBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M025_DistinctBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I025_DistinctBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M025_DistinctBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S026_ElementAt_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M026_ElementAt_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I026_ElementAt_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M026_ElementAt_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S027_ElementAtOrDefault_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M027_ElementAtOrDefault_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I027_ElementAtOrDefault_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M027_ElementAtOrDefault_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S028_Empty() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M028_Empty(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I028_Empty() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M028_Empty(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S029_Except_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M029_Except_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I029_Except_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M029_Except_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S030_ExceptBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M030_ExceptBy_second_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I030_ExceptBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M030_ExceptBy_second_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S031_First() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M031_First(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I031_First() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M031_First(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S032_First_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M032_First_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I032_First_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M032_First_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S033_FirstOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M033_FirstOrDefault(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I033_FirstOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M033_FirstOrDefault(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S034_FirstOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M034_FirstOrDefault_predicate_defaultValue(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I034_FirstOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M034_FirstOrDefault_predicate_defaultValue(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S035_GroupBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M035_GroupBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I035_GroupBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M035_GroupBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S036_GroupBy_keySelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M036_GroupBy_keySelector_Query(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I036_GroupBy_keySelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M036_GroupBy_keySelector_Query(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S037_GroupJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M037_GroupJoin_keySelectors_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I037_GroupJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M037_GroupJoin_keySelectors_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S038_Index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M038_Index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I038_Index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M038_Index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S039_Intersect_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M039_Intersect_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I039_Intersect_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M039_Intersect_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S040_IntersectBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M040_IntersectBy_second_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I040_IntersectBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M040_IntersectBy_second_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S041_Join_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M041_Join_keySelectors_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I041_Join_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M041_Join_keySelectors_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S042_Join_keySelectors_resultSelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M042_Join_keySelectors_resultSelector_Query(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I042_Join_keySelectors_resultSelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M042_Join_keySelectors_resultSelector_Query(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S043_Last() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M043_Last(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I043_Last() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M043_Last(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S044_Last_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M044_Last_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I044_Last_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M044_Last_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S045_LastOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M045_LastOrDefault(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I045_LastOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M045_LastOrDefault(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S046_LastOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M046_LastOrDefault_predicate_defaultValue(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I046_LastOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M046_LastOrDefault_predicate_defaultValue(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S047_LeftJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M047_LeftJoin_keySelectors_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I047_LeftJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M047_LeftJoin_keySelectors_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S048_LongCount() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M048_LongCount(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I048_LongCount() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M048_LongCount(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S049_LongCount_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M049_LongCount_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I049_LongCount_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M049_LongCount_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S050_Max_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M050_Max_int(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I050_Max_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M050_Max_int(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S051_Max_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M051_Max_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I051_Max_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M051_Max_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S052_MaxBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M052_MaxBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I052_MaxBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M052_MaxBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S053_Min_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M053_Min_int(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I053_Min_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M053_Min_int(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S054_Min_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M054_Min_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I054_Min_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M054_Min_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S055_MinBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M055_MinBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I055_MinBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M055_MinBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S056_OfType() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M056_OfType(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I056_OfType() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M056_OfType(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S057_Order() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M057_Order(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I057_Order() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M057_Order(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S058_OrderBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M058_OrderBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I058_OrderBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M058_OrderBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S059_OrderByDescending_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M059_OrderByDescending_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I059_OrderByDescending_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M059_OrderByDescending_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S060_OrderDescending() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M060_OrderDescending(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I060_OrderDescending() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M060_OrderDescending(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S061_Prepend_element() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M061_Prepend_element(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I061_Prepend_element() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M061_Prepend_element(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S062_Range_start_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M062_Range_start_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I062_Range_start_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M062_Range_start_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S063_Repeat_element_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M063_Repeat_element_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I063_Repeat_element_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M063_Repeat_element_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S064_Reverse() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M064_Reverse(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I064_Reverse() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M064_Reverse(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S065_RightJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M065_RightJoin_keySelectors_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I065_RightJoin_keySelectors_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M065_RightJoin_keySelectors_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S066_Select_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M066_Select_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I066_Select_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M066_Select_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S067_Select_selector_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M067_Select_selector_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I067_Select_selector_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M067_Select_selector_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S068_Select_selector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M068_Select_selector_Query(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I068_Select_selector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M068_Select_selector_Query(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S069_SelectMany_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M069_SelectMany_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I069_SelectMany_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M069_SelectMany_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S070_SelectMany_selector_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M070_SelectMany_selector_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I070_SelectMany_selector_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M070_SelectMany_selector_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S071_SelectMany_collectionSelector_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M071_SelectMany_collectionSelector_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I071_SelectMany_collectionSelector_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M071_SelectMany_collectionSelector_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S072_SelectMany_collectionSelector_resultSelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M072_SelectMany_collectionSelector_resultSelector_Query(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I072_SelectMany_collectionSelector_resultSelector_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M072_SelectMany_collectionSelector_resultSelector_Query(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S073_SequenceEqual_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M073_SequenceEqual_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I073_SequenceEqual_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M073_SequenceEqual_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S074_Single() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M074_Single(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I074_Single() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M074_Single(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S075_Single_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M075_Single_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I075_Single_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M075_Single_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S076_SingleOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M076_SingleOrDefault(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I076_SingleOrDefault() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M076_SingleOrDefault(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S077_SingleOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M077_SingleOrDefault_predicate_defaultValue(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I077_SingleOrDefault_predicate_defaultValue() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M077_SingleOrDefault_predicate_defaultValue(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S078_Skip_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M078_Skip_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I078_Skip_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M078_Skip_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S079_SkipLast_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M079_SkipLast_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I079_SkipLast_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M079_SkipLast_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S080_SkipWhile_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M080_SkipWhile_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I080_SkipWhile_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M080_SkipWhile_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S081_SkipWhile_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M081_SkipWhile_predicate_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I081_SkipWhile_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M081_SkipWhile_predicate_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S082_Sum_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M082_Sum_int(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I082_Sum_int() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M082_Sum_int(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S083_Sum_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M083_Sum_selector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I083_Sum_selector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M083_Sum_selector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S084_Take_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M084_Take_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I084_Take_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M084_Take_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S085_Take_range() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M085_Take_range(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I085_Take_range() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M085_Take_range(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S086_TakeLast_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M086_TakeLast_count(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I086_TakeLast_count() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M086_TakeLast_count(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S087_TakeWhile_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M087_TakeWhile_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I087_TakeWhile_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M087_TakeWhile_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S088_TakeWhile_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M088_TakeWhile_predicate_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I088_TakeWhile_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M088_TakeWhile_predicate_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S089_ThenBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M089_ThenBy_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I089_ThenBy_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M089_ThenBy_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S090_ThenByDescending_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M090_ThenByDescending_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I090_ThenByDescending_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M090_ThenByDescending_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S091_ToArray() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M091_ToArray(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I091_ToArray() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M091_ToArray(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S092_ToDictionary_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M092_ToDictionary_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I092_ToDictionary_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M092_ToDictionary_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S093_ToHashSet() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M093_ToHashSet(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I093_ToHashSet() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M093_ToHashSet(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S094_ToList() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M094_ToList(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I094_ToList() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M094_ToList(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S095_ToLookup_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M095_ToLookup_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I095_ToLookup_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M095_ToLookup_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S096_TryGetNonEnumeratedCount_source() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M096_TryGetNonEnumeratedCount_source(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I096_TryGetNonEnumeratedCount_source() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M096_TryGetNonEnumeratedCount_source(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S097_Union_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M097_Union_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I097_Union_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M097_Union_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S098_UnionBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M098_UnionBy_second_keySelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I098_UnionBy_second_keySelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M098_UnionBy_second_keySelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S099_Where_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M099_Where_predicate(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I099_Where_predicate() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M099_Where_predicate(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S100_Where_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M100_Where_predicate_index(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I100_Where_predicate_index() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M100_Where_predicate_index(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S101_Where_predicate_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M101_Where_predicate_Query(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I101_Where_predicate_Query() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M101_Where_predicate_Query(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S102_Zip_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M102_Zip_second(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I102_Zip_second() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M102_Zip_second(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S103_Zip_second_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M103_Zip_second_resultSelector(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I103_Zip_second_resultSelector() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M103_Zip_second_resultSelector(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void S104_Zip_second_third() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M104_Zip_second_third(EnumerableChainHelpers.Source_M066_Select_selector(EnumerableChainHelpers.Source_M099_Where_predicate(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void I104_Zip_second_third() {
        EnumerableChainHelpers.Consume(EnumerableChainHelpers.Outer_M104_Zip_second_third(Source_Select_Instance(Source_Where_Instance(EnumerableChainHelpers.Source()))));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Static_Static_Static() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,static outerValue => outerValue,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Static_Static_Instance() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,static outerValue => outerValue,static innerValue => innerValue,this.InstanceResultSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Static_Instance_Static() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,static outerValue => outerValue,this.InstanceKeySelector,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Static_Instance_Instance() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,static outerValue => outerValue,this.InstanceKeySelector,this.InstanceResultSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Instance_Static_Static() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,this.InstanceKeySelector,static innerValue => innerValue,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Instance_Static_Instance() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,this.InstanceKeySelector,static innerValue => innerValue,this.InstanceResultSelector));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Instance_Instance_Static() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,this.InstanceKeySelector,this.InstanceKeySelector,static (outerValue,innerValue) => outerValue+innerValue));
    }

    [Fact, Trait("Category","Unit")]
    public void JoinDelegateVariant_Instance_Instance_Instance() {
        var outer = EnumerableChainHelpers.Source();
        var inner = EnumerableChainHelpers.Second();
        EnumerableChainHelpers.Consume(SystemEnumerable.Join(outer,inner,this.InstanceKeySelector,this.InstanceKeySelector,this.InstanceResultSelector));
    }

    private IEnumerable<int> Source_Select_Instance(IEnumerable<int> source) => SystemEnumerable.Select(source,this.InstanceSelector);
    private IEnumerable<int> Source_Where_Instance(IEnumerable<int> source) => SystemEnumerable.Where(source,this.InstancePredicate);
    private bool InstancePredicate(int value) => value>this.instanceOffset-8;
    private int InstanceSelector(int value) => value*this.instanceOffset;
    private int InstanceKeySelector(int value) => value;
    private int InstanceResultSelector(int outerValue,int innerValue) => outerValue+innerValue+this.instanceOffset-10;
}
