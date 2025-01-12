using System;
using LanguageExt;
using static LanguageExt.Prelude;

namespace RobotApp.Logic;

public static class Runtime
{
    public interface IRuntimeLog
    {
        void LogJourneyStart(ValidatedFile file, RobotJourney journey);
        void LogState(ValidatedFile file, Either<RuntimeError, RobotState> currentState); 
        void LogJourneyEnd(ValidatedFile file, Either<RuntimeError, RobotState> finalState);
    }
        
    private static Either<RuntimeError, RobotState> Step(ValidatedFile file, RobotState state, Command command)
    {
        return command switch
        {
            Command.L => state with { Direction = TurnLeft(state.Direction) },
            Command.R => state with { Direction = TurnRight(state.Direction) },
            Command.F => MoveForward(file, state),
            _ => throw new InvalidOperationException(
                $"Unknown command {command} in {nameof(Step)} of {nameof(Runtime)}")
        };
    }

    private static Direction TurnLeft(Direction direction) => direction switch
    {
        Direction.N => Direction.W,
        Direction.W => Direction.S,
        Direction.S => Direction.E,
        Direction.E => Direction.N,
        _ => throw new InvalidOperationException($"Unknown direction {direction} in {nameof(TurnLeft)} of {nameof(Runtime)}")
    };

    private static Direction TurnRight(Direction direction) => direction switch
    {
        Direction.N => Direction.E,
        Direction.E => Direction.S,
        Direction.S => Direction.W,
        Direction.W => Direction.N,
        _ => throw new InvalidOperationException($"Unknown direction {direction} in {nameof(TurnRight)}")
    };

    private static Either<RuntimeError, RobotState> MoveForward(ValidatedFile file, RobotState state)
    {
        var (x,y) = state.Direction switch
        {
            Direction.N => (state.X, state.Y + 1),
            Direction.E => (state.X + 1, state.Y),
            Direction.S => (state.X, state.Y - 1),
            Direction.W => (state.X - 1, state.Y),
            _ => throw new InvalidOperationException($"Unknown direction {state.Direction} in {nameof(MoveForward)} of {nameof(Runtime)}")
        };

        if (!Validator.IsValidCoordinate(file.Grid, x, y))
            return RuntimeError.OutOfBounds(new RobotState(x, y, state.Direction));;

        if (file.Obstacles.Contains(new Obstacle(x, y)))
            return RuntimeError.Crashed(new RobotState(x, y, state.Direction));
            
        return state with { X = x, Y = y };
    }

    private static Either<RuntimeError, RobotState> TravelOne(ValidatedFile file, RobotJourney journey, IRuntimeLog? log = null)
    {
        log?.LogJourneyStart(file, journey);
            
        // check if we start from obstacle
        if (file.Obstacles.Contains(new Obstacle(journey.InitialState.X, journey.InitialState.Y)))
        {
            var runtimeError = RuntimeError.Crashed(journey.InitialState);
            log?.LogState(file, runtimeError);
            return runtimeError;
        }
            
        var initial = Either<RuntimeError, RobotState>.Right(journey.InitialState);
        log?.LogState(file, initial);
            
        var simulation = journey.Commands.FoldM(initial, (state, cmd) => LogState(Step(file, state, cmd)));
            
        var simulationOutcome = simulation.Bind(actualFinalState => 
            actualFinalState == journey.ExpectedFinalState
                ? Either<RuntimeError, RobotState>.Right(actualFinalState)
                : RuntimeError.UnexpectedFinalState(actualFinalState));
            
        log?.LogJourneyEnd(file, simulationOutcome);

        return simulationOutcome;
            
        Either<RuntimeError, RobotState> LogState(Either<RuntimeError, RobotState> state)
        {
            log?.LogState(file, state);
            return state;
        }
    }
        
    public static Lst<Either<RuntimeError, RobotState>> TravelAll(ValidatedFile file, IRuntimeLog? log = null)
    {
        return toList(file.Journeys).Map(journey => TravelOne(file, journey, log));
    }

    /// <summary>
    /// Either<L,R> specific foldM implementation, short circuits on Left
    /// </summary>
    private static Either<L, R> FoldM<L, R, A>(
        this Lst<A> list, 
        Either<L, R> initial, 
        Func<R, A, Either<L, R>> folder)
    {
        if (initial.IsLeft)
            return initial;
            
        foreach (var item in list)
        {
            initial = initial.Bind(r => folder(r, item));
            if (initial.IsLeft)
                return initial;
        }
            
        return initial;
    }
}