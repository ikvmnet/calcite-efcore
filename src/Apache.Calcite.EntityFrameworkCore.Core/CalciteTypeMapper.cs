using System;
using System.Collections.Generic;

using java.util;

using Microsoft.EntityFrameworkCore.Metadata;

using org.apache.calcite.rel.type;
using org.apache.calcite.sql.type;

namespace Apache.Calcite.EntityFrameworkCore.Core
{

    /// <summary>
    /// Maps between CLR types and Calcite <see cref="SqlTypeName"/> / <see cref="RelDataType"/>.
    /// Shared by the EF Core provider and the Calcite adapter so both use identical mappings.
    /// </summary>
    public static class CalciteTypeMapper
    {

        /// <summary>
        /// Returns the Calcite <see cref="SqlTypeName"/> that best represents the given CLR type.
        /// <paramref name="clrType"/> should be the non-nullable underlying type (call <see cref="Nullable.GetUnderlyingType"/> first if needed).
        /// </summary>
        public static SqlTypeName ToSqlTypeName(Type clrType)
        {
            ArgumentNullException.ThrowIfNull(clrType);

            if (clrType == typeof(bool)) return SqlTypeName.BOOLEAN;
            if (clrType == typeof(sbyte)) return SqlTypeName.TINYINT;
            if (clrType == typeof(byte)) return SqlTypeName.UTINYINT;
            if (clrType == typeof(short)) return SqlTypeName.SMALLINT;
            if (clrType == typeof(ushort)) return SqlTypeName.USMALLINT;
            if (clrType == typeof(int)) return SqlTypeName.INTEGER;
            if (clrType == typeof(uint)) return SqlTypeName.UINTEGER;
            if (clrType == typeof(long)) return SqlTypeName.BIGINT;
            if (clrType == typeof(ulong)) return SqlTypeName.UBIGINT;
            if (clrType == typeof(float)) return SqlTypeName.FLOAT;
            if (clrType == typeof(double)) return SqlTypeName.DOUBLE;
            if (clrType == typeof(decimal)) return SqlTypeName.DECIMAL;
            if (clrType == typeof(char)) return SqlTypeName.CHAR;
            if (clrType == typeof(string)) return SqlTypeName.VARCHAR;
            if (clrType == typeof(DateTime)) return SqlTypeName.TIMESTAMP;
            if (clrType == typeof(DateTimeOffset)) return SqlTypeName.TIMESTAMP_TZ;
            if (clrType == typeof(DateOnly)) return SqlTypeName.DATE;
            if (clrType == typeof(TimeOnly)) return SqlTypeName.TIME;
            if (clrType == typeof(TimeSpan)) return SqlTypeName.INTERVAL_DAY_SECOND;
            if (clrType == typeof(Guid)) return SqlTypeName.UUID;
            if (clrType == typeof(byte[])) return SqlTypeName.VARBINARY;

            return SqlTypeName.VARCHAR;
        }

        /// <summary>
        /// Creates a <see cref="RelDataType"/> for the given EF Core <see cref="IProperty"/>, using
        /// the property's CLR type and any precision/scale annotations.
        /// </summary>
        public static RelDataType ToRelDataType(RelDataTypeFactory typeFactory, IProperty property)
        {
            ArgumentNullException.ThrowIfNull(typeFactory);
            ArgumentNullException.ThrowIfNull(property);

            var clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;

            if (clrType == typeof(decimal))
                return typeFactory.createSqlType(SqlTypeName.DECIMAL, property.GetPrecision() ?? 28, property.GetScale() ?? 4);
            if (clrType == typeof(char))
                return typeFactory.createSqlType(SqlTypeName.CHAR, 1);

            return typeFactory.createSqlType(ToSqlTypeName(clrType));
        }

        /// <summary>
        /// Returns the CLR type that best represents the given Calcite <see cref="RelDataType"/>.
        /// Struct (row) types are mapped to a generated dynamic CLR type via <see cref="ClrDataTypeGenerator"/>.
        /// Scalar types respect nullability: nullable value types are returned as <see cref="Nullable{T}"/>.
        /// Returns <see cref="object"/> if the scalar type name has no direct CLR counterpart.
        /// </summary>
        public static Type ToClrType(RelDataType relDataType)
        {
            if (relDataType.isStruct())
                return ToClrType(relDataType.getFieldList().AsList<RelDataTypeField>().AsReadOnly());

            var baseType = ToClrType(relDataType.getSqlTypeName().name()) ?? typeof(object);
            if (relDataType.isNullable() && baseType.IsValueType)
                return typeof(Nullable<>).MakeGenericType(baseType);
            else
                return baseType;
        }

        /// <summary>
        /// Derives a dynamic CLR record type from an ordered list of <see cref="RelDataTypeField"/> instances.
        /// Use when only a field subset is needed (e.g. a group key projection).
        /// </summary>
        public static Type ToClrType(IReadOnlyCollection<RelDataTypeField> fields)
        {
            return ClrDataTypeGenerator.GetOrCreate(fields);
        }

        /// <summary>
        /// Returns the CLR type that best represents the given Calcite <see cref="SqlTypeName"/>.
        /// Returns <see langword="null"/> if the type name has no direct CLR counterpart.
        /// </summary>
        public static Type? ToClrType(SqlTypeName typeName) => ToClrType(typeName.name());

        /// <summary>
        /// Returns the CLR type that best represents the given Calcite SQL type name.
        /// Returns <see langword="null"/> if the type name has no direct CLR counterpart.
        /// </summary>
        public static Type? ToClrType(string typeName) => typeName switch
        {
            "BOOLEAN" => typeof(bool),
            "TINYINT" => typeof(sbyte),
            "UTINYINT" => typeof(byte),
            "SMALLINT" => typeof(short),
            "USMALLINT" => typeof(ushort),
            "INTEGER" => typeof(int),
            "UINTEGER" => typeof(uint),
            "BIGINT" => typeof(long),
            "UBIGINT" => typeof(ulong),
            "FLOAT" or "REAL" => typeof(float),
            "DOUBLE" => typeof(double),
            "DECIMAL" => typeof(decimal),
            "CHAR" or "VARCHAR" => typeof(string),
            "BINARY" or "VARBINARY" => typeof(byte[]),
            "DATE" => typeof(DateOnly),
            "TIME" or "TIME_WITH_LOCAL_TIME_ZONE" => typeof(TimeOnly),
            "TIMESTAMP" => typeof(DateTime),
            "TIMESTAMP_WITH_LOCAL_TIME_ZONE" or "TIMESTAMP_TZ" => typeof(DateTimeOffset),
            "INTERVAL_YEAR" or "INTERVAL_YEAR_MONTH" or
            "INTERVAL_MONTH" or "INTERVAL_DAY" or
            "INTERVAL_DAY_HOUR" or "INTERVAL_DAY_MINUTE" or
            "INTERVAL_DAY_SECOND" or "INTERVAL_HOUR" or
            "INTERVAL_HOUR_MINUTE" or "INTERVAL_HOUR_SECOND" or
            "INTERVAL_MINUTE" or "INTERVAL_MINUTE_SECOND" or
            "INTERVAL_SECOND" => typeof(TimeSpan),
            "UUID" => typeof(Guid),
            _ => null
        };

    }

}
