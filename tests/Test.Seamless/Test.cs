namespace Test.Seamless.Collections;

public abstract class Test<TKey,TValue>(ITestOutputHelper? output=null)
    where TKey:IEquatable<TKey>
    where TValue:notnull{
    protected abstract double MutableFraction{get;}
    protected abstract TKey CreateKey(int seed);
    protected abstract TValue CreateValue(int seed);
    protected void Write(string message)=>output?.WriteLine(message);
}
