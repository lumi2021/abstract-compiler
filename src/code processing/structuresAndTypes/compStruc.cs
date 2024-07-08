using System.Text;
using Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;
using Compiler.CodeProcessing.IntermediateAssembyLanguage;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;

namespace Compiler.CodeProcessing.CompilationStructuring;

public interface IReferenceable
{
    public string[] GetGlobalPath();
    public string GetGlobalReference();
}
public abstract class CompStruct(StatementNode referTo, CompStruct? parent = null)
{

    public StatementNode nodeReference = referTo;
    protected CompStruct? _parent = parent;

}


public class CompilationRoot(StatementNode referTo) : CompStruct(referTo)
{

    public List<Script> scripts = [];
    public List<NamespaceItem> namespaces = [];

    public NamespaceItem? FindNamespace(string identfier)
        => namespaces.Find(e => e.GetGlobalReference() == identfier);

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
                sb.Append($"namespace {@exp.name}");
            else sb.Append($"namespace (implicit)");
            
            sb.Append(" [META: ");
            sb.Append($"{ns.methods.Count} methods; ");
            sb.Append($"{ns.globalFields.Count} g. fields;");
            sb.AppendLine("]:");

            foreach (var m in ns.methods)
            {
                if (m.ScriptRef is HeaderScript)
                    sb.Append("\tHEADER ");
                else sb.Append('\t');

                sb.Append($"func {m.returnType} {m.name}(");

                foreach (var mp in m.Parameters)
                {
                    if (mp.type is TypeItem @type)
                        sb.Append($"{@type.Value} {mp.identifier}, ");
                }

                if (m.Parameters.Count > 0) sb.Remove(sb.Length - 2, 2);

                sb.Append(')');

                if (m.ScriptRef is not HeaderScript)
                {
                    sb.Append(" [META: ");
                    sb.Append($"stack size: {m.LocalMemorySize}B;");
                    sb.AppendLine("]\n\t{");

                    if (!m.compiled)
                    {
                        foreach (var i in m.codeStatements)
                        {
                            string[] lines = i.ToString()!.Split('\n');
                            foreach(var l in lines) sb.AppendLine($"\t\t{l}");
                        }
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
                else sb.Append('\n');
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

    public virtual string[] GetGlobalPath() => [$"{GetHashCode()}"];
    public virtual string GetGlobalReference() => $"{GetHashCode()}";
}

public class ImplicitNamespaceItem(Script source, StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent, source) {}
public class ExplicitNamespaceItem(Script source, StatementNode referTo, CompilationRoot parent) : NamespaceItem(referTo, parent, source)
{
    public Identifier name = new();

    public override string[] GetGlobalPath() => [.. name.values];
    public override string GetGlobalReference() => string.Join('.', name.values);
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
    
    public List<StatementNode> codeStatements = [];
    public List<IntermediateInstruction> interLang = [];
    public bool compiled = false;

    public uint LocalMemorySize => (uint)LocalData.Select(a => a.Value.Size).Sum();
    public List<TypeItem> LocalData { get; set;} = [];

    public uint ParametersMemorySize => (uint)Parameters.Select(a => a.type.Value.Size).Sum();
    public List<ParameterItem> Parameters {get; set; } = [];

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

    public string[] GetGlobalPath()
    {
        List<string> nameSpace = [];
        List<string> path = [];

        if (Parent != null)
            nameSpace.AddRange(Parent.GetGlobalPath());

        path.AddRange(name.values);

        return [..nameSpace, ..path];
    }
    public string GetGlobalReference()
    {
        List<string> nameSpace = [];
        List<string> path = [];
        List<string> paramStrs = [];

        if (Parent != null)
            nameSpace.AddRange(Parent.GetGlobalPath());

        path.AddRange(name.values);

        foreach (var i in Parameters)
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

    public bool IsAssignableTo(TypeItem type) => IsAssignableTo(this, type);

    public static bool IsAssignableTo(TypeItem a, TypeItem b)
    {
        if (a == b) return true;

        if (a.Value is PrimitiveType @prim1 && b.Value is PrimitiveType @prim2)
        {
            if (@prim2.Kind == PrimitiveTypeKind.IntegerNumeric)
                return @prim1.Kind == @prim2.Kind && @prim1.MinValue >= @prim2.MinValue && @prim1.MaxValue <= @prim2.MaxValue;

            else if (@prim2.Kind == PrimitiveTypeKind.FloatingNumeric)
                return @prim1.Kind == PrimitiveTypeKind.FloatingNumeric || @prim1.Kind == PrimitiveTypeKind.IntegerNumeric;
        }

        return false;
    }
}
