using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("materials")]
public partial class Material
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("subject_id")]
    public Guid? SubjectId { get; set; }

    [Column("group_id")]
    public Guid? GroupId { get; set; }

    [Column("uploaded_by")]
    public Guid UploadedBy { get; set; }

    [Column("file_url")]
    public string FileUrl { get; set; } = null!;

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }

    [ForeignKey("GroupId")]
    [InverseProperty("Materials")]
    public virtual Group? Group { get; set; }

    [ForeignKey("SubjectId")]
    [InverseProperty("Materials")]
    public virtual Subject? Subject { get; set; }

    [ForeignKey("UploadedBy")]
    [InverseProperty("Materials")]
    public virtual User UploadedByNavigation { get; set; } = null!;
}
