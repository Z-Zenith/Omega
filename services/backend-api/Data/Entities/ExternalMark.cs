using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("external_marks")]
public partial class ExternalMark
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [Column("grade")]
    public string Grade { get; set; } = null!;

    [Column("submitted_by")]
    public Guid SubmittedBy { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("approved")]
    public bool Approved { get; set; }

    [Column("approved_by")]
    public Guid? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("published")]
    public bool Published { get; set; }

    [ForeignKey("ApprovedBy")]
    [InverseProperty("ExternalMarkApprovedByNavigations")]
    public virtual User? ApprovedByNavigation { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("ExternalMarkStudents")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("SubjectId")]
    [InverseProperty("ExternalMarks")]
    public virtual Subject Subject { get; set; } = null!;

    [ForeignKey("SubmittedBy")]
    [InverseProperty("ExternalMarkSubmittedByNavigations")]
    public virtual User SubmittedByNavigation { get; set; } = null!;
}
