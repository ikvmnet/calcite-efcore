using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Apache.Calcite.EntityFrameworkCore.Adapter.Query
{

    /// <summary>
    /// Generates and caches dynamic CLR types used to represent intermediate row shapes produced by <see cref="Apache.Calcite.EntityFrameworkCore.Adapter.Rel.EfCoreSelect"/>.
    /// Each unique ordered sequence of <c>(name, CLR type)</c> pairs produces exactly one generated type that is reused across calls.
    /// The generated types have public read/write auto-properties and a public parameterless constructor, satisfying the requirements
    /// of EF Core's <see cref="System.Linq.Expressions.MemberInitExpression"/> translation (same as hand-written DTO projections)
    /// and of LINQ composability — downstream nodes can access named properties rather than indexed <c>object[]</c> slots.
    /// </summary>
    internal static class DynamicRowType
    {

        static readonly AssemblyBuilder AssemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Apache.Calcite.EntityFrameworkCore.DynamicRows"),
                AssemblyBuilderAccess.Run);

        static readonly ModuleBuilder ModuleBuilder =
            AssemblyBuilder.DefineDynamicModule("DynamicRows");

        static readonly ConcurrentDictionary<ShapeKey, Type> Cache = new();

        static int _typeIndex;

        /// <summary>
        /// Returns a cached dynamic type whose public properties match <paramref name="shape"/> in order.
        /// </summary>
        public static Type GetOrCreate((string Name, Type ClrType)[] shape)
        {
            return Cache.GetOrAdd(new ShapeKey(shape), static k => Build(k.Shape));
        }

        static Type Build((string Name, Type ClrType)[] shape)
        {
            var index = Interlocked.Increment(ref _typeIndex);
            var tb = ModuleBuilder.DefineType(
                $"<DynamicRow>_{index}",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

            foreach (var (name, clrType) in shape)
            {
                var backingField = tb.DefineField($"<{name}>k__BackingField", clrType, FieldAttributes.Private);
                var prop = tb.DefineProperty(name, PropertyAttributes.HasDefault, clrType, null);

                var getter = tb.DefineMethod($"get_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    clrType, Type.EmptyTypes);
                var g = getter.GetILGenerator();
                g.Emit(OpCodes.Ldarg_0);
                g.Emit(OpCodes.Ldfld, backingField);
                g.Emit(OpCodes.Ret);
                prop.SetGetMethod(getter);

                var setter = tb.DefineMethod($"set_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, [clrType]);
                var s = setter.GetILGenerator();
                s.Emit(OpCodes.Ldarg_0);
                s.Emit(OpCodes.Ldarg_1);
                s.Emit(OpCodes.Stfld, backingField);
                s.Emit(OpCodes.Ret);
                prop.SetSetMethod(setter);
            }

            return tb.CreateType()!;
        }

        readonly struct ShapeKey : IEquatable<ShapeKey>
        {

            public readonly (string Name, Type ClrType)[] Shape;

            public ShapeKey((string Name, Type ClrType)[] shape) => Shape = shape;

            public bool Equals(ShapeKey other)
            {
                if (Shape.Length != other.Shape.Length)
                    return false;
                for (int i = 0; i < Shape.Length; i++)
                    if (Shape[i] != other.Shape[i])
                        return false;
                return true;
            }

            public override bool Equals(object? obj) => obj is ShapeKey k && Equals(k);

            public override int GetHashCode()
            {
                var h = new HashCode();
                foreach (var (name, type) in Shape)
                    h.Add(HashCode.Combine(name, type));
                return h.ToHashCode();
            }

        }

    }

}
