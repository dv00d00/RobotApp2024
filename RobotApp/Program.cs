using System;
using System.IO;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Parsec;
using RobotApp;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;

namespace RobotApp
{
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

    public static class Parser
    {
        static Parser<Unit> spaces1 => skipMany1(ch(' '));

        static Parser<int> number =>
            from x in many1(digit)
            from n in parseInt(new string(x.ToArray()), 10).Match(
                Some: result,
                None: () => failure<int>("Invalid decimal value"))
            select n;

        static Parser<Grid> ParseGrid =>
            from _key in str("GRID").label($"GRID keyword [{nameof(ParseGrid)}]")
            from _ws1 in spaces1.label($"at least one space after GRID keyword [{nameof(ParseGrid)}]")
            from width in number.label($"grid width [{nameof(ParseGrid)}]")
            from _x in ch('x').label($"'x' between grid width and height [{nameof(ParseGrid)}]")
            from height in number.label($"grid height [{nameof(ParseGrid)}]")
            from _nl1 in optional(endOfLine)
            select new Grid(width, height);

        static Parser<Obstacle> ParseObstacle =>
            from _key in str("OBSTACLE").label($"OBSTACLE keyword [{nameof(ParseObstacle)}]")
            from _ws1 in spaces1.label($"at least one space after OBSTACLE keyword [{nameof(ParseObstacle)}]")
            from x in number.label($"obstacle X coordinate [{nameof(ParseObstacle)}]")
            from _ws2 in spaces1.label($"at least one space after obstacle X coordinate [{nameof(ParseObstacle)}]")
            from y in number.label($"obstacle Y coordinate [{nameof(ParseObstacle)}]")
            from _nl1 in optional(endOfLine)
            select new Obstacle(x, y);

        static Parser<Direction> ParseDirection =>
            choice(
                ch('N').Map(_ => Direction.N),
                ch('E').Map(_ => Direction.E),
                ch('S').Map(_ => Direction.S),
                ch('W').Map(_ => Direction.W)
            ).label("direction, one of [N, E, S, W]");

        static Parser<Command> ParseCommand =>
            choice(
                ch('L').Map(_ => Command.L),
                ch('R').Map(_ => Command.R),
                ch('F').Map(_ => Command.F)
            ).label("command, one of [L, R, F]");

        static Parser<RobotState> ParseRobotState =>
            from x in number.label($"robot X coordinate [{nameof(ParseRobotState)}]")
            from _ws1 in spaces1.label($"at least one space after robot X coordinate [{nameof(ParseRobotState)}]")
            from y in number.label($"robot Y coordinate [{nameof(ParseRobotState)}]")
            from _ws2 in spaces1.label($"at least one space after robot Y coordinate [{nameof(ParseRobotState)}]")
            from direction in ParseDirection.label($"robot direction [{nameof(ParseRobotState)}]")
            select new RobotState(x, y, direction);

        static Parser<RobotJourney> ParseJourney =>
            from _ws1 in spaces
            from initialState in ParseRobotState.label($"initial robot state [{nameof(ParseJourney)}]")
            from _nl1 in endOfLine.label($"newline after initial state [{nameof(ParseJourney)}]")
            from commands in many1(ParseCommand).label($"robot commands [{nameof(ParseJourney)}]")
            from _nl2 in endOfLine.label($"newline after commands [{nameof(ParseJourney)}]")
            from finalState in ParseRobotState.label($"final robot state [{nameof(ParseJourney)}]")
            from _nl3 in optional(endOfLine)
            select new RobotJourney(initialState, commands.Freeze(), finalState);

        static Parser<ParsedFile> ParseFile =>
            from grid in ParseGrid.label($"grid definition [{nameof(ParseFile)}]")
            from _s1 in spaces
            from obstacles in many(ParseObstacle).label($"obstacles list [{nameof(ParseFile)}]")
            from _s2 in spaces
            from journeys in many(ParseJourney).label($"journeys list [{nameof(ParseFile)}]")
            from _s3 in spaces
            from _eof in eof.label($"end of file [{nameof(ParseFile)}]")
            select new ParsedFile(grid, obstacles.Freeze(), journeys.Freeze());
        
        public static Either<Error, ParsedFile> Parse(string input) =>
            ParseFile.Parse(input).ToEither().MapLeft(str => new ParserError(str) as Error);
    }

    public static class Validator   
    {
        public static bool IsValidCoordinates(Grid grid, int x, int y) =>
            x >= 0 && x < grid.Width && y >= 0 && y < grid.Height;

        static Validation<ValidationError, Grid> ValidateGrid(Grid grid) =>
            grid is { Height: > 0, Width: > 0 }
                ? grid
                : ValidationError.InvalidGrid(grid);

        static Validation<ValidationError, RobotState> ValidateState(Grid grid, RobotState state, bool initial) => 
            IsValidCoordinates(grid, state.X, state.Y) 
                ? state
                : ValidationError.RobotStateOutOfBounds(state, grid, initial);

        static Validation<ValidationError, RobotJourney> ValidateJourney(Grid grid, RobotJourney journey)
        {
            return (ValidateState(grid, journey.InitialState, true), 
                    ValidateState(grid, journey.ExpectedFinalState, false))
            .Apply((_,_) => journey);
            // Apply(() => journey); does not perform applicative invocation >:(
        }

        static Validation<ValidationError, Obstacle> ValidateObstacle(Grid grid, Obstacle obstacle) =>
            IsValidCoordinates(grid, obstacle.X, obstacle.Y)
                ? obstacle
                : ValidationError.ObstacleOutOfBounds(obstacle, grid);

        static Validation<ValidationError, ValidatedFile> ValidateParsedFileM(ParsedFile parsedFile) =>
            from grid in ValidateGrid(parsedFile.Grid)
            from obstacles in parsedFile.Obstacles.Map(o => ValidateObstacle(parsedFile.Grid, o)).Sequence()
            from journeys in parsedFile.Journeys.Map(j => ValidateJourney(parsedFile.Grid, j)).Sequence()
            select new ValidatedFile(parsedFile.Grid, toHashSet(obstacles), journeys);
        
        static Validation<ValidationError, ValidatedFile> ValidateParsedFileA(ParsedFile parsedFile) =>
            ValidateGrid(parsedFile.Grid)
                .Bind(grid =>
                {
                    var maybeObstacles = parsedFile.Obstacles.Sequence(o => ValidateObstacle(grid, o));
                    var maybeJourneys = parsedFile.Journeys.Sequence(j => ValidateJourney(grid, j));

                    var validatedFile = (maybeObstacles, maybeJourneys)
                        .Apply((obstacles, journeys) => new ValidatedFile(grid, toHashSet(obstacles), journeys));
                    
                    return validatedFile;
                });

        public static Either<Error, ValidatedFile> ValidateParsedFile(ParsedFile parsedFile)
        {
            var validateParsedFileA = ValidateParsedFileA(parsedFile);
            return validateParsedFileA
                .ToEither()
                .MapLeft(errs => new ValidationErrors(toList(errs)) as Error);
        }
    }

    public static class Runtime
    {
        private static Either<RuntimeError, RobotState> Step(ValidatedFile file, RobotState state, Command command) =>
            command switch
            {
                Command.L => state with { Direction = TurnLeft(state.Direction) },
                Command.R => state with { Direction = TurnRight(state.Direction) },
                Command.F => MoveForward(file, state),
                _ => throw new InvalidOperationException($"Unknown command {command} in {nameof(Step)} of {nameof(Runtime)}")
            };

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

            if (!Validator.IsValidCoordinates(file.Grid, x, y))
                return RuntimeError.OutOfBounds(new RobotState(x, y, state.Direction));;

            if (file.Obstacles.Contains(new Obstacle(x, y)))
                return RuntimeError.Crashed(new RobotState(x, y, state.Direction));
            
            return state with { X = x, Y = y };
        }

        private static Either<RuntimeError, RobotState> TravelOne(ValidatedFile file, RobotJourney journey)
        {
            // check if we start from obstacle
            
            if (file.Obstacles.Contains(new Obstacle(journey.InitialState.X, journey.InitialState.Y)))
                return RuntimeError.Crashed(journey.InitialState);
            
            var initial = Either<RuntimeError, RobotState>.Right(journey.InitialState);
            
            var simulation = journey.Commands
                .Fold(initial, (state, cmd) => state.Bind(prev => Step(file, prev, cmd)));
            // can't find FoldM 
            // todo: custom implementation for early exit ?

            var simulationOutcome = simulation.Bind(actualFinalState => 
                actualFinalState == journey.ExpectedFinalState
                    ? Either<RuntimeError, RobotState>.Right(actualFinalState)
                    : RuntimeError.UnexpectedFinalState(actualFinalState));

            return simulationOutcome;
        }
        
        public static Lst<Either<RuntimeError, RobotState>> TravelAll(ValidatedFile file) => 
            toList(file.Journeys).Map(journey => TravelOne(file, journey));
    }

    public static class CompositionRoot
    {
        public static System.Collections.Generic.IReadOnlyCollection<string> Execute(string input)
        {
            // parsing, validation and execution
            var result = 
                from parsedFile in Parser.Parse(input)
                from validatedFile in Validator.ValidateParsedFile(parsedFile)
                select Runtime.TravelAll(validatedFile);
            
            // interpretation
            var results = result.Match(
                    Left: err =>
                    {
                        var errors = err switch
                        {
                            ParserError pe => List($"Parsing: {pe.Message}"),
                            ValidationErrors ve => ve.Errors.Map(e => $"Validation: {e.Message}"),
                            _ => List($"Unknown: {err}"),
                        };
                        
                        return errors;
                    },
                    Right: runs => runs.Map(run => run.Match(
                        Left: err =>
                        {
                            return err.Kind switch
                            {
                                RuntimeErrorType.OutOfBounds => 
                                    "OUT OF BOUNDS",
                                
                                RuntimeErrorType.Crashed => 
                                    $"CRASHED {err.State.X} {err.State.Y}",
                                
                                RuntimeErrorType.UnexpectedFinalState => 
                                    $"FAILURE {err.State.X} {err.State.Y} {err.State.Direction}",
                                
                                _ => throw new InvalidOperationException(
                                    $"Unknown runtime error {err.Kind} in {nameof(Execute)} of {nameof(CompositionRoot)}")
                            };
                        },
                        Right: state => $"SUCCESS {state.X} {state.Y} {state.Direction}")));
            // output
            return results;
        }
    }
}

public static class Program
{
    public static async Task Main()
    {
        var filenames = Directory.EnumerateFiles(@"C:\work\Robot\RobotApp.Tests\", "SampleBad*.txt");

        foreach (var filename in filenames)
        {
            Console.WriteLine();
            Console.WriteLine(Path.GetFileName(filename));
            Console.WriteLine();
            
            var text = await File.ReadAllTextAsync(filename);
            
            var output = CompositionRoot.Execute(text);
            
            foreach (var line in output)
            {
                Console.WriteLine(line);
            }
            
            Console.WriteLine("====================================");
        }
    }
}

// todo: add tests
// todo: grid dependant coordinates
// todo: case when start on obstacle
// todo: stream based parsing
// todo: command list optimization LLLL -> empty, LLL -> R etc