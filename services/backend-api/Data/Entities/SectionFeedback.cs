using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("section_feedback")]
public partial class SectionFeedback
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("section_id")]
    public Guid SectionId { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    [Column("comments")]
    public string? Comments { get; set; }

    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [ForeignKey("SectionId")]
    [InverseProperty("SectionFeedbacks")]
    public virtual Section Section { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("SectionFeedbacks")]
    public virtual User Teacher { get; set; } = null!;
}
