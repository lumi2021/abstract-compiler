using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.CompilationStructuring;

public interface IReferenceable
{
    public Identifier GetGlobalReference();
    public string GetGlobalReferenceAsm();
    public string GetGlobalReferenceIL();
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
                    if (mp.type is TypeItem @type)
                        sb.Append($"{@type.Value} {mp.identifier}, ");
                }

                if (m.parameters.Count > 0) sb.Remove(sb.Length - 2, 2);

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
                    {
                        sb.AppendLine($"\t\t{i}");
                    }
                }

                sb.AppendLine("\t}");
            }

            sb.AppendLine();

        }

        return sb.ToString();
    }

}

public abstract class NamespaceItem(StatementNode referTo, CompilationRoot parent, Script source) : CompStruct(referTo, parent), IReferenceable
{
    public List<FieldItem> globalFields = [];
    public List<MethodItem> methods = [];

    public Script ScriptSourceReference {get; private set;} = source;
    
    public CompilationRoot? Parent
    {
        get => _parent as CompilationRoot;
        set => _parent = value;
    }

    public virtual Identifier GetGlobalReference() =>  new(null!, [$"{GetHashCode()}"]);
    public string GetGlobalReferenceAsm() => string.Join('.', GetGlobalReference());
    public string GetGlobalReferenceIL() => string.Join('.', GetGlobalReference());
}

public class ImplicitNamespaceItem(Script source, StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent, source) {}
public class ExplicitNamespaceItem(Script source, StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent, source)
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

    public uint LocalMemorySize => (uint)LocalData.Select(a => a.Value.Size).Sum();
    public List<TypeItem> LocalData { get; set;} = [];

    public Script ScriptRef => Parent.ScriptSourceReference;

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

    public NamespaceItem Parent
    {
        get => (_parent as NamespaceItem)!;
        set => _parent = value;
    }

    public Identifier GetGlobalReference()
    {
        List<string> path = [];
        if (Parent != null) path.AddRange(Parent.GetGlobalReference().values);
        path.AddRange(name.values);

        return new(returnType, [.. path]);
    }
    
    public string GetGlobalReferenceAsm()
    {
        List<string> path = [];
        List<string> paramStrs = [];

        if (Parent != null)
            path.AddRange(Parent.GetGlobalReference().values);

        path.AddRange(name.values);

        foreach (var i in parameters)
            paramStrs.Add(i.type.ToAsmString());

        return string.Join(".", path) + '?' + string.Join('_', paramStrs);
    }

    public string GetGlobalReferenceIL()
    {
        List<string> nameSpace = [];
        List<string> path = [];
        List<string> paramStrs = [];

        if (Parent != null)
            nameSpace.AddRange(Parent.GetGlobalReference().values);

        path.AddRange(name.values);

        foreach (var i in parameters)
            paramStrs.Add(i.type.ToAsmString());

        return string.Join(".", nameSpace) + ':' + string.Join(".", path) + '?' + string.Join('_', paramStrs);
    }

}

public class ParameterItem(StatementNode referTo) : CompStruct(referTo)
{
    public TypeItem type = null!;
    public Identifier identifier = new();
}

public class TypeItem : CompStruct
{
    private readonly ILangType _type;
    public ILangType Value => _type;

    public TypeItem(TypeNode referTo) : base(referTo)
    {
        PrimitiveTypeNode refNode = (referTo as PrimitiveTypeNode)!;

        ILangType t = new PrimitiveType(refNode.value, TypeDefKind.Value);

        _type = t;
    }
    public TypeItem(PrimitiveTypeList type) : base(null!)
    {
        ILangType t = new PrimitiveType(type, TypeDefKind.Value);

        _type = t;
    }

    public override string ToString() => _type.ToString() ?? "null";
    public string ToAsmString() => _type.ToIlString() ?? "nil";

    public static bool operator ==(TypeItem a, TypeItem b) => a.Equals(b);
    public static bool operator !=(TypeItem a, TypeItem b) => !a.Equals(b);

    public override bool Equals(object? obj)
    {
        if (obj is TypeItem @tItem)
        {
            if (_type is PrimitiveType @pType1 && @tItem.Value is PrimitiveType @pType2)
            {
                return @pType1.Value == @pType2.Value;
            }
        }

        return false;
    }
    public override int GetHashCode() => base.GetHashCode();
}
