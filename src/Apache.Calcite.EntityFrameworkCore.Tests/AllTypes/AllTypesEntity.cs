using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Apache.Calcite.EntityFrameworkCore.Tests.AllTypes
{

    [Table("ALL_TYPES")]
    public class AllTypesEntity
    {

        [Key]
        [Column("ID")]
        public int Id { get; set; }

        [Column("COL_BOOL")]
        public bool? ColBool { get; set; }

        [Column("COL_SHORT")]
        public short? ColShort { get; set; }

        [Column("COL_INT")]
        public int? ColInt { get; set; }

        [Column("COL_LONG")]
        public long? ColLong { get; set; }

        [Column("COL_FLOAT")]
        public float? ColFloat { get; set; }

        [Column("COL_DOUBLE")]
        public double? ColDouble { get; set; }

        [Column("COL_DECIMAL")]
        public decimal? ColDecimal { get; set; }

        [Column("COL_DATETIME")]
        public DateTime? ColDateTime { get; set; }

        [Column("COL_DATETIMEOFFSET")]
        public DateTimeOffset? ColDateTimeOffset { get; set; }

        [Column("COL_DATEONLY")]
        public DateOnly? ColDateOnly { get; set; }

        [Column("COL_TIMEONLY")]
        public TimeOnly? ColTimeOnly { get; set; }

        [Column("COL_STRING")]
        public string? ColString { get; set; }

        [Column("COL_BYTES")]
        public byte[]? ColBytes { get; set; }

    }

}
