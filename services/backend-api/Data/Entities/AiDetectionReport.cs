using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("ai_detection_reports")]
public partial class AiDetectionReport
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("submission_id")]
    public Guid SubmissionId { get; set; }

    [Column("ai_likelihood_score")]
    public decimal AiLikelihoodScore { get; set; }

    [Column("pangram_report_id")]
    public string? PangramReportId { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    [ForeignKey("SubmissionId")]
    [InverseProperty("AiDetectionReports")]
    public virtual Submission Submission { get; set; } = null!;
}
