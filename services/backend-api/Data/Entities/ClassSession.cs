using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("class_sessions")]
[Index("TimetableSlotId", "SessionDate", Name = "class_sessions_timetable_slot_id_session_date_key", IsUnique = true)]
public partial class ClassSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("timetable_slot_id")]
    public Guid TimetableSlotId { get; set; }

    [Column("session_date")]
    public DateOnly SessionDate { get; set; }

    [Column("actual_teacher_id")]
    public Guid? ActualTeacherId { get; set; }

    [ForeignKey("ActualTeacherId")]
    [InverseProperty("ClassSessions")]
    public virtual User? ActualTeacher { get; set; }

    [InverseProperty("ClassSession")]
    public virtual ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("ClassSession")]
    public virtual ICollection<SuspiciousFlag> SuspiciousFlags { get; set; } = new List<SuspiciousFlag>();

    [ForeignKey("TimetableSlotId")]
    [InverseProperty("ClassSessions")]
    public virtual TimetableSlot TimetableSlot { get; set; } = null!;

    [InverseProperty("ClassSession")]
    public virtual ICollection<UsageTelemetry> UsageTelemetries { get; set; } = new List<UsageTelemetry>();
}
