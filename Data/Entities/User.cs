using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BackendApi.Data.Entities;

[Table("users")]
[Index("CollegeId", "Identifier", Name = "users_college_id_identifier_key", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("college_id")]
    public Guid CollegeId { get; set; }

    [Column("identifier")]
    public string Identifier { get; set; } = null!;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = null!;

    [Column("totp_secret")]
    public string? TotpSecret { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("department_id")]
    public Guid? DepartmentId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("date_of_birth")]
    public DateOnly? DateOfBirth { get; set; }

    [InverseProperty("Teacher")]
    public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();

    [InverseProperty("MarkedByNavigation")]
    public virtual ICollection<AttendanceRecord> AttendanceRecordMarkedByNavigations { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("Student")]
    public virtual ICollection<AttendanceRecord> AttendanceRecordStudents { get; set; } = new List<AttendanceRecord>();

    [InverseProperty("Student")]
    public virtual ICollection<BrowsingHistorySummary> BrowsingHistorySummaries { get; set; } = new List<BrowsingHistorySummary>();

    [InverseProperty("ActualTeacher")]
    public virtual ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();

    [ForeignKey("CollegeId")]
    [InverseProperty("Users")]
    public virtual College College { get; set; } = null!;

    [InverseProperty("Student")]
    public virtual ICollection<CustomCalendarEntry> CustomCalendarEntries { get; set; } = new List<CustomCalendarEntry>();

    [ForeignKey("DepartmentId")]
    [InverseProperty("Users")]
    public virtual Department? Department { get; set; }

    [InverseProperty("Owner")]
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();

    [InverseProperty("Student")]
    public virtual ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Event> Events { get; set; } = new List<Event>();

    [InverseProperty("ApprovedByNavigation")]
    public virtual ICollection<ExternalMark> ExternalMarkApprovedByNavigations { get; set; } = new List<ExternalMark>();

    [InverseProperty("Student")]
    public virtual ICollection<ExternalMark> ExternalMarkStudents { get; set; } = new List<ExternalMark>();

    [InverseProperty("SubmittedByNavigation")]
    public virtual ICollection<ExternalMark> ExternalMarkSubmittedByNavigations { get; set; } = new List<ExternalMark>();

    [InverseProperty("Student")]
    public virtual ICollection<FeeRecord> FeeRecords { get; set; } = new List<FeeRecord>();

    [InverseProperty("User")]
    public virtual ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();

    [InverseProperty("Author")]
    public virtual ICollection<GroupPost> GroupPosts { get; set; } = new List<GroupPost>();

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();

    [InverseProperty("PublishedByNavigation")]
    public virtual ICollection<InternalMark> InternalMarkPublishedByNavigations { get; set; } = new List<InternalMark>();

    [InverseProperty("Student")]
    public virtual ICollection<InternalMark> InternalMarkStudents { get; set; } = new List<InternalMark>();

    [InverseProperty("UploadedByNavigation")]
    public virtual ICollection<Material> Materials { get; set; } = new List<Material>();

    [InverseProperty("Student")]
    public virtual ICollection<MessageThread> MessageThreadStudents { get; set; } = new List<MessageThread>();

    [InverseProperty("Teacher")]
    public virtual ICollection<MessageThread> MessageThreadTeachers { get; set; } = new List<MessageThread>();

    [InverseProperty("Sender")]
    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    [InverseProperty("Owner")]
    public virtual ICollection<Note> Notes { get; set; } = new List<Note>();

    [InverseProperty("Recipient")]
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    [InverseProperty("ParentUser")]
    public virtual ICollection<ParentWard> ParentWardParentUsers { get; set; } = new List<ParentWard>();

    [InverseProperty("Student")]
    public virtual ICollection<ParentWard> ParentWardStudents { get; set; } = new List<ParentWard>();

    [InverseProperty("GrantedByNavigation")]
    public virtual ICollection<PermissionGrant> PermissionGrantGrantedByNavigations { get; set; } = new List<PermissionGrant>();

    [InverseProperty("User")]
    public virtual ICollection<PermissionGrant> PermissionGrantUsers { get; set; } = new List<PermissionGrant>();

    [InverseProperty("User")]
    public virtual ICollection<RoleBinding> RoleBindings { get; set; } = new List<RoleBinding>();

    [InverseProperty("Student")]
    public virtual ICollection<SectionEnrollment> SectionEnrollments { get; set; } = new List<SectionEnrollment>();

    [InverseProperty("Teacher")]
    public virtual ICollection<SectionFeedback> SectionFeedbacks { get; set; } = new List<SectionFeedback>();

    [InverseProperty("Teacher")]
    public virtual ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    [InverseProperty("Student")]
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();

    [InverseProperty("Student")]
    public virtual ICollection<SuspiciousFlag> SuspiciousFlags { get; set; } = new List<SuspiciousFlag>();

    [InverseProperty("Student")]
    public virtual ICollection<TeacherFeedback> TeacherFeedbackStudents { get; set; } = new List<TeacherFeedback>();

    [InverseProperty("Teacher")]
    public virtual ICollection<TeacherFeedback> TeacherFeedbackTeachers { get; set; } = new List<TeacherFeedback>();

    [InverseProperty("Student")]
    public virtual ICollection<TeacherReport> TeacherReportStudents { get; set; } = new List<TeacherReport>();

    [InverseProperty("Teacher")]
    public virtual ICollection<TeacherReport> TeacherReportTeachers { get; set; } = new List<TeacherReport>();

    [InverseProperty("Teacher")]
    public virtual ICollection<TeacherSectionAssignment> TeacherSectionAssignments { get; set; } = new List<TeacherSectionAssignment>();

    [InverseProperty("ReviewedByNavigation")]
    public virtual ICollection<TimetableChangeRequest> TimetableChangeRequestReviewedByNavigations { get; set; } = new List<TimetableChangeRequest>();

    [InverseProperty("Teacher")]
    public virtual ICollection<TimetableChangeRequest> TimetableChangeRequestTeachers { get; set; } = new List<TimetableChangeRequest>();

    [InverseProperty("Teacher")]
    public virtual ICollection<TimetableSlot> TimetableSlots { get; set; } = new List<TimetableSlot>();

    [InverseProperty("Student")]
    public virtual ICollection<Todo> Todos { get; set; } = new List<Todo>();

    [InverseProperty("Student")]
    public virtual ICollection<UsageTelemetry> UsageTelemetries { get; set; } = new List<UsageTelemetry>();

    // #130/#132 — a user accumulates many historical UserSession rows over time (one per
    // login); only one may be IsActive=true at once (enforced by the partial unique index
    // uniq_user_active_session, not a full one). This was previously scaffolded/configured as
    // a singular one-to-one navigation, which doesn't match that reality: EF's change tracker
    // (especially the InMemory test provider) then treats a second tracked UserSession for the
    // same user as replacing the first instead of coexisting alongside it, silently losing the
    // older row. Fixed to the one-to-many shape the schema actually has.
    [InverseProperty("User")]
    public virtual ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();

    [InverseProperty("RequestedByNavigation")]
    public virtual ICollection<WhitelistRequest> WhitelistRequestRequestedByNavigations { get; set; } = new List<WhitelistRequest>();

    [InverseProperty("ReviewedByNavigation")]
    public virtual ICollection<WhitelistRequest> WhitelistRequestReviewedByNavigations { get; set; } = new List<WhitelistRequest>();
}
