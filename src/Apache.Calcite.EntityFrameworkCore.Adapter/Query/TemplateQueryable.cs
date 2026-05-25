using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.EntityFrameworkCore;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query
{

    /// <summary>
    /// A queryable that records LINQ operator calls as an <see cref="Expression"/> tree without
    /// executing anything. Use <see cref="TemplateQueryable.Create{T}"/> to seed the chain, compose
    /// standard LINQ operators (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, …) against it, then
    /// read <see cref="IQueryable.Expression"/> to obtain the captured tree.
    ///
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
        /// Replaces every template root inside <paramref name="template"/>'s expression tree with
        /// the corresponding <see cref="DbSet{TEntity}"/> from <paramref name="context"/> and
        /// returns an executable <see cref="IQueryable"/> against the real EF Core provider.
        /// </summary>
        /// <param name="template">
        /// A composed <see cref="TemplateQueryable{T}"/> chain whose
        /// <see cref="IQueryable.Expression"/> contains one or more template-root constants.
        /// </param>
        /// <param name="context">
        /// The <see cref="DbContext"/> from which <see cref="DbContext.Set{TEntity}"/> is called
        /// to supply the real query roots.
        /// </param>
        public static IQueryable Apply(IQueryable template, DbContext context)
        {
            var replacer = new TemplateRootReplacer(context);
            var rewritten = replacer.Visit(template.Expression);
            return replacer.Provider!.CreateQuery(rewritten);
        }

        sealed class TemplateRootReplacer(DbContext context) : ExpressionVisitor
        {

            public IQueryProvider? Provider { get; private set; }

            protected override Expression VisitConstant(ConstantExpression node)
            {
                if (node.Value is IQueryable root and ITemplateRoot)
                {
                    var dbSet = (IQueryable)SetMethod.MakeGenericMethod(root.ElementType).Invoke(context, null)!;
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
    public sealed class TemplateQueryable<T> : IQueryable<T>, ITemplateRoot
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
            throw new NotSupportedException("TemplateQueryable is not executable. Call TemplateQueryable.Replay() to obtain a real IQueryable.");
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

}
