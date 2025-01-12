using LanguageExt;

namespace RobotApp.Logic;

public static class Validator   
{
    public static bool IsValidCoordinate(Grid grid, int x, int y) =>
        x >= 0 && x < grid.Width && y >= 0 && y < grid.Height;

    static Validation<ValidationError, Grid> ValidateGrid(Grid grid) =>
        grid is { Height: > 0, Width: > 0 }
            ? grid
            : ValidationError.InvalidGrid(grid);

    static Validation<ValidationError, RobotState> ValidateState(Grid grid, RobotState state, bool initial) => 
        IsValidCoordinate(grid, state.X, state.Y) 
            ? state
            : ValidationError.RobotStateOutOfBounds(state, grid, initial);

    static Validation<ValidationError, RobotJourney> ValidateJourney(Grid grid, RobotJourney journey)
    {
        return (ValidateState(grid, journey.InitialState, true), 
                ValidateState(grid, journey.ExpectedFinalState, false))
            .Apply((_,_) => journey);
    }

    static Validation<ValidationError, Obstacle> ValidateObstacle(Grid grid, Obstacle obstacle) =>
        IsValidCoordinate(grid, obstacle.X, obstacle.Y)
            ? obstacle
            : ValidationError.ObstacleOutOfBounds(obstacle, grid);

    static Validation<ValidationError, ValidatedFile> ValidateParsedFileM(ParsedFile parsedFile) =>
        from grid in ValidateGrid(parsedFile.Grid)
        from obstacles in parsedFile.Obstacles.Map(o => ValidateObstacle(parsedFile.Grid, o)).Sequence()
        from journeys in parsedFile.Journeys.Map(j => ValidateJourney(parsedFile.Grid, j)).Sequence()
        select new ValidatedFile(parsedFile.Grid, Prelude.toHashSet(obstacles), journeys);
        
    static Validation<ValidationError, ValidatedFile> ValidateParsedFileA(ParsedFile parsedFile) =>
        ValidateGrid(parsedFile.Grid)
            .Bind(grid =>
            {
                var maybeObstacles = parsedFile.Obstacles.Sequence(o => ValidateObstacle(grid, o));
                var maybeJourneys = parsedFile.Journeys.Sequence(j => ValidateJourney(grid, j));

                var validatedFile = (maybeObstacles, maybeJourneys)
                    .Apply((obstacles, journeys) => new ValidatedFile(grid, Prelude.toHashSet(obstacles), journeys));
                    
                return validatedFile;
            });

    public static Either<Error, ValidatedFile> ValidateParsedFile(ParsedFile parsedFile)
    {
        var validateParsedFileA = ValidateParsedFileA(parsedFile);
        return validateParsedFileA
            .ToEither()
            .MapLeft(errs => new ValidationErrors(Prelude.toList(errs)) as Error);
    }
}