namespace Benchmark.Dynamic.Core;

public sealed record RandomQueryGenerationOptions{
    public required Seed256 MasterSeed{get;init;}
    public required int QueryIndex{get;init;}
    public string? QueryId{get;init;}
    public required SourceTarget SourceTarget{get;init;}
    public required int OperatorCount{get;init;}
    public required int MaximumDepth{get;init;}
    public int InputElementCount{get;init;}=1_024;
    public long MaximumCardinality{get;init;}=100_000;
    public bool EnableBinaryOperators{get;init;}=true;
    public bool EnableNestedOperators{get;init;}
    public IReadOnlyCollection<string>? AllowedOperatorIds{get;init;}
}

public sealed class RandomQueryGenerator{
    public const string GeneratorVersion="random-query-v6";

    public QueryIrDocument Generate(RandomQueryGenerationOptions options){
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        var querySeed=SeedDerivation.Derive(options.MasterSeed,"query",options.QueryIndex);
        var context=new GenerationContext(options,querySeed);
        var sequenceOperatorCount=options.OperatorCount-1;
        var sequence=context.BuildSequence(sequenceOperatorCount,options.MaximumDepth-1,SourceSlot.Primary);
        var root=context.BuildTerminal(sequence);
        var queryId=options.QueryId;
        if(string.IsNullOrWhiteSpace(queryId)){
            queryId=$"query-{options.QueryIndex:D6}";
        }
        var document=new QueryIrDocument(
            QueryIrDocument.CurrentSchemaVersion,
            queryId,
            querySeed,
            options.OperatorCount,
            options.MaximumDepth,
            options.SourceTarget,
            root);
        QueryIrValidator.ValidateAndThrow(document);
        return document;
    }

    private static void ValidateOptions(RandomQueryGenerationOptions options){
        if(options.QueryIndex<0){
            throw new ArgumentOutOfRangeException(nameof(options.QueryIndex));
        }
        if(options.OperatorCount<1){
            throw new ArgumentOutOfRangeException(nameof(options.OperatorCount));
        }
        if(options.MaximumDepth<1){
            throw new ArgumentOutOfRangeException(nameof(options.MaximumDepth));
        }
        if(options.InputElementCount<1||options.InputElementCount>100_000){
            throw new ArgumentOutOfRangeException(nameof(options.InputElementCount));
        }
        if(options.MaximumCardinality<1||options.MaximumCardinality>100_000){
            throw new ArgumentOutOfRangeException(nameof(options.MaximumCardinality));
        }
    }

    private sealed class GenerationContext{
        private readonly RandomQueryGenerationOptions options;
        private readonly HashSet<string> allowed;
        private readonly DeterministicPrng shapeRandom;
        private readonly DeterministicPrng operatorRandom;
        private readonly DeterministicPrng argumentRandom;
        private readonly DeterministicPrng terminalRandom;
        private int nextNodeId;
        private readonly bool binaryAvailable;
        private readonly bool nestedAvailable;

        public GenerationContext(RandomQueryGenerationOptions options,Seed256 querySeed){
            this.options=options;
            this.allowed=options.AllowedOperatorIds is null
                ?QueryCapabilityProfiles.CreateSet(OperatorSetMode.Maximum,[options.SourceTarget]).OperatorIds.ToHashSet(StringComparer.Ordinal)
                :options.AllowedOperatorIds.ToHashSet(StringComparer.Ordinal);
            this.shapeRandom=new(SeedDerivation.Derive(querySeed,"shape"));
            this.operatorRandom=new(SeedDerivation.Derive(querySeed,"operator"));
            this.argumentRandom=new(SeedDerivation.Derive(querySeed,"argument"));
            this.terminalRandom=new(SeedDerivation.Derive(querySeed,"terminal"));
            var worstUnionCardinality=QueryCardinalityRange.SaturatingMultiply(options.InputElementCount,options.OperatorCount+1L);
            this.binaryAvailable=options.EnableBinaryOperators
                &&(this.allowed.Contains(QueryOperatorIds.Union)
                    ||this.allowed.Contains(QueryOperatorIds.Concat)
                    ||this.allowed.Contains(QueryOperatorIds.Except)
                    ||this.allowed.Contains(QueryOperatorIds.Intersect)
                    ||this.allowed.Contains(QueryOperatorIds.UnionByIdentity)
                    ||this.allowed.Contains(QueryOperatorIds.ExceptByIdentity)
                    ||this.allowed.Contains(QueryOperatorIds.IntersectByIdentity))
                &&worstUnionCardinality<=options.MaximumCardinality;
            var oneNestedCardinality=QueryCardinalityRange.SaturatingMultiply(
                options.InputElementCount,
                options.InputElementCount);
            var worstNestedForestCardinality=QueryCardinalityRange.SaturatingMultiply(
                oneNestedCardinality,
                options.OperatorCount+1L);
            this.nestedAvailable=options.EnableNestedOperators
                &&(this.allowed.Contains(QueryOperatorIds.SelectManyXor)
                    ||this.allowed.Contains(QueryOperatorIds.JoinXor)
                    ||this.allowed.Contains(QueryOperatorIds.GroupJoinXorCount))
                &&worstNestedForestCardinality<=options.MaximumCardinality;
            var maximumNodes=GetMaximumSequenceNodes(
                options.MaximumDepth-1,
                this.binaryAvailable,
                this.nestedAvailable);
            if(options.OperatorCount-1>maximumNodes){
                throw new ArgumentException(
                    $"Cannot place exactly {options.OperatorCount} operators within depth {options.MaximumDepth} using the enabled operator arities.",
                    nameof(options));
            }
        }

        public QueryNode BuildSequence(
            int operatorCount,
            int remainingDepth,
            SourceSlot preferredSlot,
            bool allowBranching=true){
            if(operatorCount==0){
                return this.CreateSource(preferredSlot);
            }
            if(remainingDepth<1){
                throw new InvalidOperationException("The exact operator allocation exceeded the depth budget.");
            }
            var childCapacity=GetMaximumSequenceNodes(
                remainingDepth-1,
                allowBranching&&this.binaryAvailable,
                allowBranching&&this.nestedAvailable);
            var shapes=new List<QueryNodeKind>();
            if(operatorCount-1<=childCapacity&&this.HasUnaryOperator()){
                shapes.Add(QueryNodeKind.Unary);
            }
            if(allowBranching&&this.binaryAvailable&&CanSplit(operatorCount-1,childCapacity)){
                shapes.Add(QueryNodeKind.Binary);
            }
            var linearChildCapacity=Math.Max(0,remainingDepth-1);
            if(allowBranching&&this.nestedAvailable&&CanSplit(operatorCount-1,linearChildCapacity)){
                shapes.Add(QueryNodeKind.Nested);
            }
            if(shapes.Count==0){
                throw new InvalidOperationException($"No exact operator shape is available for count {operatorCount} and depth {remainingDepth}.");
            }
            var shape=this.shapeRandom.Choose(shapes);
            return shape switch{
                QueryNodeKind.Unary=>this.BuildUnary(operatorCount,remainingDepth,preferredSlot,allowBranching),
                QueryNodeKind.Binary=>this.BuildBinary(operatorCount,remainingDepth,preferredSlot),
                QueryNodeKind.Nested=>this.BuildNested(operatorCount,remainingDepth,preferredSlot),
                _=>throw new InvalidOperationException($"Unsupported generated shape: {shape}."),
            };
        }

        public TerminalQueryNode BuildTerminal(QueryNode input){
            var descriptors=BuiltInOperatorCatalog.Initial
                .Where(static descriptor=>descriptor.NodeKind==QueryNodeKind.Terminal)
                .Where(descriptor=>this.allowed.Contains(descriptor.OperatorId))
                .Where(descriptor=>!descriptor.RequiresNonEmpty||input.Cardinality.IsDefinitelyNonEmpty)
                .Where(descriptor=>!descriptor.RequiresDeterministicOrder||input.OrderSemantics!=QueryOrderSemantics.Unordered)
                .Where(descriptor=>!descriptor.RequiresUniqueInput||QuerySequenceSemantics.HasUniqueValues(input))
                .OrderBy(static descriptor=>descriptor.OperatorId,StringComparer.Ordinal)
                .ToArray();
            if(descriptors.Length==0){
                throw new InvalidOperationException("No safe terminal operator is available.");
            }
            var descriptor=this.terminalRandom.Choose(descriptors);
            var terminalKind=GetTerminalKind(descriptor.OperatorId);
            QueryPredicate? predicate=null;
            int? value=null;
            if(terminalKind==QueryTerminalKind.All){
                predicate=this.CreatePredicate();
            }
            if(terminalKind==QueryTerminalKind.Contains){
                value=this.argumentRandom.NextInt32(-32,33);
            }
            if(terminalKind==QueryTerminalKind.ElementAt){
                var upper=(int)Math.Min(input.Cardinality.Minimum,32);
                value=this.argumentRandom.NextInt32(upper);
            }
            if(terminalKind==QueryTerminalKind.ElementAtOrDefault){
                var upper=(int)Math.Min(Math.Max(input.Cardinality.Maximum??32,1),32);
                value=this.argumentRandom.NextInt32(upper);
            }
            var shape=QueryIrValidator.GetTerminalShape(terminalKind);
            var cardinality=shape.Cardinality==QueryCardinality.Scalar
                ?QueryCardinalityRange.Exact(1)
                :terminalKind==QueryTerminalKind.ToLookup
                    ?new QueryCardinalityRange(input.Cardinality.Minimum>0?1:0,Math.Min(input.Cardinality.Maximum??4,4))
                    :terminalKind==QueryTerminalKind.ToHashSet
                        ?new QueryCardinalityRange(input.Cardinality.Minimum>0?1:0,input.Cardinality.Maximum)
                    :input.Cardinality;
            var order=shape.Cardinality==QueryCardinality.Scalar
                ?QueryOrderSemantics.TotalOrdered
                :terminalKind==QueryTerminalKind.ToHashSet
                    ?QueryOrderSemantics.Unordered
                    :terminalKind is QueryTerminalKind.ToDictionary or QueryTerminalKind.ToLookup
                        ?QueryOrderSemantics.TotalOrdered
                        :input.OrderSemantics;
            var emptySafety=descriptor.RequiresNonEmpty?QueryEmptySafety.RequiresNonEmpty:QueryEmptySafety.Safe;
            return new(
                this.CreateNodeId(),
                descriptor.OperatorId,
                input,
                new(terminalKind,predicate,value),
                shape.Type,
                shape.Cardinality,
                cardinality,
                order,
                emptySafety);
        }

        private QueryNode BuildUnary(
            int operatorCount,
            int remainingDepth,
            SourceSlot preferredSlot,
            bool allowBranching){
            var input=this.BuildSequence(operatorCount-1,remainingDepth-1,preferredSlot,allowBranching);
            var descriptors=BuiltInOperatorCatalog.Initial
                .Where(static descriptor=>descriptor.NodeKind==QueryNodeKind.Unary)
                .Where(descriptor=>this.allowed.Contains(descriptor.OperatorId))
                .Where(descriptor=>!descriptor.RequiresDeterministicOrder||input.OrderSemantics!=QueryOrderSemantics.Unordered)
                .Where(descriptor=>descriptor.OperatorId!=QueryOperatorIds.ThenBy||input.OrderSemantics==QueryOrderSemantics.TotalOrdered)
                .OrderBy(static descriptor=>descriptor.OperatorId,StringComparer.Ordinal)
                .ToArray();
            if(descriptors.Length==0){
                throw new InvalidOperationException("No unary operator is valid for the generated input.");
            }
            var descriptor=this.operatorRandom.Choose(descriptors);
            return descriptor.OperatorId switch{
                QueryOperatorIds.Where=>this.CreateWhere(input),
                QueryOperatorIds.WhereIndexed=>this.CreateWhereIndexed(input),
                QueryOperatorIds.Select=>this.CreateSelect(input),
                QueryOperatorIds.SelectIndexed=>this.CreateSelectIndexed(input),
                QueryOperatorIds.Distinct=>this.CreateDistinct(input),
                QueryOperatorIds.DistinctBy=>this.CreateDistinctBy(input),
                QueryOperatorIds.Take=>this.CreateTake(input),
                QueryOperatorIds.Skip=>this.CreateSkip(input),
                QueryOperatorIds.Reverse=>this.CreateReverse(input),
                QueryOperatorIds.OrderBy=>this.CreateOrderBy(input),
                QueryOperatorIds.ThenBy=>this.CreateThenBy(input),
                _=>throw new InvalidOperationException($"Unsupported unary operator: {descriptor.OperatorId}."),
            };
        }

        private QueryNode BuildBinary(int operatorCount,int remainingDepth,SourceSlot preferredSlot){
            var childCapacity=GetMaximumSequenceNodes(
                remainingDepth-1,
                this.binaryAvailable,
                this.nestedAvailable);
            var (leftCount,rightCount)=this.Split(operatorCount-1,childCapacity);
            var left=this.BuildSequence(leftCount,remainingDepth-1,preferredSlot);
            var right=this.BuildSequence(rightCount,remainingDepth-1,Opposite(preferredSlot));
            var descriptor=this.operatorRandom.Choose(BuiltInOperatorCatalog.Initial
                .Where(static item=>item.NodeKind==QueryNodeKind.Binary)
                .Where(item=>this.allowed.Contains(item.OperatorId))
                .OrderBy(static item=>item.OperatorId,StringComparer.Ordinal)
                .ToArray());
            var sum=QueryCardinalityRange.Add(left.Cardinality,right.Cardinality);
            var kind=descriptor.OperatorId switch{
                QueryOperatorIds.Concat=>QueryBinaryKind.Concat,
                QueryOperatorIds.Union=>QueryBinaryKind.Union,
                QueryOperatorIds.Except=>QueryBinaryKind.Except,
                QueryOperatorIds.Intersect=>QueryBinaryKind.Intersect,
                QueryOperatorIds.UnionByIdentity=>QueryBinaryKind.UnionByIdentity,
                QueryOperatorIds.ExceptByIdentity=>QueryBinaryKind.ExceptByIdentity,
                QueryOperatorIds.IntersectByIdentity=>QueryBinaryKind.IntersectByIdentity,
                _=>throw new InvalidOperationException($"Unsupported binary operator: {descriptor.OperatorId}."),
            };
            var cardinality=kind switch{
                QueryBinaryKind.Concat=>sum,
                QueryBinaryKind.Union=>new QueryCardinalityRange(0,sum.Maximum),
                QueryBinaryKind.Except=>new QueryCardinalityRange(0,left.Cardinality.Maximum),
                QueryBinaryKind.Intersect=>new QueryCardinalityRange(
                    0,
                    left.Cardinality.Maximum is { }leftMaximum&&right.Cardinality.Maximum is { }rightMaximum
                        ?Math.Min(leftMaximum,rightMaximum)
                        :left.Cardinality.Maximum??right.Cardinality.Maximum),
                QueryBinaryKind.UnionByIdentity=>new QueryCardinalityRange(0,sum.Maximum),
                QueryBinaryKind.ExceptByIdentity=>new QueryCardinalityRange(0,left.Cardinality.Maximum),
                QueryBinaryKind.IntersectByIdentity=>new QueryCardinalityRange(
                    0,
                    left.Cardinality.Maximum is { }leftMaximum&&right.Cardinality.Maximum is { }rightMaximum
                        ?Math.Min(leftMaximum,rightMaximum)
                        :left.Cardinality.Maximum??right.Cardinality.Maximum),
                _=>throw new InvalidOperationException($"Unsupported binary kind: {kind}."),
            };
            var order=kind==QueryBinaryKind.Concat
                &&left.OrderSemantics!=QueryOrderSemantics.Unordered
                &&right.OrderSemantics!=QueryOrderSemantics.Unordered
                    ?QueryOrderSemantics.Ordered
                    :QueryOrderSemantics.Unordered;
            return new BinaryQueryNode(
                this.CreateNodeId(),
                descriptor.OperatorId,
                left,
                right,
                new(kind),
                QueryValueType.Int,
                cardinality,
                order);
        }

        private QueryNode BuildNested(int operatorCount,int remainingDepth,SourceSlot preferredSlot){
            var childCapacity=Math.Max(0,remainingDepth-1);
            var (outerCount,innerCount)=this.Split(operatorCount-1,childCapacity);
            var outer=this.BuildSequence(outerCount,remainingDepth-1,preferredSlot,allowBranching:false);
            var inner=this.BuildSequence(innerCount,remainingDepth-1,Opposite(preferredSlot),allowBranching:false);
            var descriptor=this.operatorRandom.Choose(BuiltInOperatorCatalog.Initial
                .Where(static item=>item.NodeKind==QueryNodeKind.Nested)
                .Where(item=>this.allowed.Contains(item.OperatorId))
                .OrderBy(static item=>item.OperatorId,StringComparer.Ordinal)
                .ToArray());
            var kind=descriptor.OperatorId switch{
                QueryOperatorIds.SelectManyXor=>QueryNestedKind.SelectManyXor,
                QueryOperatorIds.JoinXor=>QueryNestedKind.JoinXor,
                QueryOperatorIds.GroupJoinXorCount=>QueryNestedKind.GroupJoinXorCount,
                _=>throw new InvalidOperationException($"Unsupported nested operator: {descriptor.OperatorId}."),
            };
            var product=QueryCardinalityRange.Multiply(outer.Cardinality,inner.Cardinality);
            var cardinality=kind switch{
                QueryNestedKind.JoinXor=>new QueryCardinalityRange(0,product.Maximum),
                QueryNestedKind.SelectManyXor=>product,
                QueryNestedKind.GroupJoinXorCount=>outer.Cardinality,
                _=>throw new InvalidOperationException($"Unsupported nested kind: {kind}."),
            };
            var order=outer.OrderSemantics==QueryOrderSemantics.Unordered||inner.OrderSemantics==QueryOrderSemantics.Unordered
                ?QueryOrderSemantics.Unordered
                :QueryOrderSemantics.Ordered;
            return new NestedQueryNode(
                this.CreateNodeId(),
                descriptor.OperatorId,
                outer,
                inner,
                new(kind),
                QueryValueType.Int,
                cardinality,
                order);
        }

        private UnaryQueryNode CreateWhere(QueryNode input){
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Where,
                input,
                new WhereOperatorArguments(this.CreatePredicate()),
                QueryValueType.Int,
                new(0,input.Cardinality.Maximum),
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateWhereIndexed(QueryNode input){
            var modulus=this.argumentRandom.NextInt32(2,9);
            var remainder=this.argumentRandom.NextInt32(modulus);
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.WhereIndexed,
                input,
                new WhereIndexedOperatorArguments(modulus,remainder),
                QueryValueType.Int,
                new(0,input.Cardinality.Maximum),
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateSelect(QueryNode input){
            QuerySelectorKind[] kinds=[
                QuerySelectorKind.Identity,
                QuerySelectorKind.Add,
                QuerySelectorKind.Negate,
            ];
            var kind=this.argumentRandom.Choose(kinds);
            var operand=kind switch{
                QuerySelectorKind.Add=>this.argumentRandom.NextInt32(-8,9),
                QuerySelectorKind.Multiply=>this.argumentRandom.NextInt32(1,5),
                _=>0,
            };
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Select,
                input,
                new SelectOperatorArguments(new(kind,operand)),
                QueryValueType.Int,
                input.Cardinality,
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateSelectIndexed(QueryNode input){
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.SelectIndexed,
                input,
                new SelectIndexedOperatorArguments(),
                QueryValueType.Int,
                input.Cardinality,
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateDistinct(QueryNode input){
            var minimum=input.Cardinality.Minimum>0?1:0;
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Distinct,
                input,
                new DistinctOperatorArguments(),
                QueryValueType.Int,
                new(minimum,input.Cardinality.Maximum),
                QueryOrderSemantics.Unordered);
        }

        private UnaryQueryNode CreateDistinctBy(QueryNode input){
            var modulus=this.argumentRandom.NextInt32(2,9);
            var minimum=input.Cardinality.Minimum>0?1:0;
            var maximum=input.Cardinality.Maximum is { }finiteMaximum?Math.Min(finiteMaximum,modulus):modulus;
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.DistinctBy,
                input,
                new DistinctByOperatorArguments(modulus),
                QueryValueType.Int,
                new(minimum,maximum),
                QueryOrderSemantics.Unordered);
        }

        private UnaryQueryNode CreateTake(QueryNode input){
            var maximum=input.Cardinality.Maximum??32;
            var boundedMaximum=(int)Math.Min(Math.Max(maximum,1),32);
            var count=this.argumentRandom.NextInt32(1,boundedMaximum+1);
            var minimum=Math.Min(input.Cardinality.Minimum,count);
            long? outputMaximum;
            if(input.Cardinality.Maximum is { }finiteMaximum){
                outputMaximum=Math.Min(finiteMaximum,count);
            }else{
                outputMaximum=count;
            }
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Take,
                input,
                new TakeOperatorArguments(count),
                QueryValueType.Int,
                new(minimum,outputMaximum),
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateSkip(QueryNode input){
            var maximum=input.Cardinality.Maximum??32;
            var boundedMaximum=(int)Math.Min(Math.Max(maximum,1),32);
            var count=this.argumentRandom.NextInt32(1,boundedMaximum+1);
            var minimum=Math.Max(0,input.Cardinality.Minimum-count);
            long? outputMaximum=input.Cardinality.Maximum is { }finiteMaximum
                ?Math.Max(0,finiteMaximum-count)
                :null;
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Skip,
                input,
                new SkipOperatorArguments(count),
                QueryValueType.Int,
                new(minimum,outputMaximum),
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateReverse(QueryNode input){
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.Reverse,
                input,
                new ReverseOperatorArguments(),
                QueryValueType.Int,
                input.Cardinality,
                input.OrderSemantics);
        }

        private UnaryQueryNode CreateOrderBy(QueryNode input){
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.OrderBy,
                input,
                new OrderByOperatorArguments(false),
                QueryValueType.Int,
                input.Cardinality,
                QueryOrderSemantics.TotalOrdered);
        }

        private UnaryQueryNode CreateThenBy(QueryNode input){
            if(input.OrderSemantics!=QueryOrderSemantics.TotalOrdered){
                throw new InvalidOperationException("ThenBy requires a total-ordered input.");
            }
            return new(
                this.CreateNodeId(),
                QueryOperatorIds.ThenBy,
                input,
                new ThenByOperatorArguments(false),
                QueryValueType.Int,
                input.Cardinality,
                QueryOrderSemantics.TotalOrdered);
        }

        private QueryPredicate CreatePredicate(){
            var kind=this.argumentRandom.Choose(Enum.GetValues<QueryPredicateKind>());
            return kind switch{
                QueryPredicateKind.GreaterThan or QueryPredicateKind.LessThan=>
                    new(kind,this.argumentRandom.NextInt32(-16,17)),
                QueryPredicateKind.ModuloEquals=>
                    new(kind,this.argumentRandom.NextInt32(0,3),this.argumentRandom.NextInt32(2,6)),
                _=>new(kind),
            };
        }

        private SourceNode CreateSource(SourceSlot slot){
            return new(
                this.CreateNodeId(),
                this.options.SourceTarget,
                slot,
                QueryCardinalityRange.Exact(this.options.InputElementCount),
                this.options.SourceTarget.GetInitialOrder());
        }

        private bool HasUnaryOperator(){
            return BuiltInOperatorCatalog.Initial.Any(
                descriptor=>descriptor.NodeKind==QueryNodeKind.Unary&&this.allowed.Contains(descriptor.OperatorId));
        }

        private (int Left,int Right)Split(int remaining,int childCapacity){
            var minimumLeft=Math.Max(0,remaining-childCapacity);
            var maximumLeft=Math.Min(childCapacity,remaining);
            var left=this.shapeRandom.NextInt32(minimumLeft,maximumLeft+1);
            return(left,remaining-left);
        }

        private string CreateNodeId(){
            return $"n{this.nextNodeId++:D4}";
        }

        private static SourceSlot Opposite(SourceSlot slot){
            return slot==SourceSlot.Primary?SourceSlot.Secondary:SourceSlot.Primary;
        }

        private static bool CanSplit(int remaining,int childCapacity){
            return remaining<=QueryCardinalityRange.SaturatingMultiply(childCapacity,2);
        }

        private static QueryTerminalKind GetTerminalKind(string operatorId){
            return operatorId switch{
                QueryOperatorIds.Count=>QueryTerminalKind.Count,
                QueryOperatorIds.LongCount=>QueryTerminalKind.LongCount,
                QueryOperatorIds.Any=>QueryTerminalKind.Any,
                QueryOperatorIds.All=>QueryTerminalKind.All,
                QueryOperatorIds.Contains=>QueryTerminalKind.Contains,
                QueryOperatorIds.Sum=>QueryTerminalKind.Sum,
                QueryOperatorIds.Min=>QueryTerminalKind.Min,
                QueryOperatorIds.Max=>QueryTerminalKind.Max,
                QueryOperatorIds.MinBy=>QueryTerminalKind.MinBy,
                QueryOperatorIds.MaxBy=>QueryTerminalKind.MaxBy,
                QueryOperatorIds.Average=>QueryTerminalKind.Average,
                QueryOperatorIds.AggregateXor=>QueryTerminalKind.AggregateXor,
                QueryOperatorIds.ToIntPair=>QueryTerminalKind.ToIntPair,
                QueryOperatorIds.ToIntGroup=>QueryTerminalKind.ToIntGroup,
                QueryOperatorIds.First=>QueryTerminalKind.First,
                QueryOperatorIds.FirstOrDefault=>QueryTerminalKind.FirstOrDefault,
                QueryOperatorIds.Last=>QueryTerminalKind.Last,
                QueryOperatorIds.LastOrDefault=>QueryTerminalKind.LastOrDefault,
                QueryOperatorIds.ElementAt=>QueryTerminalKind.ElementAt,
                QueryOperatorIds.ElementAtOrDefault=>QueryTerminalKind.ElementAtOrDefault,
                QueryOperatorIds.ToArray=>QueryTerminalKind.ToArray,
                QueryOperatorIds.ToList=>QueryTerminalKind.ToList,
                QueryOperatorIds.ToHashSet=>QueryTerminalKind.ToHashSet,
                QueryOperatorIds.ToDictionary=>QueryTerminalKind.ToDictionary,
                QueryOperatorIds.ToLookup=>QueryTerminalKind.ToLookup,
                _=>throw new InvalidOperationException($"Unsupported terminal operator: {operatorId}."),
            };
        }
    }

    private static int GetMaximumSequenceNodes(int depth,bool hasBinary,bool hasNested){
        if(depth<=0){
            return 0;
        }
        if(!hasBinary&&!hasNested){
            return depth;
        }
        if(!hasBinary){
            return checked((depth*2)-1);
        }
        var capacity=0L;
        for(var index=0;index<depth;index++){
            capacity=QueryCardinalityRange.SaturatingAdd(1,QueryCardinalityRange.SaturatingMultiply(capacity,2));
        }
        return (int)Math.Min(capacity,int.MaxValue);
    }

}
