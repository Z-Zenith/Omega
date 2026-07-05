using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("event_registrations")]
[Index("EventId", "StudentId", Name = "event_registrations_event_id_student_id_key", IsUnique = true)]
public partial class EventRegistration
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("student_id")]
    public Guid StudentId { get; set; }

    [Column("registered_at")]
    public DateTime RegisteredAt { get; set; }

    [ForeignKey("EventId")]
    [InverseProperty("EventRegistrations")]
    public virtual Event Event { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("EventRegistrations")]
    public virtual User Student { get; set; } = null!;
}
