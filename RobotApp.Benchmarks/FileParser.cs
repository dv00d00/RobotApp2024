using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace RobotApp.Dirty;

public enum Direction { N, E, S, W }
public enum Command { L, R, F }

public record Grid(int Width, int Height);
public record struct Obstacle(int X, int Y);
public record RobotState(int X, int Y, Direction Direction);
public record RobotJourney(RobotState InitialState, List<Command> Commands, RobotState ExpectedFinalState);
public record ParsedFile(Grid Grid, List<Obstacle> Obstacles, List<RobotJourney> Journeys);

public class FileParser(PipeReader pipeReader, StateMachineParser stateMachine)
{
    public async Task<Result<ParsedFile>> ParseAsync()
    {
        bool bom = false;
        int lineNumber = 0;
        char[] charBuffer = ArrayPool<char>.Shared.Rent(1024);
        
        try
        {
            while (true)
            {
                var result = await pipeReader.ReadAsync();
                var buffer = result.Buffer;
                if (!bom)
                {
                    if (buffer.FirstSpan.StartsWith(Encoding.UTF8.Preamble))
                    {
                        buffer = buffer.Slice(Encoding.UTF8.Preamble.Length);
                    }
                    bom = true;
                }

                while (TryReadLine(ref buffer, charBuffer, out var lineSpan))
                {
                    lineNumber++;
                    var error = stateMachine.AcceptLine(lineNumber, lineSpan);
                    if (error != null)
                    {
                        return Result.Failure<ParsedFile>(error);
                    }
                }

                pipeReader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                {
                    var unread = new SequenceReader<byte>(buffer).UnreadSequence;
                    if (unread.Length > 0 && unread.Length < 1024)
                    {
                        int written = Encoding.UTF8.GetChars(unread, charBuffer);

                        lineNumber++;
                        var error = stateMachine.AcceptLine(lineNumber, charBuffer.AsSpan(0, written));
                        if (error != null)
                        {
                            return Result.Failure<ParsedFile>(error);
                        }
                        error = stateMachine.EOF(lineNumber);
                        if (error != null)
                        {
                            return Result.Failure<ParsedFile>(error);
                        }
                    }

                    break;
                }
            }

            return stateMachine.FinalizeParsing();
        }
        finally
        {
            ArrayPool<char>.Shared.Return(charBuffer);
        }
    }

    private bool TryReadLine(
        ref ReadOnlySequence<byte> buffer,
        Span<char> lineBuffer,
        out ReadOnlySpan<char> line)
    {
        var reader = new SequenceReader<byte>(buffer);

        if (reader.TryReadTo(out ReadOnlySequence<byte> lineBytes, (byte)'\n', advancePastDelimiter: true))
        {
            if (lineBytes.Length > lineBuffer.Length)
            {
                line = default;
                return false;
            }

            int written = Encoding.UTF8.GetChars(lineBytes, lineBuffer);
            line = lineBuffer.Slice(0, written).TrimEnd('\r');
            buffer = buffer.Slice(reader.Position);
            return true;
        }

        line = default;
        return false;
    }
}

public record Error
{
    public string Message { get; }
    public int LineNumber { get; }

    public Error(string message, int lineNumber)
    {
        Message = message;
        LineNumber = lineNumber;
    }
}

public class StateMachineParser 
{
    private ParserState _currentState = ParserState.GRID;
    private Grid? _grid;
    private readonly List<Obstacle> _obstacles = new();
    private RobotState? _currentJourneyStartState;
    private readonly List<Command> _currentJourneyCommands = new();
    private readonly List<RobotJourney> _journeys = new();
    private Error? _error;

    public Error? AcceptLine(int lineNumber, ReadOnlySpan<char> line)
    {
        if (line.IsEmpty)
            return null;

        try
        {
            switch (_currentState)
            {
                case ParserState.ERROR:
                    return _error;
                
                case ParserState.SUCCESS:
                    return null;
                    
                case ParserState.GRID:
                    if (line.StartsWith("GRID"))
                    {
                        if (TryParseGrid(line, out var grid))
                        {
                            _grid = grid;
                            _currentState = ParserState.OBSTACLE;
                        }
                        else
                        {
                            return OnError(new Error("Invalid GRID format.", lineNumber));
                        }
                    }
                    else
                    {
                        return OnError(new Error("Expected GRID line.", lineNumber));
                    }
                    break;

                case ParserState.OBSTACLE:
                    if (line.StartsWith("OBSTACLE"))
                    {
                        if (TryParseObstacle(line, out var obstacle))
                        {
                            _obstacles.Add(obstacle);
                        }
                        else
                        {
                            return OnError(new Error("Invalid OBSTACLE format.", lineNumber));
                        }
                    }
                    else if (char.IsDigit(line[0]))
                    {
                        if (TryParseRobotState(line, out var robotState))
                        {
                            _currentJourneyStartState = robotState;
                            _currentState = ParserState.JOURNEY_COMMANDS;
                        }
                        else
                        {
                            return OnError(new Error("Invalid RobotState format.", lineNumber));
                        }
                    }
                    else
                    {
                        return OnError(new Error("Unexpected line in OBSTACLE state.", lineNumber));
                    }
                    break;

                case ParserState.JOURNEY_COMMANDS:
                    if (TryParseCommands(line, out var commands))
                    {
                        _currentJourneyCommands.AddRange(commands);
                        _currentState = ParserState.JOURNEY_STATE;
                    }
                    else
                    {
                        return OnError(new Error("Invalid commands format.", lineNumber));
                    }
                    break;

                case ParserState.JOURNEY_STATE:
                    if (TryParseRobotState(line, out var finalState))
                    {
                        var journey = new RobotJourney(
                            _currentJourneyStartState!,
                            [.._currentJourneyCommands],
                            finalState);

                        _journeys.Add(journey);
                        _currentJourneyCommands.Clear();
                        _currentState = ParserState.OBSTACLE;
                    }
                    else
                    {
                        return OnError(new Error("Invalid final RobotState format.", lineNumber));
                    }
                    break;

                default:
                    return OnError(new Error($"Unknown parser state: {_currentState}", lineNumber));
            }
        }
        catch (Exception ex)
        {
            return OnError(new Error($"Unexpected error: {ex.Message}", lineNumber));
        }

        return null;

        Error OnError(Error e)
        {
            _error = e;
            _currentState = ParserState.ERROR;
            return e;
        }
    }

    public Error? EOF(int lineNumber)
    {
        if (_currentState != ParserState.OBSTACLE)
        {
            return new Error("Unexpected end of file.", lineNumber);
        }

        _currentState = ParserState.SUCCESS;
        return null;
    }
    
    public Result<ParsedFile> FinalizeParsing()
    {
        if (_currentState == ParserState.SUCCESS && _error == null)
        {
            return Result.Success(new ParsedFile(_grid!, _obstacles, _journeys));
        }

        return Result.Failure<ParsedFile>(new Error("Parsing incomplete or ended in an invalid state.", 0));
    }

    private bool TryParseObstacle(ReadOnlySpan<char> line, out Obstacle result)
    {
        var rest = line[9..];
        var parts = rest.Split(' ');

        int count = 0;
        ReadOnlySpan<char> xSpan = default;
        ReadOnlySpan<char> ySpan = default;

        foreach (var part in parts)
        {
            count++;
            switch (count)
            {
                case 1:
                    xSpan = rest[part];
                    break;
                case 2:
                    ySpan = rest[part];
                    break;
                default:
                    result = default;
                    // errorCode = "Invalid OBSTACLE format: too many parts.";
                    return false;
            }
        }

        if (count == 2 && int.TryParse(xSpan, out var x) && int.TryParse(ySpan, out var y))
        {
            result = new Obstacle(x, y);
            return true;
        }

        result = default;
        // errorCode = "Invalid OBSTACLE format: failed to parse coordinates.";
        return false;
    }

    private bool TryParseGrid(ReadOnlySpan<char> line, out Grid result)
    {
        var rest = line[5..];
        var parts = rest.Split('x');

        int count = 0;
        ReadOnlySpan<char> widthSpan = default;
        ReadOnlySpan<char> heightSpan = default;

        foreach (var part in parts)
        {
            count++;
            switch (count)
            {
                case 1:
                    widthSpan = rest[part];
                    break;
                case 2:
                    heightSpan = rest[part];
                    break;
                default:
                    result = default;
                    // errorCode = "Invalid GRID format: too many parts.";
                    return false;
            }
        }

        if (count == 2 && int.TryParse(widthSpan, out var width) && int.TryParse(heightSpan, out var height))
        {
            result = new Grid(width, height);
            // errorCode = null;
            return true;
        }

        result = default;
        // errorCode = "Invalid GRID format: failed to parse dimensions.";
        return false;
    }

    private bool TryParseRobotState(ReadOnlySpan<char> line, out RobotState result)
    {
        var parts = line.Split(' ');

        int count = 0;
        ReadOnlySpan<char> xSpan = default;
        ReadOnlySpan<char> ySpan = default;
        ReadOnlySpan<char> directionSpan = default;

        foreach (var part in parts)
        {
            count++;
            switch (count)
            {
                case 1:
                    xSpan = line[part];
                    break;
                case 2:
                    ySpan = line[part];
                    break;
                case 3:
                    directionSpan = line[part];
                    break;
                default:
                    result = default;
                    // errorCode = "Invalid RobotState format: too many parts.";
                    return false;
            }
        }

        if (count == 3 &&
            int.TryParse(xSpan, out var x) &&
            int.TryParse(ySpan, out var y) &&
            TryParseDirection(directionSpan[0], out var direction))
        {
            result = new RobotState(x, y, direction);
            // errorCode = null;
            return true;
        }

        result = default;
        // errorCode = "Invalid RobotState format: failed to parse state.";
        return false;
    }

    private bool TryParseCommands(ReadOnlySpan<char> line, out List<Command> result)
    {
        var commands = new List<Command>();

        foreach (var c in line)
        {
            if (TryParseCommand(c, out var command))
            {
                commands.Add(command);
            }
            else
            {
                result = null;
                // errorCode = $"Invalid command: {commandErrorCode}";
                return false;
            }
        }

        result = commands;
        // errorCode = null;
        return true;
    }

    private bool TryParseDirection(char c, out Direction result)
    {
        switch (c)
        {
            case 'N':
                result = Direction.N;
                return true;
            case 'E':
                result = Direction.E;
                return true;
            case 'S':
                result = Direction.S;
                return true;
            case 'W':
                result = Direction.W;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private bool TryParseCommand(char c, out Command result)
    {
        switch (c)
        {
            case 'L':
                result = Command.L;
                return true;
            case 'R':
                result = Command.R;
                return true;
            case 'F':
                result = Command.F;
                return true;
            default:
                result = default;
                return false;
        }
    }
    
    enum ParserState
    {
        GRID,
        OBSTACLE,
        JOURNEY_STATE,
        JOURNEY_COMMANDS,
    
        ERROR,
        SUCCESS
    }
}

public readonly record struct Result<T>
{
    public T Value { get; }
    public Error Error { get; }
    public bool IsSuccess => Error == null!;

    private Result(T value)
    {
        Value = value;
        Error = null!;
    }
    
    private Result(Error error)
    {
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(Error error) => new(error);
}

public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}
