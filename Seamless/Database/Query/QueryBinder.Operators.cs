using Microsoft.SqlServer.TransactSql.ScriptDom;
namespace Seamless.Database.Query;
internal sealed partial class QueryBinder{
    private static SqlBinaryOperator BindBinaryOperator(BinaryExpression expression)=>expression.BinaryExpressionType switch{
        BinaryExpressionType.Add=>SqlBinaryOperator.Add,
        BinaryExpressionType.Subtract=>SqlBinaryOperator.Subtract,
        BinaryExpressionType.Multiply=>SqlBinaryOperator.Multiply,
        BinaryExpressionType.Divide=>SqlBinaryOperator.Divide,
        BinaryExpressionType.Modulo=>SqlBinaryOperator.Modulo,
        BinaryExpressionType.BitwiseAnd=>SqlBinaryOperator.BitwiseAnd,
        BinaryExpressionType.BitwiseOr=>SqlBinaryOperator.BitwiseOr,
        BinaryExpressionType.BitwiseXor=>SqlBinaryOperator.BitwiseXor,
        _=>throw Unsupported(expression)
    };
    private static SqlUnaryOperator BindUnaryOperator(UnaryExpression expression)=>expression.UnaryExpressionType switch{
        UnaryExpressionType.Positive=>SqlUnaryOperator.Positive,
        UnaryExpressionType.Negative=>SqlUnaryOperator.Negative,
        UnaryExpressionType.BitwiseNot=>SqlUnaryOperator.BitwiseNot,
        _=>throw Unsupported(expression)
    };
    private static SqlComparisonOperator BindComparisonOperator(BooleanComparisonExpression expression)=>expression.ComparisonType switch{
        BooleanComparisonType.Equals=>SqlComparisonOperator.Equals,
        BooleanComparisonType.NotEqualToBrackets or BooleanComparisonType.NotEqualToExclamation=>SqlComparisonOperator.NotEqual,
        BooleanComparisonType.GreaterThan=>SqlComparisonOperator.GreaterThan,
        BooleanComparisonType.LessThan=>SqlComparisonOperator.LessThan,
        BooleanComparisonType.GreaterThanOrEqualTo or BooleanComparisonType.NotLessThan=>SqlComparisonOperator.GreaterThanOrEqualTo,
        BooleanComparisonType.LessThanOrEqualTo or BooleanComparisonType.NotGreaterThan=>SqlComparisonOperator.LessThanOrEqualTo,
        _=>throw Unsupported(expression)
    };
}
