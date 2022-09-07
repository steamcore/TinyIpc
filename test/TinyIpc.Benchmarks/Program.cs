#pragma warning disable CA1812 // Workaround for analyzer bug https://github.com/dotnet/roslyn-analyzers/issues/5628
using BenchmarkDotNet.Running;
using TinyIpc.Benchmarks;

BenchmarkRunner.Run<Benchmark>();
