using Compiler.CodeProcessing.Lexing;

namespace Compiler.CodeProcessing.Exeptions;


public abstract class BuildException() : ApplicationException("") {}

public abstract class SyntaxException(int start, int end) : BuildException()
{
    protected int stat = start;
    protected int end = end;
}
public abstract class EvaluationException() : BuildException() {}
public abstract class CompilingException() : BuildException() {}

public class UnexpectedTokenException(Token token) : SyntaxException(token.start, token.end)
{
    protected Token _token = token;

    public override string ToString()
        => $"Unexpected token found during parsing! (\"{_token.value}\")";
}

public class MoreThanOneNamespaceException() : EvaluationException() {}
public class OutOfPlaceCodeException() : EvaluationException() {}
public class InvalidNamingException() : EvaluationException() {}

public abstract class NotFoundException() : EvaluationException() {}

public class MethodNotFoundException() : NotFoundException() {}
public class FieldNotFoundException() : NotFoundException() {}
public class LocalVariableNotFoundException() : NotFoundException() {}

public class InvalidInstructionException() : EvaluationException() {}

// assembly
public class AssemblyInvalidInstructionException() : EvaluationException() {}
public class AssemblyArgumentsMissingException() : EvaluationException() {}
public class AssemblyTooManyArgumentsException() : EvaluationException() {}
public class AssemblyNotAllowedTypeException() : EvaluationException() {}


// dev exeptions
public class InstructionProcessingNotImplementedException(string instructionName) : EvaluationException()
{
    private string _instName = instructionName;

    public override string ToString()
        => "Exception inside the compiler detected!!!\n"
        + "Please report this bug and this error message in the issues tab on GitHub!\n"
        + "Error message: " + $"Instruction \"{_instName}\" is not implemented on evaluating step! (err.000)\n"
        + "Stack trace:\n" + StackTrace?.Replace(" in", "\n   ╚> in") ?? "\t<null>";
}
