using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("groups")]
[Index("CollegeId", Name = "idx_groups_college")]
public partial class Group
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("college_id")]
    public Guid CollegeId { get; set; }

    [Column("name")]
    public string Name { get; set; } = null!;

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("section_id")]
    public Guid? SectionId { get; set; }

    [ForeignKey("CollegeId")]
    [InverseProperty("Groups")]
    public virtual College College { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    [InverseProperty("Groups")]
    public virtual User? CreatedByNavigation { get; set; }

    [InverseProperty("Group")]
    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    [InverseProperty("Group")]
    public virtual ICollection<GroupPost> GroupPosts { get; set; } = new List<GroupPost>();

    [InverseProperty("Group")]
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();

    [ForeignKey("SectionId")]
    [InverseProperty("Groups")]
    public virtual Section? Section { get; set; }
}
