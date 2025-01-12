#r "nuget: FParsec"
#r "nuget: FsToolkit.ErrorHandling"

open System.IO
open FParsec
open FsToolkit.ErrorHandling

type Direction = N | E | S | W
type Command = TurnLeft | TurnRight | MoveForward

type RobotState = { X: int; Y: int; Direction: Direction }

type RobotJourney = {
    InitialState: RobotState
    Commands: Command list
    ExpectedFinalState: RobotState
}

type Grid = {
    Width: int
    Height: int
}

type Obstacle = {
    X: int
    Y: int
}

type ParsedFile = {
    Grid: Grid
    Obstacles: Obstacle list
    Journeys: RobotJourney list
}

type ValidatedFile = {
    Grid: Grid
    Obstacles: Obstacle list
    Journeys: RobotJourney list
}

let parseGrid =
    pstring "GRID " >>. pint32 .>> pchar 'x' .>>. pint32 
    |>> (fun (width, height) -> { Grid.Width = width; Height = height })
    
let parseObstacle =
    pstring "OBSTACLE " >>. pint32 .>> pchar ' ' .>>. pint32 .>> opt newline
    |>> (fun (x, y) -> { Obstacle.X = x; Y = y })

let parseDirection =
    choice [
        pchar 'N' >>% Direction.N
        pchar 'E' >>% E
        pchar 'S' >>% S
        pchar 'W' >>% W
    ]

let parseCommand =
    choice [
        pchar 'L' >>% Command.TurnLeft
        pchar 'R' >>% TurnRight
        pchar 'F' >>% MoveForward
    ]

let parseRobotState =
    pipe3
        pint32
        (pchar ' ' >>. pint32)
        (pchar ' ' >>. parseDirection)
        (fun x y dir -> { X = x; Y = y; Direction = dir })

let parseJourney =
    spaces >>.
    pipe3
        (parseRobotState .>> newline)
        (many parseCommand .>> newline)
        (parseRobotState .>> opt newline)
        (fun initial commands final ->
            {
                RobotJourney.InitialState = initial
                Commands = commands
                ExpectedFinalState = final
            }
        )
    
let parseJourneys = many parseJourney

let parseFile =
    parseGrid
    .>> spaces
    .>>. many parseObstacle
    .>>. many parseJourney
    .>> eof
    |>> (fun ((grid, obstacles), journeys) ->
        {
            ParsedFile.Grid = grid
            Obstacles = obstacles
            Journeys = journeys
        })   
    
let runParseFile (input: string) : Result<ParsedFile, string list> =
    match run parseFile input with
    | Success(result, _, _) -> Result.Ok result
    | Failure(msg, error, _) -> Result.Error <| [$"{msg} {error}"]

let validateCoordinates (gridWidth: int) (gridHeight: int) (x: int) (y: int) =
    x >= 0 && x < gridWidth && y >= 0 && y < gridHeight

let validateParsedFile (parsedFile: ParsedFile) : Result<ValidatedFile, string list> =
    validation {
        if parsedFile.Grid.Width < 1 || parsedFile.Grid.Height < 1 then
            return! (Result.Error "Grid size is invalid. Width and height must be >= 1.")
        
        let GridWidth = parsedFile.Grid.Width
        let GridHeight = parsedFile.Grid.Height
            
        let validateState (state: RobotState) = result {
            do! validateCoordinates GridWidth GridHeight state.X state.Y
                |> Result.requireTrue $"Coordinates ({state.X}, {state.Y}) are out of bounds for grid of size {GridWidth}x{GridHeight}."
        }

        let validateJourney (journey: RobotJourney) = validation {
            let! x = validateState journey.InitialState
            and! y = validateState journey.ExpectedFinalState
            return ()
        }

        let validateObstacle (obstacle: Obstacle) = result {
            do! validateCoordinates GridWidth GridHeight obstacle.X obstacle.Y 
                |> Result.requireTrue $"Obstacle coordinates ({obstacle.X}, {obstacle.Y}) are out of bounds for grid of size {GridWidth}x{GridHeight}."
        }
        
        do! parsedFile.Obstacles |> List.traverseResultA validateObstacle |> Result.ignore
        do! parsedFile.Journeys |> List.traverseResultA validateJourney |> Result.mapError (List.concat) |> Result.ignore
        
        return { ValidatedFile.Grid = parsedFile.Grid; Obstacles = parsedFile.Obstacles; Journeys = parsedFile.Journeys }
    }

let step (data: ValidatedFile) (state: RobotState) (command: Command): Result<RobotState, string> =
    match command with
    | TurnLeft ->
        { state with Direction = match state.Direction with N -> W | W -> S | S -> E | E -> N }
        |> Result.Ok
    | TurnRight ->
        { state with Direction = match state.Direction with N -> E | E -> S | S -> W | W -> N }
        |> Result.Ok
    | MoveForward ->
        result {
            let after =
                match state.Direction with
                | N -> { state with Y = state.Y + 1 }
                | E -> { state with X = state.X + 1 }
                | S -> { state with Y = state.Y - 1 }
                | W -> { state with X = state.X - 1 }
                
            let grid = data.Grid
            let obstacles = data.Obstacles
            
            if not <| validateCoordinates grid.Width grid.Height after.X after.Y then
               return! Result.Error "OUT OF BOUNDS"
               
            if obstacles |> List.exists (fun o -> o.X = after.X && o.Y = after.Y) then
                return! Result.Error "CRASHED"
                
            return after
        }
            
module List =             
    let foldM folder initialState items =
        items |> List.fold (
            fun acc item ->
                acc |> Result.bind (
                    fun state -> folder state item
                )
            ) (Result.Ok initialState)
                
let simulate (grid: ValidatedFile) (state: RobotState) (commands: Command list) =
    commands |> List.foldM (step grid) state

let launch filename = result {
    let input = File.ReadAllText filename
    let! parsedFile = runParseFile input 
    let! validatedFile = validateParsedFile parsedFile
    
    return validatedFile.Journeys
    |> List.map (fun journey -> simulate validatedFile journey.InitialState journey.Commands, journey)
    |> List.map (fun (result, journey) ->
        match result with
        | Result.Ok state ->
            
            if state = journey.ExpectedFinalState then
                $"SUCCESS {state.X} {state.Y} {state.Direction}"
            else
                $"FAILURE {state.X} {state.Y} {state.Direction}"
                
        | Result.Error msgs -> msgs 
    )
} 
    
let path = __SOURCE_DIRECTORY__ + "\..\RobotApp.Tests\Sample0.txt"
launch path