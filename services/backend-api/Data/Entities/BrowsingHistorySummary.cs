using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("browsing_history_summaries")]
public partial class BrowsingHistorySummary
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("summary_text")]
    public string SummaryText { get; set; } = null!;

    [Column("generated_at")]
    public DateTime GeneratedAt { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("BrowsingHistorySummaries")]
    public virtual User Student { get; set; } = null!;
}
