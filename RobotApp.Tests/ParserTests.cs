using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using LanguageExt;
using RobotApp.Logic;
using Xunit.Abstractions;

namespace RobotApp.Tests;

public static class InputGenerators
{
    public static Gen<string> OneOrMoreSpaces => Gen.Elements(" ").NonEmptyListOf().Select(xs => string.Join("", xs));
    public static Gen<string> ValidDirection => Gen.Elements("N", "E", "S", "W");
    public static Gen<string> ValidCommand => Gen.Elements("L", "R", "F");
    public static Gen<string> ValidCommands => ValidCommand.NonEmptyListOf().Select(xs => string.Join("", xs));
    public static Gen<string> ValidNewLine => Gen.OneOf(Gen.Constant("\n"), Gen.Constant("\r\n"));
    public static Gen<string> ValidNewLines => ValidNewLine.NonEmptyListOf().Select(xs => string.Join("", xs));

    // Generator for valid GRID inputs
    public static Gen<string> ValidGrid =>
        from width in Gen.Choose(1, int.MaxValue)
        from height in Gen.Choose(1, int.MaxValue)
        from spaces1 in OneOrMoreSpaces
        select $"GRID{spaces1}{width}x{height}";

    // Generator for valid OBSTACLE inputs
    public static Gen<string> ValidObstacle =>
        from x in Gen.Choose(0, int.MaxValue)
        from y in Gen.Choose(0, int.MaxValue)
        from spaces1 in OneOrMoreSpaces
        from spaces2 in OneOrMoreSpaces
        select $"OBSTACLE{spaces1}{x}{spaces2}{y}";

    // Generator for valid RobotJourney inputs
    public static Gen<string> ValidJourney =>
        from x1 in Gen.Choose(0, int.MaxValue)
        from y1 in Gen.Choose(0, int.MaxValue)
        from direction1 in ValidDirection
        from commands in ValidCommands
        from x2 in Gen.Choose(0, int.MaxValue)
        from y2 in Gen.Choose(0, int.MaxValue)
        from direction2 in ValidDirection
        from newline in ValidNewLine
        from spaces1 in OneOrMoreSpaces
        from spaces2 in OneOrMoreSpaces
        from spaces3 in OneOrMoreSpaces
        from spaces4 in OneOrMoreSpaces
        select
            $"{x1}{spaces1}{y1}{spaces2}{direction1}{newline}{commands}{newline}{x2}{spaces3}{y2}{spaces4}{direction2}";

    // Generator for valid file inputs
    public static Gen<string> ValidFile =>
        from grid in ValidGrid
        from newline1 in ValidNewLines
        from obstacles in ValidObstacle.ListOf().Select(xs => string.Join("", xs))
        from newline2 in ValidNewLines
        from journey in ValidJourney.ListOf().Select(xs => string.Join("", xs))
        select $"{grid}{newline1}{string.Join(newline2, obstacles)}{newline2}{journey}";
}

public class ParserTests(ITestOutputHelper output)
{
    [Fact]
    public void Should_SuccessfullyParse_SingleValidJourney()
    {
        Prop.ForAll(InputGenerators.ValidJourney.ToArbitrary(), input =>
        {
            var result = Parser.ParseJourney.Parse(input);
            return result.ToEither().Match(
                Right: _ => true,
                Left: error =>
                {
                    output.WriteLine(error.ToString());
                    return false;
                });
        }).QuickCheckThrowOnFailure(output);
    }

    [Fact]
    public void Should_SuccessfullyParse_SingleValidObstacle()
    {
        Prop.ForAll(InputGenerators.ValidObstacle.ToArbitrary(), input =>
        {
            var result = Parser.ParseObstacle.Parse(input);
            return result.ToEither().Match(
                Right: _ => true,
                Left: error =>
                {
                    output.WriteLine(error.ToString());
                    return false;
                });
        }).QuickCheckThrowOnFailure(output);
    }

    [Fact]
    public void Should_SuccessfullyParse_SingleValidGrid()
    {
        Prop.ForAll(InputGenerators.ValidGrid.ToArbitrary(), input =>
        {
            var result = Parser.ParseGrid.Parse(input);
            return result.ToEither().Match(
                Right: _ => true,
                Left: error =>
                {
                    output.WriteLine(error.ToString());
                    return false;
                });
        }).QuickCheckThrowOnFailure(output);
    }

    [Fact]
    public void Should_SuccessfullyParse_ValidFile()
    {
        Prop.ForAll(InputGenerators.ValidFile.ToArbitrary(), input =>
        {
            var result = Parser.ParseInput(input);
            return result.Match(
                Right: _ => true,
                Left: error =>
                {
                    output.WriteLine(error.ToString());
                    return false;
                });
        }).QuickCheckThrowOnFailure(output);
    }
}