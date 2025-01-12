using System;
using LanguageExt;

namespace RobotApp.Logic;

public static class CompositionRoot
{
    public static System.Collections.Generic.IReadOnlyCollection<string> Execute(string input, Runtime.IRuntimeLog? gridVisualiser = null)
    {
        // parsing, validation and execution
        var result = RunComputation(input, gridVisualiser);
            
        // interpretation
        var results = result.Match(
            Left: error =>
            {
                return error switch
                {
                    ParserError pe => Prelude.List($"Parsing: {pe.Message}"),
                    ValidationErrors ve => ve.Errors.Map(e => $"Validation: {e.Message}"),
                    _ => Prelude.List($"Unknown: {error}"),
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

    internal static Either<Error, Lst<Either<RuntimeError, RobotState>>> RunComputation(string input, Runtime.IRuntimeLog? gridVisualiser = null) =>
        from parsedFile in Parser.ParseInput(input)
        from validatedFile in Validator.ValidateParsedFile(parsedFile)
        select Runtime.TravelAll(validatedFile, gridVisualiser);
}