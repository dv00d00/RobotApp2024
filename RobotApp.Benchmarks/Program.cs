using System.Buffers;
using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LanguageExt;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using RobotApp.Logic;

namespace RobotApp.Dirty;

[MemoryDiagnoser]
[ShortRunJob]
public class ParserBenchmarkComparison
{
    
    private static readonly string _cachedFileString = File.ReadAllText("SampleBig.txt");
    private static readonly byte[] _cachedFileBytes = File.ReadAllBytes("SampleBig.txt");
    
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

    [Benchmark]
    public Either<Logic.Error, Logic.ParsedFile> Safe_CachedFile()
    {
        return Parser.ParseInput(_cachedFileString);
    }
    
    [Benchmark]
    public async Task<Result<ParsedFile>> Dirty_CachedFile()
    {
        var pipeReader = PipeReader.Create(new ReadOnlySequence<byte>(_cachedFileBytes));
        var parser = new FileParser(pipeReader, new StateMachineParser());
        var result = await parser.ParseAsync();
        return result;
    }
    
    [Benchmark]
    public FSharpResult<FSharp.Parser.ParsedFile, FSharpList<string>> FsharpSafe_CachedFile()
    {
        return FSharp.Parser.runParseFile(_cachedFileString);
    }
    
    [Benchmark]
    public FSharpResult<FSharp.Parser.ParsedFile, FSharpList<string>> FsharpSafe_Stream()
    {
        using var stream = File.OpenRead("SampleBig.txt");
        return FSharp.Parser.runParseFileS(stream);
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