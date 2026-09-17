using System;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;

using Apache.Calcite.Data;

using Microsoft.EntityFrameworkCore.Storage;

using NetTopologySuite.Geometries;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Storage.Internal;

/// <summary>
/// Maps a NetTopologySuite geometry onto Calcite's <c>GEOMETRY</c>.
/// </summary>
/// <remarks>
/// The value that crosses is the JTS geometry Calcite holds, in both directions. Reading it means asking the
/// reader for what Calcite produced rather than for its reading of it: the converting accessors answer a
/// <c>GEOMETRY</c> as well-known text, by design and the way Calcite's own JDBC does, so
/// <see cref="CalciteDataReader.GetCalciteValue" /> is the one that hands over the geometry itself. Writing it
/// means handing a JTS geometry back — measured, a <c>GEOMETRY</c> parameter takes one.
/// <para>
/// Which leaves only the two libraries' own formats to bridge, and <see cref="CalciteGeometryValues" /> does
/// that in binary so no coordinate is rounded and the SRID survives.
/// </para>
/// </remarks>
public class CalciteGeometryTypeMapping : RelationalTypeMapping
{

    /// <summary>
    /// The store type every geometry maps to. Calcite has one: <c>GEOMETRY</c> carries the kind of shape in
    /// the value rather than in the column, so there is no <c>POINT</c> or <c>POLYGON</c> column to declare.
    /// </summary>
    public const string StoreTypeName = "GEOMETRY";

    static readonly MethodInfo _getCalciteValue =
        typeof(CalciteDataReader).GetMethod(nameof(CalciteDataReader.GetCalciteValue), [typeof(int)])
        ?? throw new InvalidOperationException($"{nameof(CalciteDataReader)} has no {nameof(CalciteDataReader.GetCalciteValue)}(int).");

    static readonly MethodInfo _toNetTopologySuite =
        typeof(CalciteGeometryValues).GetMethod(nameof(CalciteGeometryValues.ToNetTopologySuite))!;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="clrType">The geometry type the property is declared as.</param>
    public CalciteGeometryTypeMapping(Type clrType) :
        this(new RelationalTypeMappingParameters(new CoreTypeMappingParameters(clrType), StoreTypeName))
    {

    }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="parameters"></param>
    protected CalciteGeometryTypeMapping(RelationalTypeMappingParameters parameters) :
        base(parameters)
    {

    }

    /// <inheritdoc />
    protected override RelationalTypeMapping Clone(RelationalTypeMappingParameters parameters)
    {
        return new CalciteGeometryTypeMapping(parameters);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The converting accessors read a <c>GEOMETRY</c> as text; this one reads what Calcite produced.
    /// </remarks>
    /// <returns></returns>
    public override MethodInfo GetDataReaderMethod()
    {
        return _getCalciteValue;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The reader hands over a JTS geometry as an <see cref="object" />, so the shaper is given the
    /// conversion to the type the property declared. Casting to that type rather than to
    /// <see cref="Geometry" /> is deliberate: a property declared as a <see cref="Point" /> has to be given
    /// one, and a column holding some other shape fails as the mismatch it is.
    /// </remarks>
    /// <param name="expression"></param>
    /// <returns></returns>
    public override Expression CustomizeDataReaderExpression(Expression expression)
    {
        Expression read = Expression.Call(_toNetTopologySuite, expression);

        return ClrType == typeof(Geometry) ? read : Expression.Convert(read, ClrType);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A geometry parameter is given the JTS geometry, which is what the store holds and what the driver
    /// takes. Nothing here says a <see cref="DbType" />: a <c>GEOMETRY</c> is not one of them, and naming one
    /// would describe the value as something it is not.
    /// </remarks>
    /// <param name="parameter"></param>
    protected override void ConfigureParameter(DbParameter parameter)
    {
        if (parameter.Value is Geometry geometry)
            parameter.Value = CalciteGeometryValues.ToCalcite(geometry);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A literal geometry is the text of it handed to the function that reads one. The text alone would be a
    /// <c>VARCHAR</c>, which no spatial function accepts.
    /// </remarks>
    /// <param name="value"></param>
    /// <returns></returns>
    protected override string GenerateNonNullSqlLiteral(object value)
    {
        var text = new global::NetTopologySuite.IO.WKTWriter { OutputOrdinates = Ordinates.XYZ }.Write((Geometry)value);

        return $"ST_GeomFromWKT('{text.Replace("'", "''")}')";
    }

}
