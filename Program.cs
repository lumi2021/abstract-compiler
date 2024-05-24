using System.Diagnostics;
using Compiler.CodeProcessing;

namespace Compiler;

public static class Program
{

    public static bool Debug_PrintAst { get; private set; }


    public static int Main(string[] args)
    {
        Debug();

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

                else if (args[i] == "-d")
                {
                    if (args.Length < i+1) return 1;

                    switch(args[i+1].ToLower())
                    {
                        case "ast": Debug_PrintAst = true; break;

                        default: return 1;
                    }

                    i++;
                }

                else pathsToCompile.Add(args[i][1 .. ^1]);
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

    private static void Debug()
    {
        Debug_PrintAst = true;
        CodeProcess.Build(["../../../test-code/main.ab"], "../../../test-code/bin", "main.asm");
        Environment.Exit(0);
    }

}
