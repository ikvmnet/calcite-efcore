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
            [typeof(Guid)] = CalciteGuidTypeMapping.Default,
            [typeof(string)] = CalciteStringTypeMapping.Default,
            [typeof(byte[])] = CalciteByteArrayTypeMapping.Default,
        };

        static readonly CalciteJsonTypeMapping _jsonTypeMapping = CalciteJsonTypeMapping.Default;

        static readonly Dictionary<string, RelationalTypeMapping[]> _storeTypeMappings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BOOLEAN"] = [CalciteBoolTypeMapping.Default],
            ["TINYINT UNSIGNED"] = [CalciteByteTypeMapping.Default],
            ["CHAR(1)"] = [CalciteCharTypeMapping.Default],
            ["SMALLINT"] = [CalciteShortTypeMapping.Default],
            ["INTEGER"] = [CalciteIntTypeMapping.Default],
            ["INT"] = [CalciteIntTypeMapping.Default],
            ["BIGINT"] = [CalciteLongTypeMapping.Default],
            ["REAL"] = [CalciteFloatTypeMapping.Default],
            ["DOUBLE"] = [CalciteDoubleTypeMapping.Default],
            ["DATE"] = [CalciteDateOnlyTypeMapping.Default],
            ["TIME"] = [CalciteTimeOnlyTypeMapping.Default],
            ["TIMESTAMP"] = [CalciteDateTimeTypeMapping.Default],
            ["TIMESTAMP WITH TIME ZONE"] = [CalciteDateTimeOffsetTypeMapping.Default],
            ["VARCHAR"] = [CalciteStringTypeMapping.Default],
            ["CHARACTER VARYING"] = [CalciteStringTypeMapping.Default],
            ["UUID"] = [CalciteGuidTypeMapping.Default],
            ["VARBINARY"] = [CalciteByteArrayTypeMapping.Default],
            ["BINARY VARYING"] = [CalciteByteArrayTypeMapping.Default],
        };

        /// <summary>
        /// Store type names that resolve to the decimal mapping, which is built from the parsed
        /// precision and scale rather than looked up.
        /// </summary>
        static readonly HashSet<string> _decimalStoreTypeNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "DECIMAL",
            "NUMERIC",
            "DEC",
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
            if (mappingInfo.ClrType == typeof(JsonTypePlaceholder))
                return _jsonTypeMapping;

            return base.FindMapping(mappingInfo) ?? FindRawMapping(mappingInfo);
        }

        /// <summary>
        /// Finds the type mapping. A declared store type name wins over the CLR type: the mapping
        /// resolved from the name carries the reader for what the column actually holds, and when
        /// the property's CLR type disagrees with it this returns <see langword="null"/> so the
        /// caller retries with a value converter to the store type's CLR type.
        /// </summary>
        /// <param name="mappingInfo"></param>
        /// <returns></returns>
        RelationalTypeMapping? FindRawMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            var storeTypeName = mappingInfo.StoreTypeName;

            if (storeTypeName != null && TryFindStoreMapping(mappingInfo, out var storeMapping))
            {
                if (clrType != null && clrType != (Nullable.GetUnderlyingType(storeMapping.ClrType) ?? storeMapping.ClrType))
                    return null;

                return storeMapping.WithStoreTypeAndSize(storeTypeName, mappingInfo.Size);
            }

            if (clrType == typeof(decimal))
                return FindDecimalMapping(mappingInfo);

            if (clrType != null && _clrTypeMappings.TryGetValue(clrType, out var mapping))
                return storeTypeName != null
                    ? mapping.WithStoreTypeAndSize(storeTypeName, mappingInfo.Size)
                    : mapping;

            return null;
        }

        /// <summary>
        /// Finds the type mapping for a declared store type name, by the full name first and the
        /// base name second. Decimal names build the mapping from the parsed precision and scale.
        /// </summary>
        /// <param name="mappingInfo"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        bool TryFindStoreMapping(in RelationalTypeMappingInfo mappingInfo, out RelationalTypeMapping mapping)
        {
            var storeTypeName = mappingInfo.StoreTypeName!;
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase;

            if (_decimalStoreTypeNames.Contains(storeTypeNameBase ?? storeTypeName))
            {
                mapping = FindDecimalMapping(mappingInfo);
                return true;
            }

            if (_storeTypeMappings.TryGetValue(storeTypeName, out var mappings))
            {
                mapping = mappings[0];
                return true;
            }

            if (storeTypeNameBase != null && _storeTypeMappings.TryGetValue(storeTypeNameBase, out var baseMappings))
            {
                mapping = baseMappings[0];
                return true;
            }

            mapping = null!;
            return false;
        }

        /// <summary>
        /// Finds the decimal type mapping, honoring the precision and scale of the property. Calcite's maximum
        /// numeric precision is 19; a larger requested precision is reduced to it, giving up scale first so the
        /// integral capacity the precision asked for is preserved.
        /// </summary>
        /// <param name="mappingInfo"></param>
        /// <returns></returns>
        RelationalTypeMapping FindDecimalMapping(in RelationalTypeMappingInfo mappingInfo)
        {
            if (mappingInfo.Precision is not int precision)
                return CalciteDecimalTypeMapping.Default;

            var scale = mappingInfo.Scale ?? 0;
            if (precision > 19)
            {
                scale = Math.Max(0, scale - (precision - 19));
                precision = 19;
            }

            return new CalciteDecimalTypeMapping(precision, scale);
        }

    }

}
