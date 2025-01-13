using LanguageExt;

namespace RobotApp.Logic;

public enum Direction { N, E, S, W }
public enum Command { L, R, F }

public record Grid(int Width, int Height);
public record struct Obstacle(int X, int Y);
public record RobotState(int X, int Y, Direction Direction);
public record RobotJourney(RobotState InitialState, Lst<Command> Commands, RobotState ExpectedFinalState);
public record ParsedFile(Grid Grid, Lst<Obstacle> Obstacles, Lst<RobotJourney> Journeys);
public record ValidatedFile(Grid Grid, HashSet<Obstacle> Obstacles, Lst<RobotJourney> Journeys);

public abstract record Error;

public record ParserError(string Message) : Error;

public enum ValidationErrorType { InvalidGrid, ObstacleOutOfBounds, RobotStateOutOfBounds, InvalidCommand, InvalidDirection }
public record ValidationError(ValidationErrorType Kind, string Message) 
{
    public static ValidationError InvalidGrid(Grid grid) => 
        new ValidationError(
            ValidationErrorType.InvalidGrid, 
            $"Invalid grid {grid}");
    
    public static ValidationError ObstacleOutOfBounds(Obstacle obstacle, Grid grid) => 
        ObstacleOutOfBounds(obstacle, grid.Width, grid.Height);
    
    public static ValidationError ObstacleOutOfBounds(Obstacle obstacle, Validated.Grid grid) => 
        ObstacleOutOfBounds(obstacle, grid.Width.Value, grid.Height.Value);
    
    public static ValidationError ObstacleOutOfBounds(Obstacle obstacle, int width, int height) => 
        new ValidationError(
            ValidationErrorType.ObstacleOutOfBounds, 
            $"Obstacle [{obstacle}] out of bounds of defined grid [{width}x{height}]");
    
    public static ValidationError RobotStateOutOfBounds(RobotState state, Grid grid, bool initial) => 
        RobotStateOutOfBounds(state, grid.Width, grid.Height, initial);

    public static ValidationError RobotStateOutOfBounds(RobotState state, Validated.Grid grid, bool initial) => 
        RobotStateOutOfBounds(state, grid.Width.Value, grid.Height.Value, initial);

    public static ValidationError RobotStateOutOfBounds(RobotState state, int width, int height, bool initial)
    {
        var message = initial 
            ? $"Initial robot state [{state}] out of bounds of defined grid [{width}x{height}]"
            : $"Final robot state [{state}] out of bounds of defined grid [{width}x{height}]";
        
        return new ValidationError(ValidationErrorType.RobotStateOutOfBounds, message);
    }
    
    public static ValidationError InvalidCommand(Command command) => 
        new ValidationError(
            ValidationErrorType.InvalidCommand, 
            $"Invalid command {command}");
    
    public static ValidationError InvalidDirection(Direction direction) =>
        new ValidationError(
            ValidationErrorType.InvalidDirection,
            $"Invalid direction {direction}");
}
public record ValidationErrors(Lst<ValidationError> Errors) : Error;

public enum RuntimeErrorType { OutOfBounds, Crashed, UnexpectedFinalState }
public record RuntimeError(RuntimeErrorType Kind, RobotState State) 
{
    public static RuntimeError OutOfBounds(RobotState state) 
        => new RuntimeError(RuntimeErrorType.OutOfBounds, state);
    
    public static RuntimeError Crashed(RobotState state) 
        => new RuntimeError(RuntimeErrorType.Crashed, state);
    
    public static RuntimeError UnexpectedFinalState(RobotState state)
        => new RuntimeError(RuntimeErrorType.UnexpectedFinalState, state);
}