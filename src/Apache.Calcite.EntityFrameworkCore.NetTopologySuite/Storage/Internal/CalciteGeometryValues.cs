using System;

using NetTopologySuite.Geometries;

namespace Apache.Calcite.EntityFrameworkCore.NetTopologySuite.Storage.Internal;

/// <summary>
/// Carries a geometry between NetTopologySuite and the JTS one Calcite holds a <c>GEOMETRY</c> in.
/// </summary>
/// <remarks>
/// NetTopologySuite is the .NET port of JTS, so the two have the same model and the same formats, and
/// well-known binary is the one they both write exactly: coordinates as their doubles rather than as text
/// rounded to some number of digits, and the SRID alongside them. So a geometry is carried across as bytes
/// written by one library's writer and read by the other's reader, and nothing is converted by hand.
/// <para>
/// Well-known <em>text</em> would have worked too — it is what the ADO surface reads a <c>GEOMETRY</c> as,
/// and what a parameter may be given — but it is a rounding of the value rather than the value, and it
/// carries no SRID. The binary is used in both directions instead.
/// </para>
/// </remarks>
static class CalciteGeometryValues
{

    /// <summary>
    /// Three ordinates and the SRID: what a geometry has to keep to come back the same geometry.
    /// </summary>
    const int OutputDimension = 3;

    [ThreadStatic]
    static org.locationtech.jts.io.WKBWriter? _jtsWriter;

    [ThreadStatic]
    static org.locationtech.jts.io.WKBReader? _jtsReader;

    [ThreadStatic]
    static global::NetTopologySuite.IO.WKBWriter? _ntsWriter;

    [ThreadStatic]
    static global::NetTopologySuite.IO.WKBReader? _ntsReader;

    /// <summary>
    /// Returns the NetTopologySuite geometry for the JTS one Calcite produced.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static Geometry? ToNetTopologySuite(object? value)
    {
        if (value is null)
            return null;

        if (value is not org.locationtech.jts.geom.Geometry geometry)
            throw new InvalidCastException($"A GEOMETRY is held in a JTS Geometry, and a {value.GetType()} is not one.");

        _jtsWriter ??= new org.locationtech.jts.io.WKBWriter(OutputDimension, true);
        _ntsReader ??= new global::NetTopologySuite.IO.WKBReader { HandleSRID = true };

        return _ntsReader.Read(_jtsWriter.write(geometry));
    }

    /// <summary>
    /// Returns the JTS geometry Calcite takes for the NetTopologySuite one the model holds.
    /// </summary>
    /// <param name="geometry"></param>
    /// <returns></returns>
    public static object? ToCalcite(Geometry? geometry)
    {
        if (geometry is null)
            return null;

        _ntsWriter ??= new global::NetTopologySuite.IO.WKBWriter(global::NetTopologySuite.IO.ByteOrder.LittleEndian, true, true) { Strict = false };
        _jtsReader ??= new org.locationtech.jts.io.WKBReader();

        return _jtsReader.read(_ntsWriter.Write(geometry));
    }

}
