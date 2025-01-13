using LanguageExt;

namespace RobotApp.Logic;

public static class Validated
{
    public record Direction
    {
        private char Value { get; }
        private Direction(char c) { Value = c; }
        
        public static Direction North { get; } = new('N');
        public static Direction East { get; } = new('E');
        public static Direction South { get; } = new('S');
        public static Direction West { get; } = new('W');
        
        public static Option<Direction> Create(RobotApp.Logic.Direction direction) =>
            direction switch
            {
                RobotApp.Logic.Direction.N => Prelude.Some(North),
                RobotApp.Logic.Direction.E => Prelude.Some(East),
                RobotApp.Logic.Direction.S => Prelude.Some(South),
                RobotApp.Logic.Direction.W => Prelude.Some(West),
                _ => Prelude.None
            };
    }

    public record Command
    {
        private char Value { get; }
        private Command(char c) => Value = c;
        
        public static Command TurnLeft { get; } = new('L');
        public static Command TurnRight { get; } = new('R');
        public static Command MoveForward { get; } = new('F');
        
        public static Option<Command> Create(RobotApp.Logic.Command command) =>
            command switch
            {
                RobotApp.Logic.Command.L => Prelude.Some(TurnLeft),
                RobotApp.Logic.Command.R => Prelude.Some(TurnRight),
                RobotApp.Logic.Command.F => Prelude.Some(MoveForward),
                _ => Prelude.None
            };
    }

    public record Width
    {
        public int Value { get; }
        private Width(int value) => Value = value;
        
        public static Option<Width> Create(int value) => 
            value < 1 ? Option<Width>.None : new Width(value);
    }

    public record Height
    {
        public int Value { get; }
        private Height(int value) => Value = value;
        
        public static Option<Height> Create(int value) => 
            value < 1 ? Option<Height>.None : new Height(value);
    }
    
    public record Grid(Width Width, Height Height);

    public record Position
    {
        public int X { get; }
        public int Y { get; }
        
        private Position(int x, int y) => (X, Y) = (x, y);
        
        public static Option<Position> Create(int x, int y, Grid grid)
        {
            if (x < 0 || y < 0)
                return Option<Position>.None;
            if (x >= grid.Width.Value || y >= grid.Height.Value)
                return Option<Position>.None;
            
            return new Position(x, y);
        }
    };
    
    public record RobotState(Position Position, Direction Direction);
    
    public record RobotJourney(RobotState InitialState, Lst<Command> Commands, RobotState ExpectedFinalState);

    public record File
    {
        public Grid Grid { get; }
        public HashSet<Position> Obstacles { get; }
        public Lst<RobotJourney> Journeys { get; }
        
        private File(Grid grid, HashSet<Position> obstacles, Lst<RobotJourney> journeys) => 
            (Grid, Obstacles, Journeys) = (grid, obstacles, journeys);
        
        static Validation<ValidationError, Grid> CreateGrid(RobotApp.Logic.Grid grid) =>
            (
                from width in Width.Create(grid.Width)
                from height in Height.Create(grid.Height)
                select new Grid(width, height)
            )
            .ToValidation(ValidationError.InvalidGrid(grid));
        
        static Option<RobotState> CreateRobotState(RobotApp.Logic.RobotState state, Grid grid) =>
            from pos in Position.Create(state.X, state.Y, grid)
            from dir in Direction.Create(state.Direction)
            select new RobotState(pos, dir);
            
        static Validation<ValidationError, RobotJourney> CreateJourney(RobotApp.Logic.RobotJourney journey, Grid grid)
        {
            var initialState = CreateRobotState(journey.InitialState, grid)
                .ToValidation(ValidationError.RobotStateOutOfBounds(journey.InitialState, grid, true));

            var commands = journey.Commands.Map(it => Command.Create(it)
                .ToValidation(ValidationError.InvalidCommand(it))).Sequence();
            
            var expectedFinalState = CreateRobotState(journey.ExpectedFinalState, grid)
                .ToValidation(ValidationError.RobotStateOutOfBounds(journey.ExpectedFinalState, grid, false));
            
            return (initialState, commands, expectedFinalState).Apply((a, b, c) => new RobotJourney(a, b, c));
        }

        static Validation<ValidationError, HashSet<Position>> CreateObstacles(Lst<Obstacle> obstacles, Grid grid) =>
            obstacles
                .Map(obstacle => 
                    Position
                        .Create(obstacle.X, obstacle.Y, grid)
                        .ToValidation(ValidationError.ObstacleOutOfBounds(obstacle, grid))
                )
                .Sequence()
                .Map(xs => Prelude.toHashSet(xs));

        public static Validation<ValidationError, File> Create(ParsedFile parsedFile) =>
            CreateGrid(parsedFile.Grid)
                .Bind(grid =>
                {
                    var obstacles = CreateObstacles(parsedFile.Obstacles, grid);
                    var journeys = parsedFile.Journeys.Map(journey => CreateJourney(journey, grid)).Sequence();
                    
                    return (obstacles, journeys).Apply((a, b) => new File(grid, a, b));
                });
    }
}