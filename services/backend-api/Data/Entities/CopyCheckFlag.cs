using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("copy_check_flags")]
public partial class CopyCheckFlag
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("submission_a_id")]
    public Guid SubmissionAId { get; set; }

    [Column("submission_b_id")]
    public Guid SubmissionBId { get; set; }

    [Column("similarity_score")]
    public decimal SimilarityScore { get; set; }

    [Column("flagged_at")]
    public DateTime FlaggedAt { get; set; }

    [ForeignKey("SubmissionAId")]
    [InverseProperty("CopyCheckFlagSubmissionAs")]
    public virtual Submission SubmissionA { get; set; } = null!;

    [ForeignKey("SubmissionBId")]
    [InverseProperty("CopyCheckFlagSubmissionBs")]
    public virtual Submission SubmissionB { get; set; } = null!;
}
