using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.Evaluating;

namespace Compiler.CodeProcessing.CompilationStructuring;

public interface IEmitInsturction
{
    public void Emit(Instruction i, params string[] ps);
    public int Del(InstructionItem target);

    public void Alloc(TypeItem t);

    public uint LocalMemorySize { get; } 
    public List<TypeItem> LocalData { get; set;}
}
public abstract class CompStruct(StatementNode referTo, CompStruct? parent = null)
{

    public StatementNode nodeReference = referTo;
    protected CompStruct? _parent = parent;

}


public class CompilationRoot(StatementNode referTo) : CompStruct(referTo)
{

    public List<NamespaceItem> namespaces = [];

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.AppendLine("Current compilation data:\n");

        sb.AppendLine($"### NAMESPACES ({namespaces.Count}): ###");
        foreach (var ns in namespaces)
        {
            if (ns is ExplicitNamespaceItem @exp)
                sb.Append($"\t{@exp.name} ");
            else
                sb.Append($"\t(implicit) ");
            sb.AppendLine($"[{ns.methods.Count} m, {ns.globalFields.Count} gf];");
        }

        sb.AppendLine("\n### ### ### ### ###\n");

        foreach (var ns in namespaces)
        {
            if (ns is ExplicitNamespaceItem @exp)
                sb.AppendLine($"namespace {@exp.name}:");
            else
                sb.AppendLine($"namespace (implicit):");
            
            sb.Append("\tMETA: ");
            sb.Append($"{ns.methods.Count} methods; ");
            sb.Append($"{ns.globalFields.Count} g. fields;\n");

            foreach (var m in ns.methods)
            {
                sb.Append($"\tfunc {m.returnType} {m.name}(");

                foreach (var mp in m.parameters)
                {
                    if (mp.type is PrimitiveTypeItem @pType)
                        sb.Append($"{@pType.type} {mp.identifier}, ");
                }

                if (m.parameters.Count > 1) sb.Remove(sb.Length - 2, 2);

                sb.AppendLine(") {");

                sb.Append("\t\tMETA: ");
                sb.Append($"stack size: {m.LocalMemorySize}B;");
                sb.AppendLine();

                foreach (var i in m.instructions)
                {
                    sb.AppendLine($"\t\t{i}");
                }

                sb.AppendLine("\t}");
            }

        }

        return sb.ToString().Replace("\t", "  ");
    }

}

public abstract class NamespaceItem(StatementNode referTo, CompilationRoot parent) : CompStruct(referTo, parent)
{

    public List<FieldItem> globalFields = [];
    public List<MethodItem> methods = [];
    
    public CompilationRoot? Parent
    {
        get => _parent as CompilationRoot;
        set => _parent = value;
    }
}

public class ImplicitNamespaceItem(StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent) {}
public class ExplicitNamespaceItem(StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent)
{
    public Identifier name = new();
}


public class FieldItem(StatementNode referTo, CompStruct parent) : CompStruct(referTo, parent)
{

    public Identifier name = new();

    public override string ToString() => "field " + name + ";";

    public CompStruct? Parent
    {
        get => _parent as CompStruct;
        set => _parent = value;
    }

} 

public class MethodItem(StatementNode referTo, NamespaceItem parent) : CompStruct(referTo, parent), IEmitInsturction
{

    public TypeItem returnType = null!;
    public Identifier name = new();
    public List<ParameterItem> parameters = [];
    
    public List<InstructionItem> instructions = [];

    public uint LocalMemorySize => (uint)LocalData.Select(a => (int)Evaluation.SizeOf(a)).Sum();
    public List<TypeItem> LocalData { get; set;} = [];

    public void InsertRaw(int index, StatementNode r)
    {
        instructions.Insert(index, new BaseInstructionItem(r));
    }
    public void AppendRaw(StatementNode r)
    {
        instructions.Add(new BaseInstructionItem(r));
    }
    
    public void Emit(int index, Instruction inst, params string[] ps)
    {
        instructions.Insert(index, new AsmInstructionItem(inst, ps));
    }
    public void Emit(Instruction inst, params string[] ps)
    {
        instructions.Add(new AsmInstructionItem(inst, ps));
    }
    
    public int Del(InstructionItem target)
    {
        var idx = instructions.IndexOf(target);
        instructions.RemoveAt(idx);
        return idx;
    }

    public void Alloc(TypeItem t) => LocalData.Add(t);

    public NamespaceItem? Parent
    {
        get => _parent as NamespaceItem;
        set => _parent = value;
    }

}

public class ParameterItem(StatementNode referTo) : CompStruct(referTo)
{
    public TypeItem type = null!;
    public Identifier identifier = new();
}


public abstract class TypeItem(StatementNode referTo) : CompStruct(referTo) {}
public class PrimitiveTypeItem(StatementNode referTo, PrimitiveType type, bool isArray = false) : TypeItem(referTo)
{
    public PrimitiveType type = type;
    public bool isArray = isArray;

    public override string ToString() => type.ToString();
}


public abstract class InstructionItem(StatementNode nodeRef = null!) : CompStruct(nodeRef)
{
    public IEmitInsturction? Parent
    {
        get => _parent as IEmitInsturction;
        set => _parent = (CompStruct)value!;
    }
}
public class BaseInstructionItem(StatementNode reference) : InstructionItem(reference)
{
    public override string ToString() => "RAW > " + nodeReference.ToString();
}
public class AsmInstructionItem(Instruction inst, params string[] ps) : InstructionItem
{
    public readonly Instruction instruction = inst;
    public List<String> parameters = [.. ps];

    public override string ToString() => $"{instruction} {string.Join(", ", parameters)}";
}

