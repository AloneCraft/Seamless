using Microsoft.AspNetCore.Components;

namespace Seamless.Management.Components;

public sealed record 対象操作要求(string ID,ElementReference 起点,double X,double Y);
