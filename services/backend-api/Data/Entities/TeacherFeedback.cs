using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("teacher_feedback")]
public partial class TeacherFeedback
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    [Column("comments")]
    public string? Comments { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("TeacherFeedbackStudents")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("TeacherFeedbackTeachers")]
    public virtual User Teacher { get; set; } = null!;
}
