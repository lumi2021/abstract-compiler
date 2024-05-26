using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.Evaluating;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;

namespace Compiler.CodeProcessing.CompilationStructuring;

public interface IReferenceable
{
    public Identifier GetGlobalReference();
    public string GetGlobalReferenceAsString();
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

                if (!m.compiled)
                {
                    foreach (var i in m.codeStatements)
                        sb.AppendLine($"\t\t{i}");
                }
                else
                {
                    foreach (var i in m.interLang)
                        sb.AppendLine($"\t\t{i}");
                }

                sb.AppendLine("\t}");
            }

        }

        return sb.ToString().Replace("\t", "  ");
    }

}

public abstract class NamespaceItem(StatementNode referTo, CompilationRoot parent) : CompStruct(referTo, parent), IReferenceable
{

    public List<FieldItem> globalFields = [];
    public List<MethodItem> methods = [];
    
    public CompilationRoot? Parent
    {
        get => _parent as CompilationRoot;
        set => _parent = value;
    }

    public virtual Identifier GetGlobalReference() =>  new(null!, [$"{GetHashCode()}"]);
    public string GetGlobalReferenceAsString() => string.Join('.', GetGlobalReference());
}

public class ImplicitNamespaceItem(StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent) {}
public class ExplicitNamespaceItem(StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent)
{
    public Identifier name = new();

    public override Identifier GetGlobalReference() => new(null!, [.. name.values]);
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

public class MethodItem(StatementNode referTo, NamespaceItem parent) : CompStruct(referTo, parent), IReferenceable
{

    public TypeItem returnType = null!;
    public Identifier name = new();
    public List<ParameterItem> parameters = [];
    
    public List<StatementNode> codeStatements = [];
    public List<IntermediateInstruction> interLang = [];
    public bool compiled = false;

    public uint LocalMemorySize => (uint)LocalData.Select(a => (int)Evaluation.SizeOf(a)).Sum();
    public List<TypeItem> LocalData { get; set;} = [];

    #region code dynamic modifiers

    public void InsertRaw(int index, StatementNode r)
    {
        codeStatements.Insert(index, r);
    }
    public void AppendRaw(StatementNode r)
    {
        codeStatements.Add(r);
    }
        
    public int Del(StatementNode target)
    {
        var idx = codeStatements.IndexOf(target);
        codeStatements.RemoveAt(idx);
        return idx;
    }

    public void Alloc(TypeItem t) => LocalData.Add(t);

    #endregion
    #region compilation emitters

    public void Emit(IntermediateInstruction instruction)
    {
        interLang.Add(instruction);
    }
    
    #endregion

    public NamespaceItem? Parent
    {
        get => _parent as NamespaceItem;
        set => _parent = value;
    }

    public Identifier GetGlobalReference()
    {
        List<string> path = [];
        if (Parent != null) path.AddRange(Parent.GetGlobalReference().values);
        path.AddRange(name.values);

        return new(returnType, [.. path]);
    }
    public string GetGlobalReferenceAsString() => string.Join('.', GetGlobalReference());

}

public class ParameterItem(StatementNode referTo) : CompStruct(referTo)
{
    public TypeItem type = null!;
    public Identifier identifier = new();
}


public abstract class TypeItem(StatementNode referTo) : CompStruct(referTo) {}
public class PrimitiveTypeItem : TypeItem
{
    public PrimitiveType type;
    public bool isArray;

    public PrimitiveTypeItem(StatementNode referTo, PrimitiveType type, bool isArray = false) : base(referTo)
    {
        this.type = type;
        this.isArray = isArray;
    }

    public PrimitiveTypeItem(PrimitiveTypeNode baseType) : base(baseType)
    {
        this.type = baseType.value;
        this.isArray = baseType.isArray;
    }

    public override string ToString() => type.ToString();
}

