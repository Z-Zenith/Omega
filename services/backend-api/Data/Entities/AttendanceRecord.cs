using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("attendance_records")]
[Index("ClassSessionId", "StudentId", Name = "attendance_records_class_session_id_student_id_key", IsUnique = true)]
public partial class AttendanceRecord
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("class_session_id")]
    public Guid ClassSessionId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("marked_at")]
    public DateTime MarkedAt { get; set; }

    [Column("marked_by")]
    public Guid MarkedBy { get; set; }

    [ForeignKey("ClassSessionId")]
    [InverseProperty("AttendanceRecords")]
    public virtual ClassSession ClassSession { get; set; } = null!;

    [ForeignKey("MarkedBy")]
    [InverseProperty("AttendanceRecordMarkedByNavigations")]
    public virtual User MarkedByNavigation { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("AttendanceRecordStudents")]
    public virtual User Student { get; set; } = null!;
}
