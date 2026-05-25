using System;
using System.Linq;
using System.Linq.Expressions;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query
{
    /// <summary>
    /// The provider that backs all intermediate <see cref="TemplateQueryable{T}"/> nodes.
    /// <see cref="CreateQuery{TElement}"/> wraps the new expression in another template node;
    /// <see cref="Execute"/> always throws.
    /// </summary>
    sealed class TemplateQueryProvider : IQueryProvider
    {

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TemplateQueryable<TElement>(this, expression);

        public IQueryable CreateQuery(Expression expression)
        {
            var elementType = expression.Type.GetGenericArguments()[0];
            var queryableType = typeof(TemplateQueryable<>).MakeGenericType(elementType);
            return (IQueryable)Activator.CreateInstance(
                queryableType,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                [this, expression],
                null)!;
        }

        public TResult Execute<TResult>(Expression expression) =>
            throw new NotSupportedException("TemplateQueryable is not executable.");

        public object Execute(Expression expression) =>
            throw new NotSupportedException("TemplateQueryable is not executable.");

    }

}
