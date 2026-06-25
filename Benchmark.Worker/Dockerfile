FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Benchmark.Worker/Benchmark.Worker.csproj","Benchmark.Worker/"]
COPY ["Benchmark.DotNet.Linq.Enumerable.FLinq/Benchmark.DotNet.Linq.Enumerable.FLinq.csproj","Benchmark.DotNet.Linq.Enumerable.FLinq/"]
COPY ["Benchmark.DotNet.Linq.Enumerable.ZLinq/Benchmark.DotNet.Linq.Enumerable.ZLinq.csproj","Benchmark.DotNet.Linq.Enumerable.ZLinq/"]
COPY ["Benchmark.Tsavorite.Linq.Enumerable.Linq/Benchmark.Tsavorite.Linq.Enumerable.Linq.csproj","Benchmark.Tsavorite.Linq.Enumerable.Linq/"]
COPY ["Benchmark.Tsavorite.Linq.Enumerable.FLinq/Benchmark.Tsavorite.Linq.Enumerable.FLinq.csproj","Benchmark.Tsavorite.Linq.Enumerable.FLinq/"]
COPY ["Benchmark.Persisted.Linq.Enumerable.Linq/Benchmark.Persisted.Linq.Enumerable.Linq.csproj","Benchmark.Persisted.Linq.Enumerable.Linq/"]
COPY ["Benchmark.Persisted.Linq.Enumerable.FLinq/Benchmark.Persisted.Linq.Enumerable.FLinq.csproj","Benchmark.Persisted.Linq.Enumerable.FLinq/"]
COPY ["Benchmark.Persisted.Collection/Benchmark.Persisted.Collection.csproj","Benchmark.Persisted.Collection/"]
COPY ["Benchmark.Tsavorite.Collection/Benchmark.Tsavorite.Collection.csproj","Benchmark.Tsavorite.Collection/"]
COPY ["Seamless/Seamless.csproj","Seamless/"]
COPY ["Seamless.FLinq/Seamless.FLinq.csproj","Seamless.FLinq/"]
COPY ["Seamless.SourceGenerator/Seamless.SourceGenerator.csproj","Seamless.SourceGenerator/"]
COPY ["Seamless.Collections/Seamless.Collections.csproj","Seamless.Collections/"]
COPY ["Seamless.Database/Seamless.Database.csproj","Seamless.Database/"]
COPY ["Directory.Build.props","."]
COPY ["Directory.Build.targets","."]
RUN dotnet restore "Benchmark.Worker/Benchmark.Worker.csproj"
COPY . .
WORKDIR "/src/Benchmark.Worker"
RUN dotnet publish "Benchmark.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","Benchmark.Worker.dll","--logical-cpu","0","--run-root","/tmp/seamless-benchmark"]
