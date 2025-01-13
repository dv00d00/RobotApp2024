using LanguageExt;
using LanguageExt.Parsec;
using static LanguageExt.Parsec.Char;
using static LanguageExt.Parsec.Prim;
using static LanguageExt.Prelude;

namespace RobotApp.Logic;

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