using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("teacher_section_assignments")]
[Index("TeacherId", "SectionId", "SubjectId", Name = "teacher_section_assignments_teacher_id_section_id_subject_i_key", IsUnique = true)]
public partial class TeacherSectionAssignment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("section_id")]
    public Guid SectionId { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [ForeignKey("SectionId")]
    [InverseProperty("TeacherSectionAssignments")]
    public virtual Section Section { get; set; } = null!;

    [ForeignKey("SubjectId")]
    [InverseProperty("TeacherSectionAssignments")]
    public virtual Subject Subject { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("TeacherSectionAssignments")]
    public virtual User Teacher { get; set; } = null!;
}
