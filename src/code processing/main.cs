using Compiler.CodeProcessing.Compiling;
using Compiler.CodeProcessing.Evaluating;
using Compiler.CodeProcessing.Exeptions;
using Compiler.CodeProcessing.Lexing;
using Compiler.CodeProcessing.Parsing;

namespace Compiler.CodeProcessing;

public static class CodeProcess
{

    public static List<BuildException> buildingErrors = [];

    public static void Build(string[] toCompile, string outputDir, string outputFile)
    {
        
        Console.WriteLine("Starting build...");

        foreach (var i in toCompile)
        {
            // check if file exists
            if (File.Exists(i))
            {
                // send it content to be compiled
                var tokens = Lexer.Parse(File.ReadAllText(i));

                var program = Parser.ParseTokens(tokens);

                if (Program.Debug_PrintAst)
                    AstWriter.WriteAst(program, outputDir);

                Evaluation.EvalSource(program);

                Compilator.Compile(program, outputDir, outputFile);
            }
        
        }

        Console.WriteLine("Build finished successfully!");

    }

}
