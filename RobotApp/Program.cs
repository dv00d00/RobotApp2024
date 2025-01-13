using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using RobotApp.Logic;

[assembly: InternalsVisibleTo("RobotApp.Tests")]

namespace RobotApp;

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
                    sb.Append('|');
                    if (state.X == x && state.Y == y)
                    {
                        sb.Append($" {GetDirectionSymbol(state.Direction)} ");
                    }
                    else if (file.Obstacles.Contains(new Obstacle(x, y)))
                    {
                        sb.Append(" O ");
                    }
                    else
                    {
                        sb.Append("   ");
                    }
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
            
            sb.Clear();
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