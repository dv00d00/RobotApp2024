using System;
using System.IO;
using System.Threading.Tasks;
using LanguageExt;
using static LanguageExt.Prelude;

namespace RobotApp;

internal record struct FileName(string Value);
internal record struct FileContent(string Value);
internal record LoadedFile(FileName Path, FileContent Content);
internal record Inputs(LoadedFile File, bool Visualise);

internal static class CommandLineParser
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
            return $"Error: File not found at path '{path}'.";
        }
        catch (UnauthorizedAccessException)
        {
            return $"Error: Access to the file at '{path}' is denied.";
        }
        catch (IOException e)
        {
            return $"Error: An I/O error occurred while reading the file: {e.Message}";
        }
        catch (Exception e)
        {
            return $"Error: {e.Message}";
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