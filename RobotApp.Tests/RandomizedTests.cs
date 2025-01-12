using FsCheck.Fluent;
using FsCheck.Xunit;
using RobotApp.Logic;
using Xunit.Abstractions;

namespace RobotApp.Tests;

public class RandomizedTests(ITestOutputHelper output)
{
    [Fact]
    public void Should_NotThrowOn_RandomlyGeneratedStringInput()
    {
        Prop.ForAll<string>(
            input => CompositionRoot.Execute(input)
        ).QuickCheckThrowOnFailure(output);
    }

    [Fact]
    public void Should_NotThrowOn_ValidInputs()
    {
        Prop.ForAll(
            InputGenerators.ValidFile.ToArbitrary(),
            input => CompositionRoot.Execute(input)
        ).QuickCheckThrowOnFailure(output);
    }
    
    [Fact]
    public void Should_NotThrowOn_RandomlyGeneratedStringInput_Log()
    {
        Prop.ForAll<string>(
            input => CompositionRoot.Execute(input, new Program.AsciiGridRuntimeLog())
        ).QuickCheckThrowOnFailure(output);
    }

    [Fact]
    public void Should_NotThrowOn_ValidInputs_Log()
    {
        Prop.ForAll(
            InputGenerators.ValidFile.ToArbitrary(),
            input => CompositionRoot.Execute(input, new Program.AsciiGridRuntimeLog())
        ).QuickCheckThrowOnFailure(output);
    }
}