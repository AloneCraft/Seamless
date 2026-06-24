```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
Intel Core i7-14700F 2.10GHz, 1 CPU, 28 logical and 20 physical cores
.NET SDK 10.0.301
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method             | Mean     | Error     | StdDev    | Gen0    | Gen1   | Allocated |
|------------------- |---------:|----------:|----------:|--------:|-------:|----------:|
| ThenBy_keySelector | 2.498 ms | 0.0497 ms | 0.1172 ms | 15.6250 | 3.9063 | 266.08 KB |
