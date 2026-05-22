using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;

using Microsoft.EntityFrameworkCore.Storage;

namespace Apache.Calcite.EntityFrameworkCore.Storage.Internal
{

    public class CalciteTypeMappingSource : RelationalTypeMappingSource
    {

        static readonly Dictionary<Type, RelationalTypeMapping> _clrTypeMappings = new()
        {
            [typeof(bool)] = CalciteBoolTypeMapping.Default,
            [typeof(byte)] = CalciteByteTypeMapping.Default,
            [typeof(sbyte)] = CalciteSByteTypeMapping.Default,
            [typeof(char)] = CalciteCharTypeMapping.Default,
            [typeof(short)] = CalciteShortTypeMapping.Default,
            [typeof(ushort)] = CalciteUShortTypeMapping.Default,
            [typeof(int)] = CalciteIntTypeMapping.Default,
            [typeof(uint)] = CalciteUIntTypeMapping.Default,
            [typeof(long)] = CalciteLongTypeMapping.Default,
            [typeof(ulong)] = CalciteULongTypeMapping.Default,
            [typeof(float)] = CalciteFloatTypeMapping.Default,
            [typeof(double)] = CalciteDoubleTypeMapping.Default,
            [typeof(decimal)] = CalciteDecimalTypeMapping.Default,
            [typeof(DateTime)] = CalciteDateTimeTypeMapping.Default,
            [typeof(DateTimeOffset)] = CalciteDateTimeOffsetTypeMapping.Default,
            [typeof(DateOnly)] = CalciteDateOnlyTypeMapping.Default,
            [typeof(TimeOnly)] = CalciteTimeOnlyTypeMapping.Default,
            [typeof(string)] = CalciteStringTypeMapping.Default,
            [typeof(byte[])] = CalciteByteArrayTypeMapping.Default,
        };

        static readonly Dictionary<string, RelationalTypeMapping[]> _storeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOLEAN"] = [CalciteBoolTypeMapping.Default],
            ["TINYINT UNSIGNED"] = [CalciteByteTypeMapping.Default],
            ["CHAR(1)"] = [CalciteCharTypeMapping.Default],
            ["SMALLINT"] = [CalciteShortTypeMapping.Default],
            ["INTEGER"] = [CalciteIntTypeMapping.Default],
            ["BIGINT"] = [CalciteLongTypeMapping.Default],
            ["REAL"] = [CalciteFloatTypeMapping.Default],
            ["DOUBLE"] = [CalciteDoubleTypeMapping.Default],
            ["DECIMAL"] = [CalciteDecimalTypeMapping.Default],
            ["DECIMAL(28, 4)"] = [CalciteDecimalTypeMapping.Default],
            ["DATE"] = [CalciteDateOnlyTypeMapping.Default],
            ["TIME"] = [CalciteTimeOnlyTypeMapping.Default],
            ["TIMESTAMP"] = [CalciteDateTimeTypeMapping.Default],
            ["TIMESTAMP WITH TIME ZONE"] = [CalciteDateTimeOffsetTypeMapping.Default],
            ["VARCHAR"] = [CalciteStringTypeMapping.Default],
            ["VARBINARY"] = [CalciteByteArrayTypeMapping.Default],
        };

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        /// <param name="relationalDependencies"></param>
        public CalciteTypeMappingSource(TypeMappingSourceDependencies dependencies, RelationalTypeMappingSourceDependencies relationalDependencies) :
            base(dependencies, relationalDependencies)
        {

        }

        /// <inheritdoc/>
        protected override RelationalTypeMapping? FindMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            var mapping = base.FindMapping(mappingInfo)
                ?? FindRawMapping(mappingInfo);

            return mapping != null
                && mappingInfo.StoreTypeName != null
                    ? mapping.WithStoreTypeAndSize(mappingInfo.StoreTypeName, null)
                    : mapping;
        }

        /// <summary>
        /// Finds the type mapping.
        /// </summary>
        /// <param name="mappingInfo"></param>
        /// <returns></returns>
        RelationalTypeMapping? FindRawMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            if (clrType != null && _clrTypeMappings.TryGetValue(clrType, out var mapping))
                return mapping;

            var storeTypeName = mappingInfo.StoreTypeName;
            if (storeTypeName != null && _storeTypeMappings.TryGetValue(storeTypeName, out var mappings))
                foreach (var m in mappings)
                    if (clrType == null || (Nullable.GetUnderlyingType(m.ClrType) ?? m.ClrType) == clrType)
                        return m;

            var storeTypeNameBase = mappingInfo.StoreTypeNameBase;
            if (storeTypeNameBase != null && _storeTypeMappings.TryGetValue(storeTypeNameBase, out var baseMappings))
                foreach (var m in baseMappings)
                    if (clrType == null || (Nullable.GetUnderlyingType(m.ClrType) ?? m.ClrType) == clrType)
                        return m;

            return null;
        }

    }

}
