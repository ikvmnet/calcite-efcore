using System;

using Apache.Calcite.EntityFrameworkCore.Query.Expressions.Internal;

using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apache.Calcite.EntityFrameworkCore.Query.Internal
{

    /// <inheritdoc/>
    public class CalciteSqlExpressionFactory : SqlExpressionFactory
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependencies"></param>
        public CalciteSqlExpressionFactory(SqlExpressionFactoryDependencies dependencies) :
            base(dependencies)
        {

        }

        /// <summary>
        /// Creates a <see cref="CalciteBinaryExpression"/> for a Calcite-specific binary operator.
        /// </summary>
        public virtual CalciteBinaryExpression CalciteBinary(CalciteExpressionType operatorType, SqlExpression left, SqlExpression right, Type resultType)
        {
            var typeMapping = Dependencies.TypeMappingSource.FindMapping(resultType, Dependencies.Model);
            left = (SqlExpression)ApplyDefaultTypeMapping(left);
            right = (SqlExpression)ApplyDefaultTypeMapping(right);
            return new CalciteBinaryExpression(operatorType, left, right, resultType, typeMapping);
        }

        /// <summary>
        /// Creates <c>EXTRACT(unit FROM operand)</c>, typed <see cref="long"/> as Calcite returns it. A
        /// sub-second unit counts from the start of the minute: <c>EXTRACT(MILLISECOND …)</c> of 56.789
        /// seconds is 56789.
        /// </summary>
        public virtual SqlExpression Extract(string unit, SqlExpression operand)
            => Function(
                "__extract",
                [Fragment(unit), operand],
                nullable: true,
                argumentsPropagateNullability: [false, true],
                typeof(long),
                FindMapping(typeof(long)));

        /// <summary>
        /// Creates <c>TIMESTAMPADD(unit, count, operand)</c>, typed as <paramref name="operand"/>. Calcite
        /// clamps a month or year step to the last day of the month, as <see cref="DateTime.AddMonths"/>
        /// does, and wraps a <c>TIME</c>, as <see cref="TimeOnly.AddHours(double)"/> does.
        /// </summary>
        public virtual SqlExpression TimestampAdd(string unit, SqlExpression count, SqlExpression operand)
            => Function(
                "TIMESTAMPADD",
                [Fragment(unit), ApplyDefaultTypeMapping(count), operand],
                nullable: true,
                argumentsPropagateNullability: [false, true, true],
                operand.Type,
                operand.TypeMapping);

        /// <summary>
        /// Adds a possibly fractional number of units to <paramref name="operand"/> as whole milliseconds,
        /// because <c>TIMESTAMPADD</c> takes an integer count and the store keeps milliseconds.
        /// </summary>
        public virtual SqlExpression TimestampAddMilliseconds(SqlExpression amount, double millisecondsPerUnit, SqlExpression operand)
        {
            var milliseconds = millisecondsPerUnit == 1
                ? amount
                : Multiply(ApplyDefaultTypeMapping(amount), Constant(millisecondsPerUnit, FindMapping(typeof(double))));

            return TimestampAdd("MILLISECOND", Convert(milliseconds, typeof(long), FindMapping(typeof(long))), operand);
        }

        /// <summary>
        /// Translates the date or time part named by a .NET member — <c>Year</c> through <c>Nanosecond</c>,
        /// <c>DayOfYear</c> and <c>DayOfWeek</c> — or returns <see langword="null"/> for any other name.
        /// </summary>
        /// <remarks>
        /// Calcite answers every part as <c>BIGINT</c>, so each is cast to the <see cref="int"/> the member
        /// returns. Its <c>DAYOFWEEK</c> counts Sunday as 1 where <see cref="DayOfWeek"/> counts it as 0, and
        /// its sub-second <c>EXTRACT</c> units count from the start of the minute, so those are taken modulo
        /// 1000 to leave the part the member reports.
        /// </remarks>
        public virtual SqlExpression? DatePart(string member, SqlExpression operand)
        {
            return member switch
            {
                nameof(DateTime.Year) => Part("YEAR"),
                nameof(DateTime.Month) => Part("MONTH"),
                nameof(DateTime.Day) => Part("DAYOFMONTH"),
                nameof(DateTime.DayOfYear) => Part("DAYOFYEAR"),
                nameof(DateTime.Hour) => Part("HOUR"),
                nameof(DateTime.Minute) => Part("MINUTE"),
                nameof(DateTime.Second) => Part("SECOND"),
                nameof(DateTime.Millisecond) => SubSecond("MILLISECOND"),
                nameof(DateTime.Microsecond) => SubSecond("MICROSECOND"),
                nameof(DateTime.Nanosecond) => SubSecond("NANOSECOND"),
                nameof(DateTime.DayOfWeek) => DayOfWeek(),
                _ => null,
            };

            SqlExpression Part(string function)
                => ToInt(Function(function, [operand], nullable: true, argumentsPropagateNullability: [true], typeof(long), FindMapping(typeof(long))));

            SqlExpression SubSecond(string unit)
                => ToInt(Function(
                    "MOD",
                    [Extract(unit, operand), Constant(1000L, FindMapping(typeof(long)))],
                    nullable: true,
                    argumentsPropagateNullability: [true, false],
                    typeof(long),
                    FindMapping(typeof(long))));

            SqlExpression DayOfWeek()
            {
                var mapping = (RelationalTypeMapping)FindMapping(typeof(int))!.WithComposedConverter(new EnumToNumberConverter<DayOfWeek, int>());
                return Convert(Subtract(Part("DAYOFWEEK"), Constant(1, FindMapping(typeof(int)))), typeof(DayOfWeek), mapping);
            }

            SqlExpression ToInt(SqlExpression expression)
                => Convert(expression, typeof(int), FindMapping(typeof(int)));
        }

        RelationalTypeMapping? FindMapping(Type type)
            => Dependencies.TypeMappingSource.FindMapping(type, Dependencies.Model);

    }

}
