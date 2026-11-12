namespace Seamless.Database.Client;

public sealed record NativeWireLimits{
    public static NativeWireLimits Default{get;}=new();
    public int MaximumBytes{get;init;}=NativeFrame.MaximumBytes-1;
    public int MaximumStringCharacters{get;init;}=65536;
    public int MaximumArrayElements{get;init;}=65536;
    public int MaximumValueNodes{get;init;}=1000000;
    public void CheckProfile(){if(this.MaximumBytes is <=0 or >=NativeFrame.MaximumBytes||this.MaximumStringCharacters<=0||this.MaximumArrayElements<=0||this.MaximumValueNodes<=0){throw new ArgumentOutOfRangeException(nameof(NativeWireLimits));}}
}
