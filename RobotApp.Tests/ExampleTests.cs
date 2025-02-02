﻿using RobotApp.Logic;
using Xunit.Abstractions;

namespace RobotApp.Tests;

public class ExampleBasedTests(ITestOutputHelper output)
{
    private static readonly string Sample0 = File.ReadAllText(Path.Combine("Samples","Sample0.txt"));
    private static readonly string[] Sample0Expected =
    [
        "SUCCESS 1 0 W",
        "FAILURE 0 0 W",
        "OUT OF BOUNDS"
    ];
    
    private static readonly string Sample1 = File.ReadAllText(Path.Combine("Samples","Sample1.txt"));
    private static readonly string[] Sample1Expected =
    [
        "SUCCESS 1 1 E",
        "SUCCESS 3 3 N",
        "SUCCESS 2 4 S"
    ];
    
    private static readonly string Sample2 = File.ReadAllText(Path.Combine("Samples","Sample2.txt"));
    private static readonly string[] Sample2Expected =
    [
        "SUCCESS 1 1 E",
        "SUCCESS 3 3 N",
        "CRASHED 1 3"
    ];
    
    private static readonly string EmptyValidGrid = File.ReadAllText(Path.Combine("Samples","EmptyValidGrid.txt"));
    
    private static readonly string[] InvalidExamples = Directory.EnumerateFiles(@"SamplesBad", "*.txt").ToArray();
    public static readonly IEnumerable<object[]> InvalidExamplesXunit = InvalidExamples.Select(path => new object[] { path });
    
    [Fact]
    public void Should_HandleEmptyValidGrid()
    {
        var actualOutput = CompositionRoot.Execute(EmptyValidGrid).ToArray();
        Assert.Empty(actualOutput);
    }
    
    [Theory]
    [MemberData(nameof(InvalidExamplesXunit))]    
    public void Should_HandleInvalidExamples(string path)
    {
        var content = File.ReadAllText(path);
        output.WriteLine(content);
        var actualOutput = CompositionRoot.Execute(content).ToArray();
        Assert.NotEmpty(actualOutput);
        Assert.All(actualOutput, x => Assert.DoesNotMatch("SUCCESS.*", x));
    }
    
    [Theory]
    [MemberData(nameof(ValidExamples))]    
    public void Should_HandleProvidedExamples(TestCase testCase)
    {
        var actualOutput = CompositionRoot.Execute(testCase.Input).ToArray();
        Assert.Equal(expected: testCase.ExpectedOutput, actual: actualOutput);
    }
    
    public static IEnumerable<object[]> ValidExamples() 
    {
        yield return [new TestCase("Sample0", Sample0, Sample0Expected)];
        yield return [new TestCase("Sample1", Sample1, Sample1Expected)];
        yield return [new TestCase("Sample2", Sample2, Sample2Expected)];
    }

    public record TestCase(string Name, string Input, string[] ExpectedOutput)
    {
        public override string ToString() => Name;
    }
}