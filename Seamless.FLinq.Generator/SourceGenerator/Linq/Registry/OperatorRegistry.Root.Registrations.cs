namespace Seamless.FLinq;

internal static partial class OperatorRegistry {
    private static ImmutableArray<IOperatorDescriptor> CreateRootDescriptors() {
        return ImmutableArray.Create<IOperatorDescriptor>(
            CreateAsEnumerableRoot(),
            CreateRoot<EmptyRoot,EmptyRootArguments>("Empty",1000,BindEmptyRoot,DirectBindEmptyRoot,new FragmentParameter[] { FragmentParameter.ResultElementType },static arguments => new EmptyRoot(arguments.Number,arguments.GetExpression(0)),static method => method.Parameters.Length==0&&method.TypeArguments.Length==1),
            CreateRoot<InfiniteSequenceRoot,InfiniteSequenceRootArguments>("InfiniteSequence",1000,BindInfiniteSequenceRoot,DirectBindInfiniteSequenceRoot,new FragmentParameter[] { FragmentParameter.Expression("start"),FragmentParameter.Expression("next"),FragmentParameter.ResultElementType },static arguments => new InfiniteSequenceRoot(arguments.Number,arguments.GetExpression(0),arguments.GetExpression(1),arguments.GetExpression(2)),static method => method.Parameters.Length==2&&method.TypeArguments.Length==1),
            CreateRoot<RangeRoot,RangeRootArguments>("Range",1000,BindRangeRoot,DirectBindRangeRoot,new FragmentParameter[] { FragmentParameter.Expression("start"),FragmentParameter.Expression("count") },static arguments => new RangeRoot(arguments.Number,arguments.GetExpression(0),arguments.GetExpression(1)),static method => method.Parameters.Length==2&&method.TypeArguments.Length==0),
            CreateRoot<RepeatRoot,RepeatRootArguments>("Repeat",1000,BindRepeatRoot,DirectBindRepeatRoot,new FragmentParameter[] { FragmentParameter.Expression("element"),FragmentParameter.Expression("count"),FragmentParameter.ResultElementType },static arguments => new RepeatRoot(arguments.Number,arguments.GetExpression(0),arguments.GetExpression(1),arguments.GetExpression(2)),static method => method.Parameters.Length==2&&method.TypeArguments.Length==1),
            CreateRoot<SequenceRoot,SequenceRootArguments>("Sequence",1000,BindSequenceRoot,DirectBindSequenceRoot,new FragmentParameter[] { FragmentParameter.Expression("start"),FragmentParameter.Expression("next"),FragmentParameter.Expression("condition"),FragmentParameter.ResultElementType },static arguments => new SequenceRoot(arguments.Number,arguments.GetExpression(0),arguments.GetExpression(1),arguments.GetExpression(2),arguments.GetExpression(3)),static method => method.Parameters.Length==3&&method.TypeArguments.Length==1)
        );
    }
}
