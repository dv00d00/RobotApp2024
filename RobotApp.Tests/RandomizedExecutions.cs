using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using RobotApp.Logic;
using Xunit.Abstractions;

namespace RobotApp.Tests;

public class RandomizedExecutions(ITestOutputHelper output)
{
    // Random bounded start, 4 rotations, same end
    private static Gen<string> TrivialValidJourney(int maxW, int maxH) =>
        from x in Gen.Choose(0, maxW - 1)
        from y in Gen.Choose(0, maxH - 1)
        from direction in InputGenerators.ValidDirection
        from trivialCommands in Gen.OneOf(Gen.Constant("RRRR"), Gen.Constant("LLLL"))
        select @$"{x} {y} {direction}
{trivialCommands}
{x} {y} {direction}";

    // Generator for trivial valid file 
    private static Gen<string> TrivialValidFile =>
        from width in Gen.Choose(1, Int32.MaxValue)
        from height in Gen.Choose(1, Int32.MaxValue)
        from journeys in TrivialValidJourney(width, height).NonEmptyListOf().Select(xs => string.Join("\n\n", xs))
        select $"GRID {width}x{height}\n\n" +
               $"{journeys}";
    
    // Random bounded start, > max F, same end
    private static Gen<string> TrivialInvalidJourney(int maxW, int maxH) =>
        from x in Gen.Choose(0, maxW - 1)
        from y in Gen.Choose(0, maxH - 1)
        from direction in InputGenerators.ValidDirection
        from trivialCommands in Gen.Constant("F").ListOf(Math.Max(maxW, maxH)).Select(xs => string.Join("", xs))
        select 
            @$"{x} {y} {direction}
{trivialCommands}
0 0 N";
    
    // Generator for trivial invalid file, robot expected to go out of bounds
    private static Gen<string> TrivialInvalidFile =>
        from width in Gen.Choose(1, 128)
        from height in Gen.Choose(1, 128)
        from journeys in TrivialInvalidJourney(width, height).NonEmptyListOf().Select(xs => string.Join("\n\n", xs))
        select $"GRID {width}x{height}\n\n" +
               $"{journeys}";
    
    // Helper: Generates a border of obstacles around a grid
    private static string GenerateObstacleBorder(int width, int height)
    {
        var top = Enumerable.Range(0, width).Select(x => $"OBSTACLE {x} {height - 1}");
        var bottom = Enumerable.Range(0, width).Select(x => $"OBSTACLE {x} 0");
        var left = Enumerable.Range(1, height - 2).Select(y => $"OBSTACLE 0 {y}");
        var right = Enumerable.Range(1, height - 2).Select(y => $"OBSTACLE {width - 1} {y}");

        return string.Join("\n", top.Concat(bottom).Concat(left).Concat(right));
    }

    // Generates journeys that will hit the obstacle border
    private static Gen<string> JourneyHittingBorder(int maxW, int maxH) =>
        from x in Gen.Choose(1, maxW - 2) // Ensure start is inside the grid
        from y in Gen.Choose(1, maxH - 2)
        from direction in InputGenerators.ValidDirection
        from trivialCommands in Gen.Constant("F").ListOf(Math.Max(maxW, maxH)).Select(xs => string.Join("", xs))
        select @$"{x} {y} {direction}
{trivialCommands}
{x} {y} {direction}";

    // Generates files with a grid, border obstacles, and journeys hitting the border
    private static Gen<string> FileWithObstacleBorder =>
        from width in Gen.Choose(3, 128)
        from height in Gen.Choose(3, 128)
        from journeys in JourneyHittingBorder(width, height).NonEmptyListOf().Select(xs => string.Join("\n\n", xs))
        select $"GRID {width}x{height}\n\n" +
               $"{GenerateObstacleBorder(width, height)}\n\n" +
               $"{journeys}";
    
    // Random bounded start, 1 move forward, same end
    private static Gen<string> TrivialInvalidJourney_OneMoveForward(int maxW, int maxH) =>
        from x in Gen.Choose(1, maxW - 2)
        from y in Gen.Choose(1, maxH - 2)
        from direction in InputGenerators.ValidDirection
        select @$"{x} {y} {direction}
F
{x} {y} {direction}";

    // Generator for trivial invalid file where all journeys perform single valid move forward
    private static Gen<string> TrivialInvalidFile_OneMoveForward =>
        from width in Gen.Choose(3, Int32.MaxValue)
        from height in Gen.Choose(3, Int32.MaxValue)
        from journeys in TrivialInvalidJourney_OneMoveForward(width, height).NonEmptyListOf().Select(xs => string.Join("\n\n", xs))
        select $"GRID {width}x{height}\n\n" +
               $"{journeys}";

    [Fact]
    public void ForAllTrivialValidJourneys_ShouldArriveToSameState()
    {
        Prop.ForAll(TrivialValidFile.ToArbitrary(), input =>
        {
            return Parser.ParseInput(input)
                .SelectMany(Validator.ValidateParsedFile,
                    (_, validatedFile) =>
                    {
                        return Runtime
                            .TravelAll(validatedFile)
                            .ForAll(item => item.Match(
                                    Left: _ => false, 
                                    Right: state => 
                                        validatedFile.Journeys
                                            .Map(j => j.ExpectedFinalState)
                                            .Contains(state)
                                )
                            );
                    })
                .Match(valid => true, error => false);
        }).QuickCheckThrowOnFailure(output);
    }
    
    [Fact]
    public void ForAGridWithoutObstacles_WhenGoingForwardLongEnough_ShouldGoOutOfBounds()
    {
        Prop.ForAll(TrivialInvalidFile.ToArbitrary(), input =>
        {
            bool result = Parser.ParseInput(input)
                .SelectMany(Validator.ValidateParsedFile,
                    (_, validatedFile) =>
                    {
                        return Runtime
                            .TravelAll(validatedFile)
                            .ForAll(item => item.Match(
                                    Left: error => error.Kind == RuntimeErrorType.OutOfBounds, 
                                    Right: _ => false
                                )
                            );
                    })
                .Match(valid => valid, error => false);

            return result;
        }).QuickCheckThrowOnFailure(output);
    }
    
    [Fact]
    public void ForABorderedGrid_WhenGoingForwardLongEnough_ShouldCrash()
    {
        Prop.ForAll(FileWithObstacleBorder.ToArbitrary(), input =>
        {
            bool result = Parser.ParseInput(input)
                .SelectMany(Validator.ValidateParsedFile,
                    (_, validatedFile) =>
                    {
                        return Runtime
                            .TravelAll(validatedFile)
                            .ForAll(item => item.Match(
                                    Left: error => error.Kind == RuntimeErrorType.Crashed, 
                                    Right: _ => false
                                )
                            );
                    })
                .Match(valid => valid, error => false);

            return result;
        }).QuickCheckThrowOnFailure(output);
    }
    
    [Fact]
    public void ForAValidGrid_WithNoObstacles_ExecutionShouldReportError_WhenRobotMovesForward_AndExpectedFinalStateIsWrong()
    {
        Prop.ForAll(TrivialInvalidFile_OneMoveForward.ToArbitrary(), input =>
        {
            bool result = Parser.ParseInput(input)
                .SelectMany(Validator.ValidateParsedFile,
                    (_, validatedFile) =>
                    {
                        return Runtime
                            .TravelAll(validatedFile)
                            .ForAll(item => item.Match(
                                    Left: error => error.Kind == RuntimeErrorType.UnexpectedFinalState, 
                                    Right: _ => false
                                )
                            );
                    })
                .Match(valid => valid, error => false);

            return result;
        }).QuickCheckThrowOnFailure(output);
    }
}