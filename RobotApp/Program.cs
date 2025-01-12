using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Parsec;
using RobotApp;
using static LanguageExt.Prelude;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Parsec.Char;

[assembly: InternalsVisibleTo("RobotApp.Tests")]

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

        internal static Parser<Grid> ParseGrid =>
            from _key in str("GRID").label($"GRID keyword [{nameof(ParseGrid)}]")
            from _ws1 in spaces1.label($"at least one space after GRID keyword [{nameof(ParseGrid)}]")
            from width in number.label($"grid width [{nameof(ParseGrid)}]")
            from _x in ch('x').label($"'x' between grid width and height [{nameof(ParseGrid)}]")
            from height in number.label($"grid height [{nameof(ParseGrid)}]")
            from _nl1 in optional(endOfLine)
            select new Grid(width, height);

        internal static Parser<Obstacle> ParseObstacle =>
            from _key in str("OBSTACLE").label($"OBSTACLE keyword [{nameof(ParseObstacle)}]")
            from _ws1 in spaces1.label($"at least one space after OBSTACLE keyword [{nameof(ParseObstacle)}]")
            from x in number.label($"obstacle X coordinate [{nameof(ParseObstacle)}]")
            from _ws2 in spaces1.label($"at least one space after obstacle X coordinate [{nameof(ParseObstacle)}]")
            from y in number.label($"obstacle Y coordinate [{nameof(ParseObstacle)}]")
            from _nl1 in optional(endOfLine)
            select new Obstacle(x, y);

        internal static Parser<Direction> ParseDirection =>
            choice(
                ch('N').Map(_ => Direction.N),
                ch('E').Map(_ => Direction.E),
                ch('S').Map(_ => Direction.S),
                ch('W').Map(_ => Direction.W)
            ).label("direction, one of [N, E, S, W]");

        internal static Parser<Command> ParseCommand =>
            choice(
                ch('L').Map(_ => Command.L),
                ch('R').Map(_ => Command.R),
                ch('F').Map(_ => Command.F)
            ).label("command, one of [L, R, F]");

        internal static Parser<RobotState> ParseRobotState =>
            from x in number.label($"robot X coordinate [{nameof(ParseRobotState)}]")
            from _ws1 in spaces1.label($"at least one space after robot X coordinate [{nameof(ParseRobotState)}]")
            from y in number.label($"robot Y coordinate [{nameof(ParseRobotState)}]")
            from _ws2 in spaces1.label($"at least one space after robot Y coordinate [{nameof(ParseRobotState)}]")
            from direction in ParseDirection.label($"robot direction [{nameof(ParseRobotState)}]")
            select new RobotState(x, y, direction);

        internal static Parser<RobotJourney> ParseJourney =>
            from _ws1 in spaces
            from initialState in ParseRobotState.label($"initial robot state [{nameof(ParseJourney)}]")
            from _nl1 in endOfLine.label($"newline after initial state [{nameof(ParseJourney)}]")
            from commands in many1(ParseCommand).label($"robot commands [{nameof(ParseJourney)}]")
            from _nl2 in endOfLine.label($"newline after commands [{nameof(ParseJourney)}]")
            from finalState in ParseRobotState.label($"final robot state [{nameof(ParseJourney)}]")
            from _nl3 in optional(endOfLine)
            select new RobotJourney(initialState, commands.Freeze(), finalState);

        internal static Parser<ParsedFile> ParseFile =>
            from grid in ParseGrid.label($"grid definition [{nameof(ParseFile)}]")
            from _s1 in spaces
            from obstacles in many(ParseObstacle).label($"obstacles list [{nameof(ParseFile)}]")
            from _s2 in spaces
            from journeys in many(ParseJourney).label($"journeys list [{nameof(ParseFile)}]")
            from _s3 in spaces
            from _eof in eof.label($"end of file [{nameof(ParseFile)}]")
            select new ParsedFile(grid, obstacles.Freeze(), journeys.Freeze());
        
        public static Either<Error, ParsedFile> ParseInput(string input) =>
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

            if (!Validator.IsValidCoordinates(file.Grid, x, y))
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

    public static class CompositionRoot
    {
        public static System.Collections.Generic.IReadOnlyCollection<string> Execute(string input, Runtime.IRuntimeLog? gridVisualiser = null)
        {
            // parsing, validation and execution
            var result = 
                from parsedFile in Parser.ParseInput(input)
                from validatedFile in Validator.ValidateParsedFile(parsedFile)
                select Runtime.TravelAll(validatedFile, gridVisualiser);
            
            // interpretation
            var results = result.Match(
                    Left: error =>
                    {
                        return error switch
                        {
                            ParserError pe => List($"Parsing: {pe.Message}"),
                            ValidationErrors ve => ve.Errors.Map(e => $"Validation: {e.Message}"),
                            _ => List($"Unknown: {error}"),
                        };
                    },
                    Right: runs => runs.Map(run => run.Match(
                        Left: runtimeError =>
                        {
                            return runtimeError.Kind switch
                            {
                                RuntimeErrorType.OutOfBounds =>
                                    "OUT OF BOUNDS",

                                RuntimeErrorType.Crashed =>
                                    $"CRASHED {runtimeError.State.X} {runtimeError.State.Y}",

                                RuntimeErrorType.UnexpectedFinalState =>
                                    $"FAILURE {runtimeError.State.X} {runtimeError.State.Y} {runtimeError.State.Direction}",

                                _ => throw new InvalidOperationException(
                                    $"Unknown runtime error {runtimeError.Kind} in {nameof(Execute)} of {nameof(CompositionRoot)}")
                            };
                        },
                        Right: state => $"SUCCESS {state.X} {state.Y} {state.Direction}")));

            // output
            return results;
        }
    }
}

public record struct FileName(string Value);
public record struct FileContent(string Value);
public record LoadedFile(FileName Path, FileContent Content);
public record Inputs(LoadedFile File, bool Visualise);

public static class CommandLineParser
{
    public static async Task<Either<string, Inputs>> ParseArgs(string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
            return "Usage: RobotApp.exe <InputFile> [--visualise|-v]";

        var tryLoadFile = await LoadFile(args[0]);

        var tryParseVisualiseFlag = args.Length == 2
            ? ParseVisualiseFlag(args[1])
            : Right(false);

        return from file in tryLoadFile
            from vis in tryParseVisualiseFlag
            select new Inputs(file, vis);
    }

    private static async Task<Either<string, LoadedFile>> LoadFile(string path)
    {
        try
        {
            var fileName = Path.GetFileName(path);
            var contents = await File.ReadAllTextAsync(path);
            return Right(new LoadedFile(new FileName(fileName), new FileContent(contents)));
        }
        catch (FileNotFoundException)
        {
            return Left<string, LoadedFile>($"Error: File not found at path '{path}'.");
        }
        catch (UnauthorizedAccessException)
        {
            return Left<string, LoadedFile>($"Error: Access to the file at '{path}' is denied.");
        }
        catch (IOException e)
        {
            return Left<string, LoadedFile>($"Error: An I/O error occurred while reading the file: {e.Message}");
        }
        catch (Exception e)
        {
            return Left<string, LoadedFile>($"Error: {e.Message}");
        }
    }

    private static Either<string, bool> ParseVisualiseFlag(string flag) =>
        flag.ToLower() switch
        {
            "--visualise" => Right(true),
            "-v" => Right(true),
            _ => Left($"Error: Invalid flag '{flag}'. Did you mean '--visualise'?")
        };
}

public static class Program
{
    public static async Task Main(string[] args)
    {
        var maybeInputs = await CommandLineParser.ParseArgs(args);
        maybeInputs.Match(
            Right: Run,
            Left: error =>
            {
                Console.WriteLine(error);
                Environment.Exit(1);
            }
        );
    }

    private static void Run(Inputs inputs)
    {
        Console.Write($"Processing file {inputs.File.Path.Value}");
        if (inputs.Visualise)
        {
            Console.Write(" with visualisation");
        }
        Console.WriteLine();
        
        var visualiser = inputs.Visualise ? new AsciiGridRuntimeLog() : null; 
        var output = CompositionRoot.Execute(inputs.File.Content.Value, visualiser);

        if (visualiser != null)
        {
            foreach (var trace in visualiser.Output)
            {
                Console.WriteLine(trace);
            }
        }

        foreach (var line in output)
        {
            Console.WriteLine(line);
        }
    }

    internal class AsciiGridRuntimeLog : Runtime.IRuntimeLog
    {
        private readonly System.Collections.Generic.List<string> _output = new();
        public System.Collections.Generic.IReadOnlyCollection<string> Output => _output;

        public void LogJourneyStart(ValidatedFile file, RobotJourney journey)
        {
            Log($"== Starting Journey from {journey.InitialState} to {journey.ExpectedFinalState} ==");
        }

        public void LogJourneyEnd(ValidatedFile file, Either<RuntimeError, RobotState> finalState)
        {            
            Log("========= Final state ===========");
            
            finalState.Match(
                Left: VisualiseError,
                Right: state => Log($"[SUCCESS {state.X} {state.Y} {state.Direction}]"));
            
            Log("========= End of Journey ===========");
        }

        public void LogState(ValidatedFile file, Either<RuntimeError, RobotState> currentState)
        {
            currentState.Match(Left: VisualiseError, Right: validState => VisualiseState(file, validState));
        }

        private void VisualiseError(RuntimeError err)
        {
            switch (err.Kind)
            {
                case RuntimeErrorType.OutOfBounds:
                    Log("[OUT OF BOUNDS]");
                    break;
                case RuntimeErrorType.Crashed:
                    Log($"[CRASHED {err.State.X} {err.State.Y}]");
                    break;
                case RuntimeErrorType.UnexpectedFinalState:
                    Log($"[FAILURE {err.State.X} {err.State.Y} {err.State.Direction}]");
                    break;
            }
        }

        private void VisualiseState(ValidatedFile file, RobotState state)
        {
            // pool?
            var sb = new StringBuilder();
            const int viewRange = 2;

            int startX = Math.Max(0, state.X - viewRange);
            int endX = Math.Min(file.Grid.Width - 1, state.X + viewRange);
            int startY = Math.Max(0, state.Y - viewRange);
            int endY = Math.Min(file.Grid.Height - 1, state.Y + viewRange);

            for (int y = endY; y >= startY; y--)
            {
                sb.Append("  ");
                for (int x = startX; x <= endX; x++)
                {
                    sb.Append("+---");
                }
                sb.AppendLine("+");

                sb.Append($"{y:D2}");
                for (int x = startX; x <= endX; x++)
                {
                    sb.Append("|");
                    if (state.X == x && state.Y == y)
                        sb.Append($" {GetDirectionSymbol(state.Direction)} ");
                    else if (file.Obstacles.Contains(new Obstacle(x, y)))
                        sb.Append(" O ");
                    else
                        sb.Append("   ");
                }
                sb.AppendLine("|");
            }

            sb.Append("  ");
            for (int x = startX; x <= endX; x++)
            {
                sb.Append("+---");
            }
            sb.AppendLine("+");

            sb.Append("    ");
            for (int x = startX; x <= endX; x++)
            {
                sb.Append($"{x:D2}  ");
            }
            sb.AppendLine();

            Log(sb.ToString());
        }
        
        private static string GetDirectionSymbol(Direction direction) =>
            direction switch
            {
                Direction.N => "^",
                Direction.E => ">",
                Direction.S => "v",
                Direction.W => "<",
                _ => "?"
            };
        
        private void Log(string line) => _output.Add(line);
    }
}

// open questions
// primitive obsession: maybe due to problem scope working with bounded context dependent numbers (x,y) in grid
// how many monads is too many? I've tried using reader for injecting visualiser, it was painful
// null vs noop visualiser?
// is starting on an obstacle a validation issue or a runtime outcome?

// todo: add tests
// todo: grid dependant coordinates
// todo: stream based parsing
// todo: command list optimization LLLL -> empty, LLL -> R etc