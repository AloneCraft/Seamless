using Benchmark.Dynamic.Core;

namespace Benchmark.Dynamic.Worker;

/// <summary>
/// 全組み合わせの生成モード(<see cref="QueryGenerationMode.Exhaustive"/>)で、QueryIndex 番目のクエリを返す。
/// 全組み合わせを番号順にたどり、静的に成り立たない並びは飛ばして、成り立つ並びの QueryIndex 番目を使う。
/// 同じ実行の中で QueryIndex が増える順に呼ぶのが基本で、再開時のように先の番号を求められたら、そこまで数え直す。
/// </summary>
internal sealed class ExhaustiveQueryProvider{
    private readonly List<QueryIrDocument> feasible=[];
    private readonly IEnumerator<ExhaustiveQuerySpec> specs;
    private readonly Seed256 baseSeed;
    private readonly SourceTarget target;
    private readonly int inputElementCount;
    private readonly bool expectExceptions;
    private readonly QueryElementType elementType;

    public ExhaustiveQueryProvider(WorkerRunRequest request,QueryCapabilitySet capability){
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capability);
        this.baseSeed=request.BaseSeed;
        this.target=request.SourceTargets.Order().First();
        this.inputElementCount=request.InputElementCount;
        this.expectExceptions=request.ExpectExceptions;
        this.elementType=request.ElementType;
        var intermediates=capability.OperatorIds
            .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind is QueryNodeKind.Unary or QueryNodeKind.Binary or QueryNodeKind.Nested)
            .ToArray();
        var terminals=request.ExhaustiveTerminalIds is { Count:>0 }ids
            ?ids.ToArray()
            :capability.OperatorIds
                .Where(static id=>BuiltInOperatorCatalog.Get(id).NodeKind==QueryNodeKind.Terminal)
                .ToArray();
        this.specs=ExhaustiveQueryEnumerator.Enumerate(request.ExhaustiveIntermediateCount,intermediates,terminals).GetEnumerator();
    }

    public QueryIrDocument Get(int queryIndex){
        ArgumentOutOfRangeException.ThrowIfNegative(queryIndex);
        while(this.feasible.Count<=queryIndex){
            if(!this.specs.MoveNext()){
                throw new InvalidOperationException(
                    $"The exhaustive enumeration has only {this.feasible.Count} feasible queries; QueryIndex {queryIndex} was requested.");
            }
            if(ExhaustiveQueryEnumerator.TryGenerate(this.specs.Current,this.baseSeed,this.target,this.inputElementCount,out var document,this.expectExceptions,this.elementType)){
                this.feasible.Add(document!);
            }
        }
        return this.feasible[queryIndex];
    }
}
