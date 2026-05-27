using System.Linq.Expressions;

using org.apache.calcite.rex;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Rex
{

    /// <summary>
    /// A delegate that receives the already-translated CLR <see cref="Expression"/> operands for a
    /// <see cref="RexCall"/> and returns the <see cref="Expression"/> that implements the function.
    /// </summary>
    /// <remarks>
    /// Returning a complete <see cref="Expression"/> directly—rather than a metadata record describing
    /// how to build one—keeps dispatch logic in the table and removes the need for a separate interpreter.
    /// </remarks>
    public delegate Expression SqlOperatorTranslator(Expression[] operands);

}
