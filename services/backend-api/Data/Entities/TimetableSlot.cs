using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("timetable_slots")]
[Index("SectionId", "DayOfWeek", "StartTime", Name = "idx_timetable_slots_section")]
public partial class TimetableSlot
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("section_id")]
    public Guid SectionId { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("day_of_week")]
    public int DayOfWeek { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [Column("room")]
    public string? Room { get; set; }

    [Column("manually_edited")]
    public bool ManuallyEdited { get; set; }

    [InverseProperty("TimetableSlot")]
    public virtual ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();

    [ForeignKey("SectionId")]
    [InverseProperty("TimetableSlots")]
    public virtual Section Section { get; set; } = null!;

    [ForeignKey("SubjectId")]
    [InverseProperty("TimetableSlots")]
    public virtual Subject Subject { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("TimetableSlots")]
    public virtual User Teacher { get; set; } = null!;
}
