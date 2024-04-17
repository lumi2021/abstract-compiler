using System.Text;

namespace Compiler.CodeProcessing;

public static class Compilation
{

    public static void Compile(SyntaxTree[] trees, string resultDir, string resultName)
    {

        StringBuilder finalAsm = new();

        /*
        foreach (var t in trees)
        {
            finalAsm.AppendLine(" ;\tSource Code:\n");
            var lines = t.ToString().Replace("\r\n", "\n").Split('\n');
            foreach (var line in lines)
                finalAsm.AppendLine(" ;\t" + line);
        }
        finalAsm.Append('\n');
        */

        List<(string, string)> stringData = [];

        Dictionary<string, DecVariable> variablesReference = [];

        finalAsm.AppendLine("section .text\n");

        //FIXME Forcing including STDLIB
        var stdlib = File.ReadAllText("src/templates/stdlib.asm");
        finalAsm.Append(stdlib + "\n");
        finalAsm.AppendLine("_main: jmp Program.Start");

        // compile the final asm
        foreach (var i in trees)
        {

            string nSpace = "";
            bool usingNamespace = false;

            List<NodeSyntax> toCompile = [.. i.Root.ChildrenNodes];
            List<bool> stack = [ true ];

            while(toCompile.Count > 0)
            {
                var node = toCompile[0];
               
                if (node is UsingNamespaceNodeSyntax @uNamespace)
                {
                    usingNamespace = true;
                    nSpace = @uNamespace.Identifier.ValuesString;
                }

                else if (node is MethodNodeSyntax @mNodeSyntax)
                {
                    string path = usingNamespace ? $"{nSpace}." : "";
                    finalAsm.AppendLine($"{path}{mNodeSyntax.Name.ValuesString}:");

                    toCompile.InsertRange(1 , [.. node.ChildrenNodes, new FlagStackEndNode()]);
                    stack.Push(false);
                }

                else if (node is VariableDeclarationSyntax @varDec)
                {
                    var dname = $"_byted_{variablesReference.Count:0000}";
                    var variable = new DecVariable
                    {
                        labelName = dname,
                        type = @varDec.Type,
                        initialValue = @varDec.InitialValue?.ToAsmReadable() ?? ""
                    };

                    variablesReference.Add(@varDec.Name.ValuesString, variable);
                }

                else if (node is AsmOperationNodeSyntax @asmOp)
                {
                    finalAsm.AppendLine($"\t{@asmOp.ToString()}");
                }

                else if (node is CallNodeSyntax @call)
                {
                    var args = @call.Arguments.Values;
                    for (var j = args.Length-1; j >= 0; j--)
                    {
                        if (args[j] is StringLiteralNodeSyntax @stringLiteral)
                        {
                            finalAsm.AppendLine($"\tpush _strd_{stringData.Count:0000}");
                            stringData.Add(
                                ($"_strd_{stringData.Count:0000}", @stringLiteral.ToAsmReadable()) );
                        }
                        else if (args[j] is NumericValueNodeSyntax @numericValue)
                        {
                            finalAsm.AppendLine($"\tpush 0x{@numericValue.Value}");
                        }
                        else if (args[j] is IdentifierNodeSyntax @identifier)
                        {
                            if (variablesReference.TryGetValue(@identifier.ValuesString, out var dName))
                            {
                                finalAsm.AppendLine($"\tmov eax, 0");               // clear eax
                                finalAsm.AppendLine($"\tmov al, BYTE[{dName}]");    // move byte to low a
                                finalAsm.AppendLine($"\tpush eax");                 // put eax onto stack
                            }
                            else throw new Exception();
                        }
                    }

                    finalAsm.AppendLine($"\tcall {@call.Method}");
                }

                else if (node is AssiginNodeSyntax @assigin)
                {
                    if (variablesReference.TryGetValue(@assigin.Destiny.ValuesString, out var variable))
                    {
                        if (VerifyValue(@assigin.Value, variable.type))
                        {
                            finalAsm.AppendLine($"\tmov BYTE[{variable.labelName}],"
                            + $"{@assigin.Value.ToAsmReadable()}");
                        }
                    }
                }

                else if (node is ReturnNodeSyntax)
                {
                    stack[0] = true;
                    finalAsm.AppendLine("\tret");
                }


                // FLAGS //
                else if (node is FlagStackEndNode)
                {
                    if (stack[0] == false)
                        finalAsm.AppendLine("\tret");
                    
                    stack.Pop();
                    finalAsm.AppendLine();
                }


                toCompile.Pop();

            }


        }

        finalAsm.AppendLine("section .data\n");
        finalAsm.AppendLine("\t; string constants");
        foreach (var i in stringData)
            finalAsm.AppendLine($"\t{i.Item1}\tdb {i.Item2}");
        
        finalAsm.AppendLine("\n\t; variables");
        foreach (var i in variablesReference)
        {
            finalAsm.AppendLine($"\t{i.Value.labelName}\tdb {i.Value.initialValue}");
        }

        // Save in disk

        if (!Directory.Exists(resultDir))
            Directory.CreateDirectory(resultDir);
        
        File.WriteAllText($"{resultDir}/{resultName}", finalAsm.ToString());

        Console.WriteLine("Compilation ended here!\nResult in "
        + Path.GetFullPath($"{resultDir}/{resultName}"));

    }

    private static bool VerifyValue(ValueNodeSyntax value, TypeNodeSyntax destinyType)
    {
        switch (destinyType.Type)
        {
            case "byte":
                
                if (value is NumericValueNodeSyntax @numeric)
                {
                    long nv = @numeric.DecimalValue;
                    if (nv > 0 && nv < 256) // byte range
                        return true;

                    else
                        throw new Exception("Error! Value is lower or grater than the supported by byte!");

                }
                break;

            case "int": break;

            case "string": break;

            default: return false;
        }

        return true;
    }


    struct DecVariable()
    {
        public string labelName;
        public TypeNodeSyntax type;
        public string initialValue = "";

        public override readonly string ToString() => labelName;
    }

}