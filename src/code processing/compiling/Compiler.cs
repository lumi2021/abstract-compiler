using Compiler.CodeProcessing.CompilationStructuring;

namespace Compiler.CodeProcessing.Compiling;

public abstract class BaseCompiler
{

    public abstract void Compile(CompilationRoot program, string oPath, string oFile);

}