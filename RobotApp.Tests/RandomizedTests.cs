using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit.Abstractions;

namespace RobotApp.Tests;

public class RandomizedTests(ITestOutputHelper output)
{
    [Property]
    public void Should_NotThrowOn_RandomlyGeneratedStringInput(string input)
    {
        var exception = Record.Exception(() =>
        {
            _ = CompositionRoot.Execute(input).ToArray();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void Should_NotThrowOn_ValidInputs()
    {
        Prop.ForAll(ParserGenerators.ValidFile.ToArbitrary(), input =>
        {
            var exception = Record.Exception(() =>
            {
                _ = CompositionRoot.Execute(input).ToArray();
            });

            Assert.Null(exception);
        }).QuickCheckThrowOnFailure(output);
    }
}