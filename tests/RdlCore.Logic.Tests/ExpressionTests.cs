namespace RdlCore.Logic.Tests;

public class VbExpressionGeneratorTests
{
    private readonly VbExpressionGenerator _generator;

    public VbExpressionGeneratorTests()
    {
        var logger = Mock.Of<ILogger<VbExpressionGenerator>>();
        _generator = new VbExpressionGenerator(logger);
    }

    [Fact]
    public void Generate_FieldReference_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.FieldReference,
            "CustomerName",
            null, null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Be("=Fields!CustomerName.Value");
    }

    [Fact]
    public void Generate_ParameterReference_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.ParameterReference,
            "ReportDate",
            null, null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Be("=Parameters!ReportDate.Value");
    }

    [Fact]
    public void Generate_GlobalReference_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.GlobalReference,
            "PageNumber",
            null, null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Be("=Globals!PageNumber");
    }

    [Fact]
    public void Generate_StringLiteral_ShouldGenerateQuotedString()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.Literal,
            "Hello World",
            null, null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Be("=\"Hello World\"");
    }

    [Fact]
    public void Generate_NumericLiteral_ShouldGenerateNumber()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.Literal,
            42,
            null, null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Be("=42");
    }

    [Fact]
    public void Generate_Conditional_ShouldGenerateIIf()
    {
        // Arrange
        var condition = new AbstractSyntaxTree(
            AstNodeType.BinaryOperation,
            null,
            new[]
            {
                new AbstractSyntaxTree(AstNodeType.FieldReference, "Amount", null, null, null),
                new AbstractSyntaxTree(AstNodeType.Literal, 100, null, null, null)
            },
            ">", null);

        var trueValue = new AbstractSyntaxTree(AstNodeType.Literal, "High", null, null, null);
        var falseValue = new AbstractSyntaxTree(AstNodeType.Literal, "Low", null, null, null);

        var ast = new AbstractSyntaxTree(
            AstNodeType.Conditional,
            "IIf",
            new[] { condition, trueValue, falseValue },
            null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Contain("IIf");
        result.Should().Contain("Fields!Amount.Value");
        result.Should().Contain("High");
        result.Should().Contain("Low");
    }

    [Fact]
    public void Generate_FunctionCall_ShouldGenerateFunctionSyntax()
    {
        // Arrange
        var ast = new AbstractSyntaxTree(
            AstNodeType.FunctionCall,
            "Format",
            new[]
            {
                new AbstractSyntaxTree(AstNodeType.FieldReference, "Date", null, null, null),
                new AbstractSyntaxTree(AstNodeType.Literal, "yyyy-MM-dd", null, null, null)
            },
            null, null);

        // Act
        var result = _generator.Generate(ast);

        // Assert
        result.Should().Contain("Format(");
        result.Should().Contain("Fields!Date.Value");
        result.Should().Contain("yyyy-MM-dd");
    }
}

public class SandboxValidatorTests
{
    private readonly SandboxValidator _validator;

    public SandboxValidatorTests()
    {
        var logger = Mock.Of<ILogger<SandboxValidator>>();
        _validator = new SandboxValidator(logger);
    }

    [Theory]
    [InlineData("=Fields!Name.Value")]
    [InlineData("=IIf(Fields!Amount.Value > 100, \"High\", \"Low\")")]
    [InlineData("=Format(Fields!Date.Value, \"yyyy-MM-dd\")")]
    [InlineData("=Sum(Fields!Amount.Value)")]
    public void Validate_ValidExpression_ShouldPass(string expression)
    {
        // Act
        var result = _validator.Validate(expression);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SandboxViolations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("=System.IO.File.ReadAllText(\"test.txt\")")]
    [InlineData("=System.Net.WebClient.DownloadString(\"http://test.com\")")]
    [InlineData("=System.Reflection.Assembly.Load(\"test\")")]
    public void Validate_ProhibitedPattern_ShouldFail(string expression)
    {
        // Act
        var result = _validator.Validate(expression);

        // Assert
        result.IsValid.Should().BeFalse();
        result.SandboxViolations.Should().NotBeEmpty();
    }
}

public class ExpressionOptimizerTests
{
    private readonly ExpressionOptimizer _optimizer;

    public ExpressionOptimizerTests()
    {
        var logger = Mock.Of<ILogger<ExpressionOptimizer>>();
        _optimizer = new ExpressionOptimizer(logger);
    }

    [Fact]
    public void Optimize_DoubleParentheses_ShouldRemove()
    {
        // Arrange
        var expression = "=((Fields!Name.Value))";

        // Act
        var result = _optimizer.Optimize(expression);

        // Assert
        result.Should().Be("=(Fields!Name.Value)");
    }

    [Fact]
    public void Optimize_TrueCondition_ShouldSimplify()
    {
        // Arrange
        var expression = "=IIf(True, \"Yes\", \"No\")";

        // Act
        var result = _optimizer.Optimize(expression);

        // Assert
        result.Should().Contain("Yes");
    }

    [Fact]
    public void Optimize_FalseCondition_ShouldSimplify()
    {
        // Arrange
        var expression = "=IIf(False, \"Yes\", \"No\")";

        // Act
        var result = _optimizer.Optimize(expression);

        // Assert
        result.Should().Contain("No");
    }
}
