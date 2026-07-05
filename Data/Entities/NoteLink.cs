using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("note_links")]
[Index("FromNoteId", "ToNoteId", Name = "note_links_from_note_id_to_note_id_key", IsUnique = true)]
public partial class NoteLink
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("from_note_id")]
    public Guid FromNoteId { get; set; }

    [Column("to_note_id")]
    public Guid ToNoteId { get; set; }

    [Column("anchor")]
    public string Anchor { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FromNoteId")]
    [InverseProperty("NoteLinkFromNotes")]
    public virtual Note FromNote { get; set; } = null!;

    [ForeignKey("ToNoteId")]
    [InverseProperty("NoteLinkToNotes")]
    public virtual Note ToNote { get; set; } = null!;
}
