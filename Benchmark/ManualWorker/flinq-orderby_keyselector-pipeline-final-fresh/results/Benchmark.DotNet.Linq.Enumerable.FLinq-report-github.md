```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.301
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method              | Mean     | Error     | StdDev   | Median   | Gen0      | Allocated |
|-------------------- |---------:|----------:|---------:|---------:|----------:|----------:|
| OrderBy_keySelector | 7.373 ms | 0.5021 ms | 1.480 ms | 8.376 ms | 2359.3750 |  38.91 MB |
