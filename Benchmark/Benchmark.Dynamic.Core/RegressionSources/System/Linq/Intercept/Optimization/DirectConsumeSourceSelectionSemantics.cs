namespace Test.System.Linq.Intercept.Optimization;

[global::Seamless.FLinq.LinqIntercept(false)]
public sealed class DirectConsumeSourceSelectionSemantics {
    [Fact, Trait("Category","Unit")]
    public void OrdinaryRangeWrapperIsSelectedWhenConsumerIsAlsoAnInvocation() {
        var expected=new[] {
            "consumer.Create",
            "consumer.Consume(4)",
            "consumer.Consume(5)",
            "consumer.Consume(6)",
        };

        var receiverExpressionEvents=new List<string>();
        ReceiverExpressionOptimized(receiverExpressionEvents);
        Assert.Equal(expected,receiverExpressionEvents);

        var receiverStatementEvents=new List<string>();
        ReceiverStatementOptimized(receiverStatementEvents);
        Assert.Equal(expected,receiverStatementEvents);

        var argumentExpressionEvents=new List<string>();
        ArgumentExpressionOptimized(argumentExpressionEvents);
        Assert.Equal(expected,argumentExpressionEvents);

        var argumentStatementEvents=new List<string>();
        ArgumentStatementOptimized(argumentStatementEvents);
        Assert.Equal(expected,argumentStatementEvents);
    }

    private static IEnumerable<int> CreateRange(int start,int count) => Enumerable.Range(start,count);

    private static RecordingConsumer<int> CreateConsumer(List<string> events) {
        events.Add("consumer.Create");
        return new RecordingConsumer<int>(events);
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ReceiverExpressionOptimized(List<string> events) =>
        CreateRange(4,3).Consume(CreateConsumer(events));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ReceiverStatementOptimized(List<string> events) {
        CreateRange(4,3).Consume(CreateConsumer(events));
    }

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArgumentExpressionOptimized(List<string> events) =>
        CreateConsumer(events).Consume(CreateRange(4,3));

    [global::Seamless.FLinq.LinqIntercept(true)]
    private static void ArgumentStatementOptimized(List<string> events) {
        CreateConsumer(events).Consume(CreateRange(4,3));
    }
}
