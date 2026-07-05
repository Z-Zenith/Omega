using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("teacher_reports")]
public partial class TeacherReport
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("section_id")]
    public Guid? SectionId { get; set; }

    [Column("student_id")]
    public Guid? StudentId { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [ForeignKey("SectionId")]
    [InverseProperty("TeacherReports")]
    public virtual Section? Section { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("TeacherReportStudents")]
    public virtual User? Student { get; set; }

    [ForeignKey("TeacherId")]
    [InverseProperty("TeacherReportTeachers")]
    public virtual User Teacher { get; set; } = null!;
}
