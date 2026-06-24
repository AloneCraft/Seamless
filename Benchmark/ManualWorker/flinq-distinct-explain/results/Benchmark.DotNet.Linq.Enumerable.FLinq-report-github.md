```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.301
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method   | Mean     | Error    | StdDev   | Median   | Allocated |
|--------- |---------:|---------:|---------:|---------:|----------:|
| Distinct | 28.93 ms | 1.594 ms | 4.624 ms | 27.31 ms |      57 B |
