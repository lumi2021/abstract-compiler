using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
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

        // check if all sources exists
        bool breakBuild = false;
        foreach (var i in toCompile)
        {
            // check if file exists
            if (!File.Exists(i))
            {
                Console.WriteLine($"Error! {Path.GetFullPath(i)} don't exist on the disk!");
                breakBuild = true;
            }

        }
        if (breakBuild) Environment.Exit(1);

        // build
        List<ScriptNode> scriptTrees = [];
        foreach (var i in toCompile)
        {
            // send script content to the lexer
            var tokens = Lexer.Parse(File.ReadAllText(i));

            // send tokens array to be parsed into AST
            var program = Parser.ParseTokens(tokens);

            if (Program.Debug_PrintAst)
                AstWriter.WriteAst(program, outputDir);

            scriptTrees.Add(program);
        }

        // evaluate the entire project
        var evaluated = Evaluation.EvalSource([.. scriptTrees]);

        Compilator.Compile(evaluated, outputDir, outputFile);

        Console.WriteLine("Build finished successfully!");

    }

}
