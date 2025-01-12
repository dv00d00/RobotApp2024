using LanguageExt;
using LanguageExt.Parsec;

namespace RobotApp.Logic;

public static class Parser
{
    static Parser<Unit> spaces1 => Prim.skipMany1(Char.ch(' '));

    static Parser<int> number =>
        from x in Prim.many1(Char.digit)
        from n in Prelude.parseInt(new string(x.ToArray()), 10).Match(
            Some: Prim.result,
            None: () => Prim.failure<int>("Invalid decimal value"))
        select n;

    internal static Parser<Grid> ParseGrid =>
        from _key in Char.str("GRID").label($"GRID keyword [{nameof(ParseGrid)}]")
        from _ws1 in spaces1.label($"at least one space after GRID keyword [{nameof(ParseGrid)}]")
        from width in number.label($"grid width [{nameof(ParseGrid)}]")
        from _x in Char.ch('x').label($"'x' between grid width and height [{nameof(ParseGrid)}]")
        from height in number.label($"grid height [{nameof(ParseGrid)}]")
        from _nl1 in Prim.optional(Char.endOfLine)
        select new Grid(width, height);

    internal static Parser<Obstacle> ParseObstacle =>
        from _key in Char.str("OBSTACLE").label($"OBSTACLE keyword [{nameof(ParseObstacle)}]")
        from _ws1 in spaces1.label($"at least one space after OBSTACLE keyword [{nameof(ParseObstacle)}]")
        from x in number.label($"obstacle X coordinate [{nameof(ParseObstacle)}]")
        from _ws2 in spaces1.label($"at least one space after obstacle X coordinate [{nameof(ParseObstacle)}]")
        from y in number.label($"obstacle Y coordinate [{nameof(ParseObstacle)}]")
        from _nl1 in Prim.optional(Char.endOfLine)
        select new Obstacle(x, y);

    internal static Parser<Direction> ParseDirection =>
        Prim.choice(
            Char.ch('N').Map(_ => Direction.N),
            Char.ch('E').Map(_ => Direction.E),
            Char.ch('S').Map(_ => Direction.S),
            Char.ch('W').Map(_ => Direction.W)
        ).label("direction, one of [N, E, S, W]");

    internal static Parser<Command> ParseCommand =>
        Prim.choice(
            Char.ch('L').Map(_ => Command.L),
            Char.ch('R').Map(_ => Command.R),
            Char.ch('F').Map(_ => Command.F)
        ).label("command, one of [L, R, F]");

    internal static Parser<RobotState> ParseRobotState =>
        from x in number.label($"robot X coordinate [{nameof(ParseRobotState)}]")
        from _ws1 in spaces1.label($"at least one space after robot X coordinate [{nameof(ParseRobotState)}]")
        from y in number.label($"robot Y coordinate [{nameof(ParseRobotState)}]")
        from _ws2 in spaces1.label($"at least one space after robot Y coordinate [{nameof(ParseRobotState)}]")
        from direction in ParseDirection.label($"robot direction [{nameof(ParseRobotState)}]")
        select new RobotState(x, y, direction);

    internal static Parser<RobotJourney> ParseJourney =>
        from _ws1 in Char.spaces
        from initialState in ParseRobotState.label($"initial robot state [{nameof(ParseJourney)}]")
        from _nl1 in Char.endOfLine.label($"newline after initial state [{nameof(ParseJourney)}]")
        from commands in Prim.many1(ParseCommand).label($"robot commands [{nameof(ParseJourney)}]")
        from _nl2 in Char.endOfLine.label($"newline after commands [{nameof(ParseJourney)}]")
        from finalState in ParseRobotState.label($"final robot state [{nameof(ParseJourney)}]")
        from _nl3 in Prim.optional(Char.endOfLine)
        select new RobotJourney(initialState, commands.Freeze(), finalState);

    internal static Parser<ParsedFile> ParseFile =>
        from grid in ParseGrid.label($"grid definition [{nameof(ParseFile)}]")
        from _s1 in Char.spaces
        from obstacles in Prim.many(ParseObstacle).label($"obstacles list [{nameof(ParseFile)}]")
        from _s2 in Char.spaces
        from journeys in Prim.many(ParseJourney).label($"journeys list [{nameof(ParseFile)}]")
        from _s3 in Char.spaces
        from _eof in Prim.eof.label($"end of file [{nameof(ParseFile)}]")
        select new ParsedFile(grid, obstacles.Freeze(), journeys.Freeze());
        
    public static Either<Error, ParsedFile> ParseInput(string input) =>
        ParseFile.Parse(input).ToEither().MapLeft(str => new ParserError(str) as Error);
}