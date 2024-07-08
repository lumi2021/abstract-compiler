using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.Compiling;
using Compiler.CodeProcessing.ErrorHandling;
using Compiler.CodeProcessing.Evaluating;
using Compiler.CodeProcessing.Exeptions;
using Compiler.CodeProcessing.Lexing;
using Compiler.CodeProcessing.Parsing;
using Compiler.CodeProcessing.Scripts;

namespace Compiler.CodeProcessing;

public static class CodeProcess
{

    public static List<BuildException> buildingErrors = [];

    public static string OutputDirectory {get; private set;} = "";

    public static void Build(string[] sourceToCompileArr, string outputDir, string outputFile)
    {
        
        Console.WriteLine("Starting build...");

        OutputDirectory = outputDir;

        List<HeaderScript> includedLibs = [ new("./resources/libs/std.abh", "./resources/libs/nasm/stdx32.asm")];
        List<Script> toCompile =  [.. includedLibs];

        // TODO Append libraries

        // Append sources
        foreach (var src in sourceToCompileArr)
            toCompile.Add(new SourceScript(src));

        // check if all sources exists
        bool breakBuild = false;
        foreach (var i in toCompile)
        {
            // check if file exists
            if (!File.Exists(i.Path))
            {
                Console.WriteLine($"Error! \"{Path.GetFullPath(i.Path)}\" don't exist on the disk!");
                breakBuild = true;
            }

        }
        if (breakBuild) Environment.Exit(1);

        // build
        List<ScriptNode> scriptTrees = [];
        foreach (var i in toCompile)
        {
            // send script content to the lexer
            var tokens = Lexer.Parse(i);

            // send tokens array to be parsed into AST
            var program = Parser.ParseTokens(tokens, i);

            if (Program.Debug_PrintAst)
                AstWriter.WriteAst(program, outputDir, i.Path.Split('/')[^1].Split('.')[0]);

            scriptTrees.Add(program);
        }

        // evaluate the entire project
        var evaluated = Evaluation.EvalSource([.. scriptTrees]);

        if (!ErrorHandler.CompilationFailed)
        {
            BaseCompiler compiler = new NasmCompiler(); // compiling to Nasm
            //                      new WasmCompiler(); // compiling to Wasm
            compiler.Compile(evaluated, outputDir, outputFile);
        }

        ErrorHandler.LogErrors();

        if (!ErrorHandler.CompilationFailed)
            Console.WriteLine("Build finished successfully!");
            
        else
        {
            Console.OpenStandardError();
            Console.WriteLine("Build failed!");
            Console.OpenStandardOutput();
        }

    }

}
