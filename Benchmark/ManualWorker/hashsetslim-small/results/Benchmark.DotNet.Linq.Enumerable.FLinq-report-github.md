```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.301
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method    | Mean     | Error   | StdDev  | Gen0    | Gen1    | Gen2    | Allocated |
|---------- |---------:|--------:|--------:|--------:|--------:|--------:|----------:|
| ToHashSet | 197.4 μs | 2.72 μs | 2.41 μs | 13.4277 | 13.4277 | 13.4277 |  394.4 KB |
