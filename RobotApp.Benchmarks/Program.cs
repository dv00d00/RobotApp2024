using System.Buffers;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LanguageExt;
using RobotApp.Logic;

namespace RobotApp.Dirty;

[MemoryDiagnoser]
[ShortRunJob]
public class ParserBenchmarkComparison
{
    [Benchmark]
    public async Task<Either<Logic.Error, Logic.ParsedFile>> Safe()
    {
        var input = await File.ReadAllTextAsync("SampleBig.txt");
        return Parser.ParseInput(input);
    }
    
    [Benchmark]
    public async Task<Result<ParsedFile>> Dirty()
    {
        await using var file = File.OpenRead("SampleBig.txt");
        var pipeReader = PipeReader.Create(file);
        var parser = new FileParser(pipeReader, new StateMachineParser());
        var result = await parser.ParseAsync();
        return result;
    }
    
    private static readonly string _cachedFileString = File.ReadAllText("SampleBig.txt");
    
    [Benchmark]
    public Either<Logic.Error, Logic.ParsedFile> Safe_CachedFile()
    {
        return Parser.ParseInput(_cachedFileString);
    }
    
    private static readonly byte[] _cachedFileBytes = File.ReadAllBytes("SampleBig.txt");
    
    [Benchmark]
    public async Task<Result<ParsedFile>> Dirty_CachedFile()
    {
        var pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(_cachedFileBytes));
        var parser = new FileParser(pipeReader, new StateMachineParser());
        var result = await parser.ParseAsync();
        return result;
    }
}

[MemoryDiagnoser]
[ShortRunJob]
public class RobotCompositionRoot
{
    private static readonly string _cachedFileString = File.ReadAllText("SampleBig.txt");
    
    [Benchmark]
    public IReadOnlyCollection<string> Go()
    {
        return CompositionRoot.Execute(_cachedFileString);
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}