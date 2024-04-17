using Compiler.CodeProcessing;

namespace Compiler;

public static class Program
{

    public static bool debugMode = false;

    public static int Main(string[] args)
    {

        if (args.Length < 1)
        {
            Help();
            return 1;
        }
    
        if (args[0] == "compile")
        {
            List<string> pathsToCompile = [];
            string outputPath = "";
            string outputFileName = "";

            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "-o") // output flag
                {
                    if (args.Length < i+1) return 1;

                    string outputFile = args[i+1][1 .. ^1];
                    var outputFileSplited = outputFile.Split(['\\', '/']);
                    outputFileName = outputFileSplited[^1];
                    outputPath = outputFile[.. ^outputFileName.Length];
                    i++;
                }

                else
                    pathsToCompile.Add(args[i][1 .. ^1]);
            }

            CodeProcess.Build([.. pathsToCompile], outputPath, outputFileName);

            return 0;
        }

        return 1;

    }

    public static void Help()
    {
        Console.WriteLine("No argument provided.");
        Console.WriteLine("Try 'help' to more details.\n");

        Console.WriteLine("Compier options:");
        Console.WriteLine("\t- compile; Compile the project");

    }

}