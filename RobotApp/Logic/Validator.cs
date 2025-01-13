using LanguageExt;
using static LanguageExt.Prelude;

namespace RobotApp.Logic;

public static class Validator   
{
    public static bool IsValidCoordinate(Grid grid, int x, int y) =>
        x >= 0 && x < grid.Width && y >= 0 && y < grid.Height;

    public static Validation<ValidationError, Grid> ValidateGrid(Grid grid) =>
        grid is { Height: > 0, Width: > 0 }
            ? grid
            : ValidationError.InvalidGrid(grid);

    public static Validation<ValidationError, RobotState> ValidateState(Grid grid, RobotState state, bool initial) => 
        IsValidCoordinate(grid, state.X, state.Y) 
            ? state
            : ValidationError.RobotStateOutOfBounds(state, grid, initial);

    public static Validation<ValidationError, RobotJourney> ValidateJourney(Grid grid, RobotJourney journey)
    {
        return (ValidateState(grid, journey.InitialState, true), 
                ValidateState(grid, journey.ExpectedFinalState, false))
            .Apply((_,_) => journey);
    }

    public static Validation<ValidationError, Obstacle> ValidateObstacle(Grid grid, Obstacle obstacle) =>
        IsValidCoordinate(grid, obstacle.X, obstacle.Y)
            ? obstacle
            : ValidationError.ObstacleOutOfBounds(obstacle, grid);
        
    public static Validation<ValidationError, ValidatedFile> ValidateParsedFileA(ParsedFile parsedFile) =>
        ValidateGrid(parsedFile.Grid)
            .Bind(grid =>
            {
                var maybeObstacles = parsedFile.Obstacles.Sequence(o => ValidateObstacle(grid, o));
                var maybeJourneys = parsedFile.Journeys.Sequence(j => ValidateJourney(grid, j));

                return (maybeObstacles, maybeJourneys)
                    .Apply((obstacles, journeys) => new ValidatedFile(grid, toHashSet(obstacles), journeys));
            });

    public static Either<Error, ValidatedFile> ValidateParsedFile(ParsedFile parsedFile) =>
        ValidateParsedFileA(parsedFile)
            .ToEither()
            .MapLeft(errs => new ValidationErrors(toList(errs)) as Error);

    static Either<Error, Validated.File> ValidateParsedFile_Alternate(ParsedFile parsedFile) =>
        Validated.File.Create(parsedFile)
            .ToEither()
            .MapLeft(errs => new ValidationErrors(toList(errs)) as Error);
}