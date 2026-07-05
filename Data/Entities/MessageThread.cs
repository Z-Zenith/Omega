using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("message_threads")]
[Index("StudentId", "TeacherId", Name = "message_threads_student_id_teacher_id_key", IsUnique = true)]
public partial class MessageThread
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("teacher_id")]
    public Guid TeacherId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Thread")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [ForeignKey("StudentId")]
    [InverseProperty("MessageThreadStudents")]
    public virtual User Student { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("MessageThreadTeachers")]
    public virtual User Teacher { get; set; } = null!;
}
