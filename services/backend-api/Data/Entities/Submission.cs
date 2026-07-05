using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("submissions")]
[Index("AssignmentId", "StudentId", Name = "idx_submissions_assignment")]
public partial class Submission
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("assignment_id")]
    public Guid AssignmentId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("content_url")]
    public string ContentUrl { get; set; } = null!;

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [Column("is_late")]
    public bool IsLate { get; set; }

    [Column("is_autosubmitted")]
    public bool IsAutosubmitted { get; set; }

    [InverseProperty("Submission")]
    public virtual ICollection<AiDetectionReport> AiDetectionReports { get; set; } = new List<AiDetectionReport>();

    [ForeignKey("AssignmentId")]
    [InverseProperty("Submissions")]
    public virtual Assignment Assignment { get; set; } = null!;

    [InverseProperty("Submission")]
    public virtual ICollection<AutogradeSuggestion> AutogradeSuggestions { get; set; } = new List<AutogradeSuggestion>();

    [InverseProperty("SubmissionA")]
    public virtual ICollection<CopyCheckFlag> CopyCheckFlagSubmissionAs { get; set; } = new List<CopyCheckFlag>();

    [InverseProperty("SubmissionB")]
    public virtual ICollection<CopyCheckFlag> CopyCheckFlagSubmissionBs { get; set; } = new List<CopyCheckFlag>();

    [InverseProperty("Submission")]
    public virtual ICollection<PlagiarismReport> PlagiarismReports { get; set; } = new List<PlagiarismReport>();

    [ForeignKey("StudentId")]
    [InverseProperty("Submissions")]
    public virtual User Student { get; set; } = null!;
}
