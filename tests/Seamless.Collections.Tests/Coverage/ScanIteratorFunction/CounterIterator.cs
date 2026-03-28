using Xunit;
namespace Seamless.Collections.Tests;
[Collection("Sequential")]
public class CounterIterator {
    [Fact, Trait("Category","Coverage")]
    public void OnException_例外を無視する() {
        var iter = new Collections.CounterIterator();
        iter.OnException(new Exception("test"),0);
    }
}
