module FSharp.Parser

open System.IO
open System.Text
open FParsec

type Direction =
    | N
    | E
    | S
    | W

type Command =
    | TurnLeft
    | TurnRight
    | MoveForward

type RobotState =
    { X: int; Y: int; Direction: Direction }

type RobotJourney =
    { InitialState: RobotState
      Commands: Command list
      ExpectedFinalState: RobotState }

type Grid = { Width: int; Height: int }

type Obstacle = { X: int; Y: int }

type ParsedFile =
    { Grid: Grid
      Obstacles: Obstacle list
      Journeys: RobotJourney list }

type ValidatedFile =
    { Grid: Grid
      Obstacles: Obstacle list
      Journeys: RobotJourney list }

let parseGrid =
    pstring "GRID " >>. pint32 .>> pchar 'x' .>>. pint32
    |>> (fun (width, height) -> { Grid.Width = width; Height = height })

let parseObstacle =
    pstring "OBSTACLE " >>. pint32 .>> pchar ' ' .>>. pint32 .>> opt newline
    |>> (fun (x, y) -> { Obstacle.X = x; Y = y })

let parseDirection =
    choice [ pchar 'N' >>% Direction.N; pchar 'E' >>% E; pchar 'S' >>% S; pchar 'W' >>% W ]

let parseCommand =
    choice
        [ pchar 'L' >>% Command.TurnLeft
          pchar 'R' >>% TurnRight
          pchar 'F' >>% MoveForward ]

let parseRobotState =
    pipe3 pint32 (pchar ' ' >>. pint32) (pchar ' ' >>. parseDirection) (fun x y dir ->
        { X = x; Y = y; Direction = dir })

let parseJourney =
    spaces
    >>. pipe3
        (parseRobotState .>> newline)
        (many parseCommand .>> newline)
        (parseRobotState .>> opt newline)
        (fun initial commands final ->
            { RobotJourney.InitialState = initial
              Commands = commands
              ExpectedFinalState = final })

let parseJourneys = many parseJourney

let parseFile =
    parseGrid .>> spaces .>>. many parseObstacle .>>. many parseJourney .>> eof
    |>> (fun ((grid, obstacles), journeys) ->
        { ParsedFile.Grid = grid
          Obstacles = obstacles
          Journeys = journeys })

let runParseFile (input: string) : Result<ParsedFile, string list> =
    match run parseFile input with
    | Success(result, _, _) -> Result.Ok result
    | Failure(msg, error, _) -> Result.Error <| [ $"{msg} {error}" ]
    
let runParseFileS (input: Stream) : Result<ParsedFile, string list> =
    match runParserOnStream parseFile () "input" input Encoding.UTF8 with
    | Success(result, _, _) -> Result.Ok result
    | Failure(msg, error, _) -> Result.Error <| [ $"{msg} {error}" ]    
