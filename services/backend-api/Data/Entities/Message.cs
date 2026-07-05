using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("messages")]
[Index("ThreadId", "SentAt", Name = "idx_messages_thread")]
public partial class Message
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("thread_id")]
    public Guid ThreadId { get; set; }

    [Column("sender_id")]
    public Guid SenderId { get; set; }

    [Column("content")]
    public string Content { get; set; } = null!;

    [Column("sent_at")]
    public DateTime SentAt { get; set; }

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [ForeignKey("SenderId")]
    [InverseProperty("Messages")]
    public virtual User Sender { get; set; } = null!;

    [ForeignKey("ThreadId")]
    [InverseProperty("Messages")]
    public virtual MessageThread Thread { get; set; } = null!;
}
