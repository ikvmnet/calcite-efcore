using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.EntityFrameworkCore.Core;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query
{

    /// <summary>
    /// A queryable that records LINQ operator calls as an <see cref="Expression"/> tree without executing anything. Use <see cref="Create{T}"/> to seed the chain, compose
    /// standard LINQ operators (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, …) against it, then read <see cref="IQueryable.Expression"/> to obtain the captured tree.
    /// <para>
    /// At query-execution time, pass the captured expression to <see cref="TemplateQueryable.Apply"/>
    /// together with a fresh <see cref="IQueryable{T}"/> root (e.g. <c>context.Set&lt;T&gt;()</c>)
    /// to substitute the template root and obtain a real, executable <see cref="IQueryable"/>.
    /// </para>
    /// </summary>
    public static class TemplateQueryable
    {

        static readonly MethodInfo CreateGenericMethod = typeof(TemplateQueryable).GetMethod(nameof(Create), []) ?? throw new InvalidOperationException();
        static readonly MethodInfo SetMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), 1, [])!;

        /// <summary>
        /// Creates a root <see cref="IQueryable{T}"/> whose expression tree is a
        /// <see cref="ConstantExpression"/> pointing back to itself. Compose LINQ operators
        /// on top of this and then read <see cref="IQueryable.Expression"/>.
        /// </summary>
        public static IQueryable<T> Create<T>()
        {
            return new TemplateQueryable<T>();
        }

        /// <summary>
        /// Creates a root <see cref="IQueryable"/> for the given element type at runtime.
        /// </summary>
        public static IQueryable Create(Type elementType)
        {
            ArgumentNullException.ThrowIfNull(elementType);
            return (IQueryable)CreateGenericMethod.MakeGenericMethod(elementType).Invoke(null, null)!;
        }

        /// <summary>
        /// Replaces every dynamic-parameter placeholder (<c>?0</c>, <c>?1</c>, …) in <paramref name="expression"/>
        /// with the value <paramref name="getDynamicValue"/> supplies for it.
        /// </summary>
        /// <remarks>
        /// Placeholders have to be bound before the expression is compiled, not after: a free
        /// <see cref="ParameterExpression"/> anywhere in the tree makes <see cref="Expression{TDelegate}.Compile()"/>
        /// throw, so a plan that carries an <c>OFFSET</c> or <c>FETCH</c> parameter never reaches
        /// <see cref="Apply"/> to have it substituted there.
        /// </remarks>
        /// <param name="expression">The expression to bind placeholders in.</param>
        /// <param name="getDynamicValue">Supplies the value bound to the parameter of the given ordinal.</param>
        /// <returns>The expression with every placeholder replaced by a constant.</returns>
        public static Expression BindDynamicParameters(Expression expression, Func<int, object?> getDynamicValue)
        {
            ArgumentNullException.ThrowIfNull(expression);
            ArgumentNullException.ThrowIfNull(getDynamicValue);

            return new DynamicParameterBinder(getDynamicValue).Visit(expression);
        }

        /// <summary>
        /// Converts a value bound to a dynamic parameter into a constant of the type the placeholder declared.
        /// </summary>
        /// <param name="value">The value Calcite bound, which arrives boxed the Java way.</param>
        /// <param name="type">The CLR type the placeholder was typed as.</param>
        /// <returns>The constant to substitute.</returns>
        static ConstantExpression ToConstant(object? value, Type type)
        {
            var clr = CalciteValueConverter.FromJavaObject(value);
            if (clr is not null && type.IsInstanceOfType(clr) == false)
            {
                var target = Nullable.GetUnderlyingType(type) ?? type;
                if (clr is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
                    clr = Convert.ChangeType(clr, target);
            }

            return Expression.Constant(clr, type);
        }

        /// <summary>
        /// Replaces dynamic-parameter placeholders with the values bound to them.
        /// </summary>
        /// <param name="getDynamicValue">Supplies the value bound to the parameter of the given ordinal.</param>
        sealed class DynamicParameterBinder(Func<int, object?> getDynamicValue) : ExpressionVisitor
        {

            /// <inheritdoc />
            protected override Expression VisitParameter(ParameterExpression node)
            {
                var name = (node.Name ?? "").AsSpan();
                if (name.StartsWith("?") && int.TryParse(name.Slice(1), out var index))
                    return ToConstant(getDynamicValue(index), node.Type);

                return base.VisitParameter(node);
            }

        }

        /// <summary>
        /// Replaces every template root inside <paramref name="template"/>'s expression tree with
        /// the corresponding <see cref="DbSet{TEntity}"/> from <paramref name="context"/> and
        /// returns an executable <see cref="IQueryable"/> against the real EF Core provider.
        /// </summary>
        /// <param name="template">
        /// A composed <see cref="TemplateQueryable{T}"/> chain whose
        /// <see cref="IQueryable.Expression"/> contains one or more template-root constants.
        /// </param>
        /// <param name="getDynamicValue"></param>
        /// <param name="context">
        /// The <see cref="DbContext"/> from which <see cref="DbContext.Set{TEntity}"/> is called
        /// to supply the real query roots.
        /// </param>
        public static IQueryable Apply(IQueryable template, Func<int, object> getDynamicValue, DbContext context)
        {
            var replacer = new TemplateRootReplacer(getDynamicValue, context);
            var rewritten = replacer.Visit(template.Expression);
            return replacer.Provider!.CreateQuery(rewritten);
        }

        sealed class TemplateRootReplacer(Func<int, object> GetDynamicValue, DbContext Context) : ExpressionVisitor
        {

            public IQueryProvider? Provider { get; private set; }

            /// <inheritdoc />
            protected override Expression VisitParameter(ParameterExpression node)
            {
                // replace parameters that represent a DynamicParam (e.g. ?0, ?1, ?2) with the corresponding entry from the constants array
                var name = (node.Name ?? "").AsSpan();
                if (name.StartsWith("?"))
                    if (int.TryParse(name.Slice(1), out var index))
                        return ToConstant(GetDynamicValue(index), node.Type);

                return base.VisitParameter(node);
            }

            /// <inheritdoc />
            protected override Expression VisitConstant(ConstantExpression node)
            {
                // replace the root Expression in the template with the appropriate DbSet queryable
                if (node.Value is IQueryable root and ITemplateRoot)
                {
                    var dbSet = (IQueryable)SetMethod.MakeGenericMethod(root.ElementType).Invoke(Context, null)!;
                    Provider ??= dbSet.Provider;
                    return dbSet.Expression;
                }

                return node;
            }

        }

    }

    /// <summary>
    /// The strongly-typed implementation of a template queryable node.
    /// </summary>
    public sealed class TemplateQueryable<T> : IOrderedQueryable<T>, ITemplateRoot
    {

        readonly Expression _expression;
        readonly IQueryProvider _provider;

        /// <summary>
        /// Creates the root node. The expression is a <see cref="ConstantExpression"/> pointing
        /// to <c>this</c> so that <see cref="TemplateQueryable.TemplateRootReplacer"/> can find it.
        /// </summary>
        internal TemplateQueryable()
        {
            _provider = new TemplateQueryProvider();
            _expression = Expression.Constant(this);
        }

        /// <summary>
        /// Creates an intermediate node produced by <see cref="TemplateQueryProvider.CreateQuery{TElement}"/>.
        /// </summary>
        internal TemplateQueryable(IQueryProvider provider, Expression expression)
        {
            _provider = provider;
            _expression = expression;
        }

        /// <inheritdoc />
        public Type ElementType => typeof(T);

        /// <inheritdoc />
        public Expression Expression => _expression;

        /// <inheritdoc />
        public IQueryProvider Provider => _provider;

        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator()
        {
            throw new NotSupportedException("TemplateQueryable is not executable. Call TemplateQueryable.Apply() to obtain a real IQueryable.");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

}
