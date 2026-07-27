namespace Shared.Linq.Optimization.Rewrite;
public abstract class Array<T>(int count = 4_096):global::Common.Base<T>(count)where T : notnull {
    [BenchFact, Benchmark]
    public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)]
    private void Where_predicate_Count_Impl() =>
        this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
    // Same AlgebraicRewriter branch. [BenchFact,Benchmark]
    public void Where_predicate_Any() =>
        this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Any());
}
//public abstract class ArrayRewrite<T>(int count = 4_096):global::Common.Base<T>(count) where T : notnull {
//    [BenchFact, Benchmark] public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
//    [LinqIntercept(true,Optimizations = OptimizationSettings.Value)] private void Where_predicate_Count_Impl() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
//    // Same AlgebraicRewriter branch. [BenchFact,Benchmark]
//    public void Where_predicate_Any() => this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Any());
//}
//public static class Array<T>where T : notnull {
//    public abstract class Rewrite(int count = 4_096):global::Common.Base<T>(count){
//        [BenchFact, Benchmark]
//        public void Where_predicate_Count() => this.Where_predicate_Count_Impl();
//        [LinqIntercept(true,Optimizations = OptimizationSettings.Value)]
//        private void Where_predicate_Count_Impl() =>
//            this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
//        // Same AlgebraicRewriter branch. [BenchFact,Benchmark]
//        public void Where_predicate_Any() =>
//            this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Any());
//    }
//}
//public static class Array {
//    public abstract class ArrayRewrite<T>(int count=4_096):global::Common.Base<T>(count) where T : notnull {
//        [BenchFact,Benchmark]
//        public void Where_predicate_Count1() => this.Where_predicate_Count_Impl();
//        [LinqIntercept(true,Optimizations=OptimizationSettings.Value)]
//        private void Where_predicate_Count_Impl() =>
//            this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Count());
//        // Same AlgebraicRewriter branch. [BenchFact,Benchmark]
//        public void Where_predicate_Any() =>
//            this.consumer.Consume(this.srcCount(4_096).Where(static value => ToInt(value)>2_048).Any());
//    }
//}
