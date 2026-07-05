using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("suspicious_flags")]
[Index("StudentId", "FlaggedAt", Name = "idx_suspicious_flags_student", IsDescending = new[] { false, true })]
public partial class SuspiciousFlag
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("class_session_id")]
    public Guid? ClassSessionId { get; set; }

    [Column("assignment_id")]
    public Guid? AssignmentId { get; set; }

    [Column("confidence_score")]
    public decimal ConfidenceScore { get; set; }

    [Column("flagged_at")]
    public DateTime FlaggedAt { get; set; }

    [ForeignKey("AssignmentId")]
    [InverseProperty("SuspiciousFlags")]
    public virtual Assignment? Assignment { get; set; }

    [ForeignKey("ClassSessionId")]
    [InverseProperty("SuspiciousFlags")]
    public virtual ClassSession? ClassSession { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("SuspiciousFlags")]
    public virtual User Student { get; set; } = null!;
}
