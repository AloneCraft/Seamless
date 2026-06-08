namespace Seamless
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class OptimizeAttribute : Attribute
    {
    }
}
