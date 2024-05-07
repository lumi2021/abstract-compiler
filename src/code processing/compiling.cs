using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;

namespace Compiler.CodeProcessing.Compiling;

public static class Compilator
{

    public static void Compile(ScriptNode program, string oPath, string oFile)
    {

        StringBuilder final = new();


        if (!Directory.Exists(oPath))
            Directory.CreateDirectory(oPath);
        
        File.WriteAllText(oPath + '/' + oFile, final.ToString());

    }


}
