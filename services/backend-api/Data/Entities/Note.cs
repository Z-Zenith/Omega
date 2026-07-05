using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("notes")]
public partial class Note
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("owner_id")]
    public Guid OwnerId { get; set; }

    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("content_markdown")]
    public string ContentMarkdown { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [InverseProperty("FromNote")]
    public virtual ICollection<NoteLink> NoteLinkFromNotes { get; set; } = new List<NoteLink>();

    [InverseProperty("ToNote")]
    public virtual ICollection<NoteLink> NoteLinkToNotes { get; set; } = new List<NoteLink>();

    [ForeignKey("OwnerId")]
    [InverseProperty("Notes")]
    public virtual User Owner { get; set; } = null!;
}
