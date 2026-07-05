using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("internal_marks")]
[Index("StudentId", "SubjectId", Name = "idx_internal_marks_student_subject")]
public partial class InternalMark
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [Column("assignment_id")]
    public Guid? AssignmentId { get; set; }

    [Column("marks")]
    public decimal Marks { get; set; }

    [Column("published")]
    public bool Published { get; set; }

    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    [Column("published_by")]
    public Guid? PublishedBy { get; set; }

    [ForeignKey("AssignmentId")]
    [InverseProperty("InternalMarks")]
    public virtual Assignment? Assignment { get; set; }

    [ForeignKey("PublishedBy")]
    [InverseProperty("InternalMarkPublishedByNavigations")]
    public virtual User? PublishedByNavigation { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("InternalMarkStudents")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("SubjectId")]
    [InverseProperty("InternalMarks")]
    public virtual Subject Subject { get; set; } = null!;
}
