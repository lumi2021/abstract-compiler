using System.Collections;
using Compiler.CodeProcessing.CompilationStructuring;
using Compiler.CodeProcessing.Lexing;
using Compiler.CodeProcessing.Scripts;
using Compiler.Util.Compilation;
using Newtonsoft.Json;

namespace Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;

public abstract class StatementNode {}

public class ScriptNode(Script srcr) : ScopeNode
{
    public Script SourceReference {get; private set;} = srcr;
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

public class NamespaceNode : StatementNode
{
    public Identifier name = new();
    public ScopeNode namespaceScope = null!;
}

public class ScopeNode : StatementNode, IEnumerable<StatementNode>
{
    public List<StatementNode> body = [];

    public override string ToString() => $"{{\n\t{string.Join("\n\t", body)}\n}}";

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<StatementNode> GetEnumerator() => body.GetEnumerator();
}

public class AssemblyScopeNode : StatementNode
{
    public List<AssemblyExpressionNode> body = [];
}

public class MethodDeclarationNode : StatementNode
{
    public TypeNode returnType = null!;
    public Identifier name = new();
    public ParametersListNode parameters = null!;
    public ScopeNode methodScope = null!;
}

public class ParametersListNode : StatementNode, IEnumerable<ParameterNode>
{
    public List<ParameterNode> parameters = [];

    public override string ToString() => string.Join(", ", parameters);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<ParameterNode> GetEnumerator() => parameters.GetEnumerator();
}
public class ParameterNode : StatementNode
{
    public TypeNode type = null!;
    public string identifier = "";

    public override string ToString() => $"{type} {identifier}";    
}

public class VariableDeclarationNode : StatementNode
{
    public bool isConstant = false;
    public TypeNode type = null!;
    public Identifier identifier = new();
    public ExpressionNode? value = null;

    public override string ToString() => isConstant? "const" : "let" + $" {type} {identifier}"
    + (value != null ? $" = {value}" : "");
}

public class LocalDeclarationNode : StatementNode
{
    public bool isConstant = false;
    public TypeNode type = null!;
    public LocalRef reference = new();
    public ExpressionNode? value = null;

    public override string ToString() => isConstant? "const" : "let" + $"{type} {reference}";
}

public class IfStatementNode : StatementNode
{
    public ExpressionNode condition = null!;
    public StatementNode? result = null;

    public ElseStatementNode? elseStatement = null;

    public override string ToString()
    {
        string str = $"if {condition} => {result}";

        if (elseStatement != null)
            str += $"\n{elseStatement}";

        return str;
    }
}

public class ElseStatementNode : StatementNode
{
    public ExpressionNode? condition = null;
    public StatementNode? result = null;

    public ElseStatementNode? elseStatement = null;

    public override string ToString()
    {
        string str = "";
        
        if (condition != null)
            str += $"elif {condition} => {result}";
        else
            str += $"else => {result}";

        if (elseStatement != null)
            str += $"\n{elseStatement}";

        return str;
    }
}

public class ReturnStatementNode : StatementNode
{
    public ExpressionNode? value = null;

    public override string ToString() => $"return {value}";
}


public abstract class ExpressionNode : StatementNode
{
    public bool processed = false;
    public override string ToString() => $"{{ kind: {GetType().Name} }}";
}

public class AssiginmentExpressionNode : ExpressionNode
{

    public ExpressionNode assigne = null!;
    public ExpressionNode value = null!;

    public override string ToString() => $"{assigne} = {value}";
}


public class BinaryExpressionNode : ExpressionNode
{

    public ExpressionNode leftStatement = null!;
    public ExpressionNode rightStatement = null!;

    public string expOperator = "";

    public override string ToString()
    {
        var str = "";

        if (leftStatement is BinaryExpressionNode @leftBin)
        {
        
            if (expOperator != "+" && expOperator != "-" &&
            (@leftBin.expOperator == "+" || @leftBin.expOperator == "-"))
                str += $"({leftStatement})";
            else
                str += $"{leftStatement}";
        
        }
        else
            str += $"{leftStatement}";

        str += $" {expOperator} ";

        if (rightStatement is BinaryExpressionNode @rightBin)
        {

            if (expOperator != "+" && expOperator != "-" &&
            (@rightBin.expOperator == "+" || @rightBin.expOperator == "-"))
                str += $"({rightStatement})";
            else
                str += $"{rightStatement}";

        }
        else
            str += $"{rightStatement}";

        return str;
    }

}
public class UnaryExpressionNode : ExpressionNode
{
    public ExpressionNode expression = null!;
    public Token expOperator;

    public override string ToString()
    {
        var str = $"{expOperator.value}";

        if (expression is BinaryExpressionNode || expression is UnaryExpressionNode)
            str += $"({expression})";
        else str += expression.ToString();

        return str;
    }
}
public class TypeCastingExpressionNode : ExpressionNode
{
    public ExpressionNode expression = null!;
    public TypeNode type = null!;

    public override string ToString()
    {
        var str = $"";

        if (expression is BinaryExpressionNode || expression is UnaryExpressionNode)
            str += $"({expression})";
        else str += expression.ToString();

        str += $" as {type}";

        return str;
    }
}


public class IdentifierNode(TypeItem? type = null, int? local = null) : ExpressionNode
{

    public bool isLocal = local != null; 

    public Identifier symbol = new();
    public LocalRef localRef = (local == null) ? new() : new(type!, local.Value);

    public TypeItem refersToType => isLocal ? localRef.refersToType : symbol.refersToType;

    public override string ToString() => isLocal ? $"{localRef}" : $"{symbol}";

    public static bool operator ==(IdentifierNode left, IdentifierNode right) =>  left.Equals(right);
    public static bool operator !=(IdentifierNode left, IdentifierNode right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is IdentifierNode inode)
        {
            if (isLocal == inode.isLocal)
            {
                if (isLocal)
                    return localRef == inode.localRef;
                else
                    return symbol == inode.symbol;
            }
        }

        return false;
    }
    public override int GetHashCode() => base.GetHashCode();

}

public class MethodCallNode : ExpressionNode
{

    public Identifier target = new();
    public List<ExpressionNode> arguments = [];

    public override string ToString() => $"{target}({string.Join(", ", arguments)})";

}

public class NumericLiteralNode : ExpressionNode
{

    public double value = 0.0;

    public override string ToString() => $"{value}";
}

public class StringLiteralNode : ExpressionNode
{

    public string value = "";

    public override string ToString() => $"\"{value}\"";

}

public class BooleanLiteralNode : ExpressionNode
{
    public bool value = false;
    public override string ToString() => $"{value}";
}

public class NullLiteralNode : ExpressionNode
{
    public override string ToString() => "null";
}

public class AssemblyExpressionNode : ExpressionNode
{
    public AssemblyInstruction instruction = AssemblyInstruction.Undefined;
    public List<ExpressionNode> arguments = [];

    public override string ToString() => $"{instruction.ToString().ToUpper()} {string.Join(", ", arguments)}";
}

public class ReferenceModifier : ExpressionNode
{
    public ExpressionNode expression = null!;
    public Token modifier;

    public override string ToString() => $"{modifier.value}{expression}";
}

public abstract class TypeNode : ExpressionNode {}
public class PrimitiveTypeNode : TypeNode
{
    public PrimitiveTypeList value = PrimitiveTypeList.Void;
    override public string ToString() => $"{value}";
}
public class ComplexTypeNode : TypeNode
{
    //string identifier = "";
}


public struct Identifier(TypeItem refersToType, params string[] values)
{
    public readonly List<string> values = [..values];
    public readonly bool isGlobal = false;

    public TypeItem refersToType = refersToType;
    public CompStruct refersTo;

    public int Len => values.Count;

    public override string ToString() => string.Join('.', values) ?? "nil";

    public static bool operator ==(Identifier left, Identifier right) =>  left.Equals(right);
    public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is Identifier id)
            return Enumerable.SequenceEqual(values, id.values);
        else return false;
    }
    public override int GetHashCode() => base.GetHashCode();
}
public readonly struct LocalRef(TypeItem refersToType, int idx)
{
    public readonly int index = idx;

    public readonly TypeItem refersToType = refersToType;

    override public string ToString() => index >= 0 ? $"local.{index}" : $"arg.{Math.Abs(index)-1}";

    public static bool operator ==(LocalRef left, LocalRef right) =>  left.Equals(right);
    public static bool operator !=(LocalRef left, LocalRef right) => !left.Equals(right);

    public override bool Equals(object? obj)
    {
        if (obj is LocalRef lref)
            return index == lref.index;
        else return false;
    }
    public override int GetHashCode() => base.GetHashCode();
}


/* #### INTERMEDIATE ASSEMBLY INSTRUCTIONS #### */
public enum AssemblyInstruction : ushort
{
    Undefined = 0,

    Mov,
    Push,

    Add,
    Sub,
    Mul,
    Div,

    Call,

    // pseudo-instructions
    Extern, PseudoInstructionsStart = Extern,
    
}
