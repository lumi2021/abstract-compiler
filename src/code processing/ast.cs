
using Newtonsoft.Json;

namespace Compiler.CodeProcessing.AbstractSyntaxTree.Nodes;

public abstract class StatementNode {}

public class ScriptNode : ScopeNode
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}

public class NamespaceNode : ScopeNode
{
}

public class ScopeNode : StatementNode
{
    public List<StatementNode> body = [];

    public override string ToString() => $"{{\n\t{string.Join("\n\t", body)}\n}}";
}

public class AssemblyScopeNode : StatementNode
{
    public List<AssemblyExpressionNode> body = [];
}

public class MethodDeclarationNode : StatementNode
{
    public TypeNode returnType = null!;
    public string name = "";
    public ParametersListNode parameters = null!;
    public ScopeNode methodScope = null!;
}

public class ParametersListNode : StatementNode
{
    public List<ParameterNode> parameters = [];

    public override string ToString() => string.Join(", ", parameters);
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
    public string identifier = "";
    public ExpressionNode? value = null;

    public override string ToString() => $"{type} {identifier}";
}



public abstract class ExpressionNode : StatementNode
{
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

    public string expOperator = "";

    public override string ToString()
    {
        var str = $"{expOperator}";

        if (expression is BinaryExpressionNode || expression is UnaryExpressionNode)
            str += $"({expression})";
        else str += expression.ToString();

        return str;
    }
}

public class IdentifierNode : ExpressionNode
{

    public string symbol = "";

    public override string ToString() => $"{symbol}";

}

public class NumericLiteralNode : ExpressionNode
{

    public double value = 0.0;

    public override string ToString() => $"{value}";
}

public class NullLiteralNode : ExpressionNode
{
    public override string ToString() => "null";
}

public class AssemblyExpressionNode : ExpressionNode
{
    public AssemblyInstruction instruction = AssemblyInstruction.Undefined;
    public List<string> arguments = [];

    public override string ToString() => $"{instruction.ToString().ToUpper()} {string.Join(", ", arguments)}";
}


public abstract class TypeNode : ExpressionNode
{
    public bool isArray = false;
}
public class PrimitiveTypeNode : TypeNode
{
    public PrimitiveType value = PrimitiveType.Void;

    override public string ToString() => $"{value}";
}
public class ComplexTypeNode : TypeNode
{
    string identifier = "";
}


/* #################################### */
/* #### PRIMITIVE TYPES ENUMERATOR #### */
public enum PrimitiveType : byte
{
    Void = 0,

    // signed integers
    Integer_8,
    Integer_16,
    Integer_32,
    Integer_64,
    Integer_128,

    // unsigned integers
    UnsignedInteger_8,
    UnsignedInteger_16,
    UnsignedInteger_32,
    UnsignedInteger_64,
    UnsignedInteger_128,

    // floating point numbers
    Floating_32,
    Floating_64,
    SinglePrecisionFloat = Floating_32,
    DoublePrecisionFloat = Floating_64,

    // boolean
    Boolean,

    // text
    Character,
    String,

}

/* #### PRIMITIVE TYPES ENUMERATOR #### */
public enum AssemblyInstruction : ushort
{
    Undefined = 0,

    Mov,
    Push,

    Add,
    Sub,
    Mul,
    Div,
}
