using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

// AIS-01: raw per-visit log the browsing summary is generated from. Distinct from
// browsing_history_summaries, which stores the generated summary text itself.
[Table("browsing_history")]
[Index("StudentId", "VisitedAt", Name = "idx_browsing_history_student_time", IsDescending = new[] { false, true })]
public partial class BrowsingHistory
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("url")]
    public string Url { get; set; } = null!;

    [Column("visited_at")]
    public DateTime VisitedAt { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("BrowsingHistories")]
    public virtual User Student { get; set; } = null!;
}
