using System;
using System.Linq;
using System.Reflection;

using java.lang.reflect;

using Microsoft.EntityFrameworkCore;

using org.apache.calcite.linq4j.tree;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query.Steps
{

    /// <summary>
    /// The seed step in every EF Core queryable pipeline: calls <c>context.Set&lt;T&gt;()</c>
    /// to obtain the initial <see cref="IQueryable{T}"/> for the entity type.
    ///
    /// <para>
    /// This step corresponds to the <see cref="Rel.EfCoreEntityScan"/> physical rel node. It ignores the <c>source</c> argument passed by <see cref="EfCoreEnumerable.Execute"/> because
    /// it is always the first step in the pipeline; there is no prior queryable to chain onto.
    /// </para>
    /// </summary>
    public sealed class EfCoreEntityScanStep : IEfCoreQueryableStep
    {

        static readonly MethodInfo SetMethod =
            typeof(DbContext).GetMethod(nameof(DbContext.Set), 1, System.Type.EmptyTypes)!;

        /// <summary>
        /// The <c>(Class)</c> constructor overload — used by <see cref="ToExpression"/> to emit <c>new EfCoreEntityScanStep(EntityType.class)</c>.
        /// Janino resolves class literals as <c>java.lang.Class</c>, so the overload must accept that type directly.
        /// </summary>
        static readonly Constructor ScanStepCtor =
            ((java.lang.Class)typeof(EfCoreEntityScanStep)).getDeclaredConstructor([(java.lang.Class)typeof(java.lang.Class)]);

        readonly System.Type _entityType;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="entityType">The CLR type of the entity to scan.</param>
        public EfCoreEntityScanStep(System.Type entityType)
        {
            _entityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        }

        /// <summary>
        /// Initializes a new instance from a Java class literal.
        /// Called by Janino-generated code; bridges <c>java.lang.Class</c> to <see cref="System.Type"/> via the IKVM implicit conversion.
        /// </summary>
        /// <param name="entityClass">The Java class object for the entity type.</param>
        public EfCoreEntityScanStep(java.lang.Class entityClass) : this((System.Type)(object)entityClass)
        {
        }

        /// <summary>
        /// Gets the entity CLR type this step scans.
        /// </summary>
        public System.Type EntityType => _entityType;

        /// <inheritdoc />
        public IQueryable Apply(IQueryable? source, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return (IQueryable)SetMethod.MakeGenericMethod(_entityType).Invoke(context, null)!;
        }

        /// <inheritdoc />
        public Expression ToExpression()
        {
            // Emit: new EfCoreEntityScanStep(EntityType.class)
            // Calcite renders a Class constant as a Java class literal. The java.lang.Class-typed constructor overload
            // satisfies Janino's type checker, and IKVM bridges it to System.Type at runtime.
            var classLiteralExpr = Expressions.constant((java.lang.Class)_entityType, (java.lang.Class)typeof(java.lang.Class));
            return Expressions.new_(ScanStepCtor, [classLiteralExpr]);
        }

    }

}
