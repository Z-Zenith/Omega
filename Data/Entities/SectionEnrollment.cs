using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("section_enrollments")]
[Index("SectionId", "StudentId", Name = "section_enrollments_section_id_student_id_key", IsUnique = true)]
public partial class SectionEnrollment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("section_id")]
    public Guid SectionId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [ForeignKey("SectionId")]
    [InverseProperty("SectionEnrollments")]
    public virtual Section Section { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("SectionEnrollments")]
    public virtual User Student { get; set; } = null!;
}
