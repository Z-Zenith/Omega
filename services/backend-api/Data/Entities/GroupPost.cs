using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("group_posts")]
[Index("GroupId", "CreatedAt", Name = "idx_group_posts_group", IsDescending = new[] { false, true })]
public partial class GroupPost
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("group_id")]
    public Guid GroupId { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("AuthorId")]
    [InverseProperty("GroupPosts")]
    public virtual User Author { get; set; } = null!;

    [ForeignKey("GroupId")]
    [InverseProperty("GroupPosts")]
    public virtual Group Group { get; set; } = null!;
}
