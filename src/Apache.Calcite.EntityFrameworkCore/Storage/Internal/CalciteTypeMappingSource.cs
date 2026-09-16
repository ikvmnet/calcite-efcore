using System;
using System.Collections.Generic;

using Apache.Calcite.EntityFrameworkCore.Storage.Internal.Mapping;
using Apache.Calcite.EntityFrameworkCore.Utilities;

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
        /// The element types a primitive collection is stored as an <c>ARRAY</c> of.
        /// </summary>
        /// <remarks>
        /// An allowlist rather than a denylist, because the cost of being wrong runs one way: an
        /// element type the driver cannot read back out of a collection fails at materialization,
        /// where the JSON storage it would otherwise have had works. The types left out are the ones
        /// whose CLR form differs from what Calcite's runtime holds, which the driver converts for a
        /// scalar but not for an element — <c>char</c> arrives as a string, a <c>DATE</c> as a
        /// <see cref="DateTime"/> rather than a <see cref="DateOnly"/>, an enum as its integer. See
        /// the ARRAY element item in <c>TODO.md</c>; each one moves here once the driver coerces it.
        /// </remarks>
        static readonly HashSet<Type> _arrayElementTypes =
        [
            typeof(bool),
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(string),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(DateOnly),
            typeof(TimeOnly),
            typeof(char),
        ];

        /// <summary>
        /// The collection types a primitive collection is stored as an <c>ARRAY</c> of.
        /// </summary>
        /// <remarks>
        /// The reader builds the collection the property asks for, and it knows how to build these.
        /// A type it does not know — <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/>
        /// and <see cref="System.Collections.ObjectModel.ObservableCollection{T}"/> among them —
        /// fails at materialization, so those keep the JSON storage, which builds the collection in
        /// EF rather than in the driver. An array is handled separately: it is not a generic type.
        /// </remarks>
        static readonly HashSet<Type> _arrayCollectionTypes =
        [
            typeof(List<>),
            typeof(IList<>),
            typeof(ICollection<>),
            typeof(IEnumerable<>),
            typeof(IReadOnlyList<>),
            typeof(IReadOnlyCollection<>),
            typeof(HashSet<>),
            typeof(ISet<>),
        ];

        /// <summary>
        /// Returns whether a CLR collection type is one the reader can build.
        /// </summary>
        /// <param name="collectionType"></param>
        /// <returns></returns>
        static bool IsSupportedArrayCollection(Type collectionType)
        {
            return collectionType.IsArray
                || (collectionType.IsGenericType && _arrayCollectionTypes.Contains(collectionType.GetGenericTypeDefinition()));
        }

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
        /// Maps a CLR collection onto a Calcite <c>ARRAY</c> column rather than onto the JSON string
        /// the relational base falls back to.
        /// </summary>
        /// <remarks>
        /// This is the hook the base calls once nothing has mapped the collection type itself, which
        /// is where <see cref="RelationalTypeMappingSource"/> would compose a
        /// <c>CollectionToJsonStringConverter</c> over a <c>VARCHAR</c>. Calcite has a native array
        /// type, so a primitive collection belongs in one: it is what a reader hands back, what
        /// <c>UNNEST</c> takes, and what someone writing the SQL by hand would expect to find in the
        /// column. A store type the model declares is honored whether or not it names a collection,
        /// so a property can still be pinned to JSON text by asking for <c>VARCHAR</c>.
        ///
        /// <para>The structural comparer and the JSON reader/writer come from the base's own
        /// <see cref="TypeMappingSourceBase.TryFindJsonCollectionMapping"/>: only the storage
        /// strategy changes here, and a collection nested inside a JSON document still reads and
        /// writes through the same machinery every other provider uses.</para>
        /// </remarks>
        /// <param name="info"></param>
        /// <param name="modelType"></param>
        /// <param name="providerType"></param>
        /// <param name="elementMapping"></param>
        /// <returns></returns>
        protected override RelationalTypeMapping? FindCollectionMapping(RelationalTypeMappingInfo info, Type modelType, Type? providerType, CoreTypeMapping? elementMapping)
        {
            // a declared store type that is not a collection is a deliberate choice of storage, and
            // JSON text in a VARCHAR remains reachable that way
            var storeTypeName = info.StoreTypeName;
            if (storeTypeName != null && CalciteArrayTypeMapping.GetElementStoreTypeName(storeTypeName) is null)
                return base.FindCollectionMapping(info, modelType, providerType, elementMapping);

            if (!TryFindJsonCollectionMapping(info.CoreTypeMappingInfo, modelType, providerType, ref elementMapping, out var collectionComparer, out var collectionReaderWriter))
                return null;

            if (elementMapping is not RelationalTypeMapping relationalElementMapping)
                return base.FindCollectionMapping(info, modelType, providerType, elementMapping);

            // a collection the reader cannot build, or an element it cannot read back, keeps the
            // JSON storage — where neither passes through the driver's own conversion at all
            if (!IsSupportedArrayCollection(modelType) || !_arrayElementTypes.Contains(relationalElementMapping.ClrType.UnwrapNullableType()))
                return base.FindCollectionMapping(info, modelType, providerType, elementMapping);

            // the element's own store type is what the column's name is built from, and a declared
            // one wins over whatever the element's CLR type resolved to
            if (CalciteArrayTypeMapping.GetElementStoreTypeName(storeTypeName) is { } declaredElementStoreType &&
                !string.Equals(declaredElementStoreType, relationalElementMapping.StoreType, StringComparison.OrdinalIgnoreCase) &&
                FindMapping(relationalElementMapping.ClrType, declaredElementStoreType) is { } redeclared)
                relationalElementMapping = redeclared;

            return new CalciteArrayTypeMapping(
                storeTypeName ?? CalciteArrayTypeMapping.GetCollectionStoreTypeName(relationalElementMapping.StoreType),
                modelType,
                relationalElementMapping,
                collectionComparer,
                collectionReaderWriter);
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
