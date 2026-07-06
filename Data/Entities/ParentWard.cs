using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("parent_wards")]
[Index("StudentId", Name = "idx_parent_wards_student")]
[Index("ParentUserId", "StudentId", Name = "parent_wards_parent_user_id_student_id_key", IsUnique = true)]
public partial class ParentWard
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("parent_user_id")]
    public Guid ParentUserId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ParentUserId")]
    [InverseProperty("ParentWardParentUsers")]
    public virtual User ParentUser { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("ParentWardStudents")]
    public virtual User Student { get; set; } = null!;
}
