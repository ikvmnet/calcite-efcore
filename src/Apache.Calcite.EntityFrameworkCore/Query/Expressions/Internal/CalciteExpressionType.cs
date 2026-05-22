namespace Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal
{

    /// <summary>
    /// Calcite-specific binary operator types that have no equivalent in <see cref="System.Linq.Expressions.ExpressionType"/>
    /// or are not supported by <see cref="Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlBinaryExpression"/>.
    /// </summary>
    public enum CalciteExpressionType
    {

        /// <summary>
        /// Left shift (x &lt;&lt; n).
        /// </summary>
        LeftShift,

        /// <summary>
        /// Right shift (x &gt;&gt; n).
        /// </summary>
        RightShift,

    }

}
