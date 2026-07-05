using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("custom_calendar_entries")]
[Index("StudentId", "EntryDate", Name = "idx_custom_calendar_student")]
public partial class CustomCalendarEntry
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("entry_date")]
    public DateOnly EntryDate { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("CustomCalendarEntries")]
    public virtual User Student { get; set; } = null!;
}
