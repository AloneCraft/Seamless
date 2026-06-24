```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8655/25H2/2025Update/HudsonValley2)
Unknown processor
.NET SDK 10.0.301
  [Host] : .NET 10.0.9 (10.0.9, 10.0.926.27113), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  

```
| Method              | Mean     | Error     | StdDev    | Allocated |
|-------------------- |---------:|----------:|----------:|----------:|
| OrderBy_keySelector | 1.549 ms | 0.0119 ms | 0.0105 ms |       7 B |
