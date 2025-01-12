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

public enum ValidationErrorType { InvalidGrid, ObstacleOutOfBounds, RobotStateOutOfBounds }
public record ValidationError(ValidationErrorType Kind, string Message) 
{
    public static ValidationError InvalidGrid(Grid grid) => 
        new ValidationError(
            ValidationErrorType.InvalidGrid, 
            $"Invalid grid {grid}");
    
    public static ValidationError ObstacleOutOfBounds(Obstacle obstacle, Grid grid) => 
        new ValidationError(
            ValidationErrorType.ObstacleOutOfBounds, 
            $"Obstacle [{obstacle}] out of bounds of defined grid [{grid}]");
    
    public static ValidationError RobotStateOutOfBounds(RobotState state, Grid grid, bool initial)
    {
        var message = initial 
                ? $"Initial robot state [{state}] out of bounds of defined grid [{grid}]"
                : $"Final robot state [{state}] out of bounds of defined grid [{grid}]";
        
        return new ValidationError(
            ValidationErrorType.RobotStateOutOfBounds,
            message);
    }
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