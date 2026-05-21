using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

using Apache.Calcite.EntityFrameworkCore.Metadata;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Apache.Calcite.EntityFrameworkCore.ValueGeneration
{

    /// <inheritdoc />
    public class CalciteEntitySequenceHiLoValueGenerator<TValue> : HiLoValueGenerator<TValue>
    {

        interface IEntitySequenceInvoker
        {

            long GetNextValue();

            Task<long> GetNextValueAsync(CancellationToken cancellationToken);

        }

        sealed class EntitySequenceInvoker<TEntity>(CalciteEntitySequenceHiLoValueGenerator<TValue> generator) : IEntitySequenceInvoker
            where TEntity : class
        {

            public long GetNextValue() => generator.GetNextValueCore<TEntity>();

            public Task<long> GetNextValueAsync(CancellationToken cancellationToken) => generator.GetNextValueCoreAsync<TEntity>(cancellationToken);

        }

        readonly ICalciteEntitySequence _sequence;
        readonly ICurrentDbContext _currentDbContext;
        readonly IRelationalCommandDiagnosticsLogger _commandLogger;
        readonly IEntitySequenceInvoker _invoker;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="currentDbContext"></param>
        /// <param name="generatorState"></param>
        /// <param name="commandLogger"></param>
        public CalciteEntitySequenceHiLoValueGenerator(ICurrentDbContext currentDbContext, CalciteEntitySequenceGeneratorState generatorState, IRelationalCommandDiagnosticsLogger commandLogger) :
            base(generatorState)
        {
            _sequence = generatorState.EntitySequence;
            _currentDbContext = currentDbContext;
            _commandLogger = commandLogger;

            var invokerType = typeof(EntitySequenceInvoker<>).MakeGenericType(typeof(TValue), _sequence.EntityType.ClrType);
            _invoker = (IEntitySequenceInvoker)Activator.CreateInstance(invokerType, this)!;
        }

        /// <inheritdoc/>
        protected override long GetNewLowValue()
        {
            return _invoker.GetNextValue();
        }

        /// <inheritdoc/>
        protected override async Task<long> GetNewLowValueAsync(CancellationToken cancellationToken = default)
        {
            return await _invoker.GetNextValueAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the next low value by atomically incrementing the entity sequence row
        /// and reading back the new value.
        /// </summary>
        private long GetNextValueCore<TEntity>() where TEntity : class
        {
            var context = _currentDbContext.Context;
            var query = BuildFilteredQuery<TEntity>(context);
            query.ExecuteUpdate(BuildSetPropertyAction<TEntity>());
            return query.Select(BuildValueSelector<TEntity>()).Single();
        }

        /// <summary>
        /// Async version of <see cref="GetNextValueCore{TEntity}"/>
        /// </summary>
        private async Task<long> GetNextValueCoreAsync<TEntity>(CancellationToken cancellationToken) where TEntity : class
        {
            var context = _currentDbContext.Context;
            var query = BuildFilteredQuery<TEntity>(context);
            await query.ExecuteUpdateAsync(BuildSetPropertyAction<TEntity>(), cancellationToken).ConfigureAwait(false);
            return await query.Select(BuildValueSelector<TEntity>()).SingleAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds an <see cref="IQueryable{TEntity}"/> filtered to the sequence row by its primary key value.
        /// </summary>
        IQueryable<TEntity> BuildFilteredQuery<TEntity>(DbContext context) where TEntity : class
        {
            IQueryable<TEntity> query = context.Set<TEntity>();

            if (_sequence.KeyValue is { } keyValue)
                query = query.Where(BuildKeyEqualsExpression<TEntity>(keyValue));

            return query;
        }

        /// <summary>
        /// Builds <c>e =&gt; e.&lt;PrimaryKeyProperty&gt; == keyValue</c>.
        /// </summary>
        Expression<Func<TEntity, bool>> BuildKeyEqualsExpression<TEntity>(object keyValue)
        {
            var keyProperty = _sequence.EntityType.FindPrimaryKey()?.Properties.Single()
                ?? throw new InvalidOperationException($"Entity sequence backing entity '{_sequence.EntityType.DisplayName()}' must have a single primary key property.");

            var clrProperty = typeof(TEntity).GetProperty(keyProperty.Name)
                ?? throw new InvalidOperationException($"Property '{keyProperty.Name}' was not found on '{typeof(TEntity)}'.");

            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var member = Expression.MakeMemberAccess(parameter, clrProperty);
            var constant = Expression.Constant(keyValue, clrProperty.PropertyType);
            return Expression.Lambda<Func<TEntity, bool>>(Expression.Equal(member, constant), parameter);
        }

        /// <summary>
        /// Builds <c>e => (long)e.ValueProperty</c>.
        /// </summary>
        Expression<Func<TEntity, long>> BuildValueSelector<TEntity>()
        {
            var param = Expression.Parameter(typeof(TEntity), "e");
            var property = typeof(TEntity).GetProperty(_sequence.ValueProperty!.Name)!;
            var access = Expression.MakeMemberAccess(param, property);
            return Expression.Lambda<Func<TEntity, long>>(Expression.Convert(access, typeof(long)), param);
        }

        /// <summary>
        /// Builds an <see cref="Action{T}"/> that calls
        /// <c>SetProperty(e => e.ValueProp, e => (TProp)((long)e.ValueProp + incrementBy))</c>
        /// on the <see cref="UpdateSettersBuilder{TEntity}"/>
        /// </summary>
        Action<UpdateSettersBuilder<TEntity>> BuildSetPropertyAction<TEntity>() where TEntity : class
        {
            var property = typeof(TEntity).GetProperty(_sequence.ValueProperty!.Name)!;
            var propertyType = property.PropertyType;
            var funcType = typeof(Func<,>).MakeGenericType(typeof(TEntity), propertyType);

            // Expression<Func<TEntity, TProp>>: e => e.ValueProp
            var selectorParam = Expression.Parameter(typeof(TEntity), "e");
            var selectorAccess = Expression.MakeMemberAccess(selectorParam, property);
            var propSelector = Expression.Lambda(funcType, selectorAccess, selectorParam);

            // Expression<Func<TEntity, TProp>>: e => (TProp)((long)e.ValueProp + incrementBy)
            var factoryParam = Expression.Parameter(typeof(TEntity), "e");
            var factoryAccess = Expression.MakeMemberAccess(factoryParam, property);
            var incremented = Expression.Convert(
                Expression.Add(
                    Expression.Convert(factoryAccess, typeof(long)),
                    Expression.Constant((long)_sequence.IncrementBy)),
                propertyType);
            var valueFactory = Expression.Lambda(funcType, incremented, factoryParam);

            // Resolve SetProperty<TProp>(Expression<Func<TEntity, TProp>>, Expression<Func<TEntity, TProp>>)
            var setPropertyMethod = typeof(UpdateSettersBuilder<TEntity>)
                .GetMethods()
                .First(m => m.Name == nameof(UpdateSettersBuilder<TEntity>.SetProperty)
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 2
                    && m.GetParameters()[1].ParameterType.IsGenericType
                    && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
                .MakeGenericMethod(propertyType);

            return builder => setPropertyMethod.Invoke(builder, [propSelector, valueFactory]);
        }

        /// <inheritdoc/>
        public override bool GeneratesTemporaryValues => false;

    }

}
