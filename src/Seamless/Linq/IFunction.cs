namespace Seamless.Linq;

public interface IFunction<TResult>
{
    static abstract TResult Invoke();
}

public interface IFunction<T1, TResult>
{
    static abstract TResult Invoke(T1 value1);
}

public interface IFunction<T1, T2, TResult>
{
    static abstract TResult Invoke(T1 value1,T2 value2);
}

public interface IFunction<T1, T2, T3, TResult>
{
    static abstract TResult Invoke(T1 value1,T2 value2,T3 value3);
}
