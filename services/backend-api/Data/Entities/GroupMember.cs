using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("group_members")]
[Index("GroupId", "UserId", Name = "group_members_group_id_user_id_key", IsUnique = true)]
public partial class GroupMember
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("group_id")]
    public Guid GroupId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [ForeignKey("GroupId")]
    [InverseProperty("GroupMembers")]
    public virtual Group Group { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("GroupMembers")]
    public virtual User User { get; set; } = null!;
}
