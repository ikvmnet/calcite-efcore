using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

using Apache.Calcite.EntityFrameworkCore.Core.Reflection;

using org.apache.calcite.rel.type;

namespace Apache.Calcite.EntityFrameworkCore.Core
{

    /// <summary>
    /// Generates and caches dynamic CLR record types used to represent intermediate row shapes in EF Core adapter nodes.
    /// Each unique ordered sequence of <see cref="RelDataTypeField"/> instances produces exactly one generated type,
    /// keyed by a structural digest of field names and CLR types.
    /// Generated types are sealed classes that implement value equality (<see cref="IEquatable{T}"/>,
    /// <see cref="object.Equals(object)"/>, <see cref="object.GetHashCode"/>) over all properties,
    /// mirroring the semantics of a C# <c>record</c> type.
    /// </summary>
    static class ClrDataTypeGenerator
    {

        static readonly AssemblyBuilder AssemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName("Apache.Calcite.EntityFrameworkCore.DynamicRows"),
                AssemblyBuilderAccess.RunAndCollect);

        static readonly ModuleBuilder ModuleBuilder =
            AssemblyBuilder.DefineDynamicModule("DynamicRows");

        static readonly ConcurrentDictionary<IReadOnlyCollection<RelDataTypeField>, Type> Cache =
            new(ReadOnlyCollectionComparer<RelDataTypeField>.Default);

        static int _typeIndex;

        /// <summary>
        /// Returns a cached record type whose properties correspond to the given <paramref name="fields"/> in order.
        /// </summary>
        internal static Type GetOrCreate(IReadOnlyCollection<RelDataTypeField> fields)
        {
            return Cache.GetOrAdd(fields, static f => Build(f));
        }

        static Type Build(IReadOnlyCollection<RelDataTypeField> fields)
        {
            var index = Interlocked.Increment(ref _typeIndex);
            var tb = ModuleBuilder.DefineType(
                $"<DynamicRow>_{index}",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit);

            var equatableInterface = typeof(IEquatable<>).MakeGenericType(tb);
            tb.AddInterfaceImplementation(equatableInterface);

            var backingFields = new FieldBuilder[fields.Count];

            // ── Properties ────────────────────────────────────────────────────────────────
            var i = 0;
            foreach (var field in fields)
            {
                var name = field.getName();
                var clrType = CalciteTypeMapper.ToClrType(field.getType());
                var bf = tb.DefineField($"<{name}>k__BackingField", clrType, FieldAttributes.Private);
                var pb = tb.DefineProperty(name, PropertyAttributes.HasDefault, clrType, null);

                var getter = tb.DefineMethod($"get_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    clrType, Type.EmptyTypes);
                var g = getter.GetILGenerator();
                g.Emit(OpCodes.Ldarg_0); g.Emit(OpCodes.Ldfld, bf); g.Emit(OpCodes.Ret);
                pb.SetGetMethod(getter);

                var setter = tb.DefineMethod($"set_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null, [clrType]);
                var s = setter.GetILGenerator();
                s.Emit(OpCodes.Ldarg_0); s.Emit(OpCodes.Ldarg_1); s.Emit(OpCodes.Stfld, bf); s.Emit(OpCodes.Ret);
                pb.SetSetMethod(setter);

                backingFields[i++] = bf;
            }

            // ── Equals(T other) ───────────────────────────────────────────────────────────
            var equalsT = tb.DefineMethod("Equals",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(bool), [tb]);
            {
                var il = equalsT.GetILGenerator();
                var notNull = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Brtrue_S, notNull);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ret);
                il.MarkLabel(notNull);
                foreach (var bf in backingFields)
                {
                    var defaultProp = EqualityComparerMethods.Default(bf.FieldType);
                    var equalsMethod = EqualityComparerMethods.Equals(bf.FieldType);
                    var next = il.DefineLabel();
                    il.Emit(OpCodes.Call, defaultProp);
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, bf);
                    il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldfld, bf);
                    il.Emit(OpCodes.Callvirt, equalsMethod);
                    il.Emit(OpCodes.Brtrue_S, next);
                    il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
                    il.MarkLabel(next);
                }
                il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ret);
            }

            // ── Equals(object? obj) ───────────────────────────────────────────────────────
            var equalsObj = tb.DefineMethod("Equals",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(bool), [typeof(object)]);
            {
                var il = equalsObj.GetILGenerator();
                var cast = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Isinst, tb);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, cast);
                il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ret);
                il.MarkLabel(cast);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, equalsT);
                il.Emit(OpCodes.Ret);
            }

            // ── GetHashCode() ─────────────────────────────────────────────────────────────
            var getHashCode = tb.DefineMethod("GetHashCode",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
                typeof(int), Type.EmptyTypes);
            {
                var il = getHashCode.GetILGenerator();
                var hcLocal = il.DeclareLocal(typeof(HashCode));
                var addMethod = HashCodeMethods.Add;
                var toHashCode = HashCodeMethods.ToHashCode;
                il.Emit(OpCodes.Ldloca_S, hcLocal);
                il.Emit(OpCodes.Initobj, typeof(HashCode));
                foreach (var bf in backingFields)
                {
                    var addGeneric = addMethod.MakeGenericMethod(bf.FieldType);
                    il.Emit(OpCodes.Ldloca_S, hcLocal);
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, bf);
                    il.Emit(OpCodes.Call, addGeneric);
                }
                il.Emit(OpCodes.Ldloca_S, hcLocal);
                il.Emit(OpCodes.Call, toHashCode);
                il.Emit(OpCodes.Ret);
            }

            // ── == / != operators ─────────────────────────────────────────────────────────
            var opEq = tb.DefineMethod("op_Equality",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(bool), [tb, tb]);
            {
                var il = opEq.GetILGenerator();
                var notNullLeft = il.DefineLabel();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Brtrue_S, notNullLeft);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Ret);
                il.MarkLabel(notNullLeft);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, equalsT);
                il.Emit(OpCodes.Ret);
            }

            var opNe = tb.DefineMethod("op_Inequality",
                MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(bool), [tb, tb]);
            {
                var il = opNe.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Call, opEq);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Ceq);
                il.Emit(OpCodes.Ret);
            }

            tb.DefineMethodOverride(equalsT, TypeBuilder.GetMethod(equatableInterface, EqualityComparerMethods.IEquatableEquals));

            return tb.CreateType()!;
        }

    }

}
