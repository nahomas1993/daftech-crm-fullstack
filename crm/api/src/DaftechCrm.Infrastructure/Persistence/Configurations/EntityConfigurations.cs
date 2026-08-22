using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DaftechCrm.Infrastructure.Persistence.Configurations;

/// <summary>
/// List&lt;string&gt; and List&lt;EmployeeRole&gt; properties are stored as
/// delimited strings via value converters rather than a native array
/// column — PostgreSQL does support arrays, but a delimited string keeps
/// the column portable and avoids provider-specific mapping — with a
/// value comparer so EF's change tracking sees mutations to the list
/// contents (Add/Remove), not just reference changes.
/// </summary>
internal static class ValueConverters
{
    public static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => string.Join('|', v),
        v => v == string.Empty ? new List<string>() : v.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList()
    );

    public static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
        v => v.ToList()
    );

    public static readonly ValueConverter<List<EmployeeRole>, string> RoleListConverter = new(
        v => string.Join('|', v.Select(r => r.ToString())),
        v => v == string.Empty ? new List<EmployeeRole>() : v.Split('|', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<EmployeeRole>).ToList()
    );

    public static readonly ValueComparer<List<EmployeeRole>> RoleListComparer = new(
        (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
        v => v.Aggregate(0, (hash, r) => HashCode.Combine(hash, r.GetHashCode())),
        v => v.ToList()
    );
}

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.IdNumber).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.IdNumber).IsUnique();
        b.Property(x => x.AccountRefId).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.AccountRefId).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.Property(x => x.Office).HasMaxLength(200);
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Region).HasMaxLength(100);
        b.Property(x => x.Zone).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.Woreda).HasMaxLength(100);
        b.Property(x => x.KycType).HasMaxLength(100);
        b.Property(x => x.KycContact).HasMaxLength(200);
        b.Property(x => x.RejectionReason).HasMaxLength(500);
        b.Property(x => x.Username).HasMaxLength(50);
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(200);
        b.HasMany(x => x.SystemProducts).WithOne(s => s.Client).HasForeignKey(s => s.ClientId);
        b.HasMany(x => x.Tickets).WithOne(t => t.Client).HasForeignKey(t => t.ClientId);
    }
}

public class SystemProductConfiguration : IEntityTypeConfiguration<SystemProduct>
{
    public void Configure(EntityTypeBuilder<SystemProduct> b)
    {
        b.ToTable("system_products");
        b.HasKey(x => x.Id);
        b.Property(x => x.ReferenceNumber).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.ReferenceNumber).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasColumnType("text");
        b.HasMany(x => x.Agreements).WithOne(a => a.SystemProduct).HasForeignKey(a => a.SystemProductId);
    }
}

public class AgreementTypeConfiguration : IEntityTypeConfiguration<AgreementType>
{
    public void Configure(EntityTypeBuilder<AgreementType> b)
    {
        b.ToTable("agreement_types");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Description).HasMaxLength(500);
        // Restrict — an agreement type in use must be removed from its agreements
        // (or those agreements deleted/retyped) before the type itself can go;
        // never let deleting a lookup value silently orphan or cascade-delete
        // real signed agreements.
        b.HasMany(x => x.Agreements).WithOne(a => a.AgreementType).HasForeignKey(a => a.AgreementTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgreementConfiguration : IEntityTypeConfiguration<Agreement>
{
    public void Configure(EntityTypeBuilder<Agreement> b)
    {
        b.ToTable("agreements");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentNumber).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.DocumentNumber).IsUnique();
        b.Property(x => x.ScannedFileUrl).HasMaxLength(500);
        b.Property(x => x.AgreementPlace).HasMaxLength(200);
        b.Property(x => x.Details).HasColumnType("text");
        // TrainingSession is one-to-one, owned by AgreementId as its own key (see
        // TrainingSessionConfiguration) — never cascade-deleted implicitly here;
        // TrainingSessionConfiguration states the delete behavior explicitly.
        b.HasOne(x => x.TrainingSession).WithOne(t => t.Agreement).HasForeignKey<TrainingSession>(t => t.AgreementId);
    }
}

public class TrainingSessionConfiguration : IEntityTypeConfiguration<TrainingSession>
{
    public void Configure(EntityTypeBuilder<TrainingSession> b)
    {
        b.ToTable("training_sessions");
        b.HasKey(x => x.AgreementId);
        b.Property(x => x.Location).HasMaxLength(300);
        b.Property(x => x.Participants).HasColumnType("text");
        b.Property(x => x.Attendance).HasColumnType("text");
        b.Property(x => x.TopicsCovered).HasColumnType("text");
        b.Property(x => x.IssuesOrQuestions).HasColumnType("text");
        b.Property(x => x.TrainerComments).HasColumnType("text");
        b.Property(x => x.ClientRepresentativeConfirmation).HasMaxLength(300);
        b.Property(x => x.ClientRepresentativeComments).HasColumnType("text");
        b.Property(x => x.FollowUpNotes).HasColumnType("text");
        b.Property(x => x.ScanStorageKey).HasMaxLength(500);
        b.Property(x => x.ScanFileName).HasMaxLength(300);
    }
}

public class TrainingAssignmentConfiguration : IEntityTypeConfiguration<TrainingAssignment>
{
    public void Configure(EntityTypeBuilder<TrainingAssignment> b)
    {
        b.ToTable("training_assignments");
        b.HasKey(x => x.Id);
        b.Property(x => x.WorkDescription).HasColumnType("text");
        b.Property(x => x.FileStorageKey).HasMaxLength(500);
        b.Property(x => x.FileName).HasMaxLength(300);
        b.Property(x => x.ReviewedByName).HasMaxLength(200);
        b.Property(x => x.ReviewNotes).HasColumnType("text");

        // Deleting the owning TrainingSession removes its assignments too —
        // there is no code path that deletes a TrainingSession on its own
        // (it lives and dies with its Agreement), so this only matters for
        // the (currently unused) case of a hard Agreement delete.
        b.HasOne(x => x.TrainingSession).WithMany(t => t.TrainerAssignments)
            .HasForeignKey(x => x.TrainingSessionId).OnDelete(DeleteBehavior.Cascade);

        // Restrict rather than SetNull (unlike the old singular
        // TrainerEmployeeId, which was nullable) because TrainerEmployeeId
        // is required here — a TrainingAssignment without a trainer isn't
        // a meaningful row, it should be removed instead. In practice
        // Employee soft-delete (IsDeleted) never issues a real DELETE, so
        // this Restrict only guards against a hypothetical hard delete.
        b.HasOne(x => x.TrainerEmployee).WithMany()
            .HasForeignKey(x => x.TrainerEmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.TrainingSessionId);
        b.HasIndex(x => x.TrainerEmployeeId);
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> b)
    {
        b.ToTable("tickets");
        b.HasKey(x => x.Id);
        // 0.5-increment range 1.0-5.0 (e.g. 4.5) — see Ticket.SatisfactionStars.
        b.Property(x => x.SatisfactionStars).HasColumnType("numeric(2,1)");
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.Property(x => x.VoiceNoteStorageKey).HasMaxLength(500);
        b.Property(x => x.VoiceNoteFileName).HasMaxLength(300);
        b.HasOne(x => x.Agreement).WithMany(a => a.Tickets).HasForeignKey(x => x.AgreementId);
        b.HasOne(x => x.AssignedEmployee).WithMany(e => e.AssignedTickets).HasForeignKey(x => x.AssignedEmployeeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.ForwardedByEmployee).WithMany().HasForeignKey(x => x.ForwardedByEmployeeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.FailureType).WithMany().HasForeignKey(x => x.FailureTypeId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.AuditTrail).WithOne(a => a.Ticket).HasForeignKey(a => a.TicketId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.Priority);
        b.HasIndex(x => x.ClientConfirmationDeadline);

        // SupportPhase is a computed, expression-bodied property (no
        // setter) — EF Core's conventions wouldn't map it as a column
        // anyway, but Ignore() here makes that explicit rather than
        // relying on the convention silently doing the right thing.
        b.Ignore(x => x.SupportPhase);

        // NOTE: Ticket previously used Postgres's xmin system column as an EF
        // Core concurrency token (b.UseXminAsConcurrencyToken()). That is
        // deliberately removed.
        //
        // A ticket is owned by exactly one assigned technician, and the only
        // writers are (a) that technician's status update, (b) the client's
        // confirmation, (c) the auto-close sweep. None of these need
        // last-write-detection, but the xmin token made every UPDATE a
        // conditional "... AND xmin = @token" write: any row touched between
        // the read and the save (background sweep tick, a second browser tab,
        // a poll-triggered refresh, a connection served a slightly different
        // snapshot) turned into DbUpdateConcurrencyException -> HTTP 409, which
        // surfaced in the technician UI as "This ticket was changed by another
        // user. Please refresh." and made "Resolved" impossible to save.
        //
        // Status transitions are validated server-side against the caller's
        // identity and the ticket's current state, so a plain update-by-id is
        // both safe and reliable here.
    }
}

public class TicketAuditEntryConfiguration : IEntityTypeConfiguration<TicketAuditEntry>
{
    public void Configure(EntityTypeBuilder<TicketAuditEntry> b)
    {
        b.ToTable("ticket_audit_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Actor).HasMaxLength(200).IsRequired();
        b.Property(x => x.Action).HasColumnType("text").IsRequired();
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.ToTable("employees");
        b.HasKey(x => x.Id);
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.AccountRefId).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.AccountRefId).IsUnique();
        b.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Specialization).HasMaxLength(100).IsRequired();
        b.Property(x => x.DisabledReason).HasMaxLength(500);
        b.Property(x => x.Username).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.Username).IsUnique();
        b.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();

        b.Property(x => x.Roles)
            .HasConversion(ValueConverters.RoleListConverter)
            .HasColumnType("varchar(200)")
            .Metadata.SetValueComparer(ValueConverters.RoleListComparer);

        b.Property(x => x.AllowedIpAddresses)
            .HasConversion(ValueConverters.StringListConverter)
            .HasColumnType("varchar(1000)")
            .Metadata.SetValueComparer(ValueConverters.StringListComparer);

        b.Property(x => x.ExtraRoleLabels)
            .HasConversion(ValueConverters.StringListConverter)
            .HasColumnType("varchar(500)")
            .Metadata.SetValueComparer(ValueConverters.StringListComparer);

        b.HasMany(x => x.DeviceSessions).WithOne(d => d.Employee).HasForeignKey(d => d.EmployeeId);
        b.HasMany(x => x.LoginRecords).WithOne(l => l.Employee).HasForeignKey(l => l.EmployeeId);
        b.HasMany(x => x.TimeLogs).WithOne(t => t.Employee).HasForeignKey(t => t.EmployeeId);
    }
}

public class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> b)
    {
        b.ToTable("device_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceIdentifier).HasMaxLength(200).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired(); // 45 = max IPv6 literal length
    }
}

public class LoginRecordConfiguration : IEntityTypeConfiguration<LoginRecord>
{
    public void Configure(EntityTypeBuilder<LoginRecord> b)
    {
        b.ToTable("login_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
        b.Property(x => x.DeviceIdentifier).HasMaxLength(200).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => x.Timestamp);
    }
}

public class TimeLogConfiguration : IEntityTypeConfiguration<TimeLog>
{
    public void Configure(EntityTypeBuilder<TimeLog> b)
    {
        b.ToTable("time_logs");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.EmployeeId, x.Date });
    }
}

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> b)
    {
        b.ToTable("maintenance_records");
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasColumnType("text").IsRequired();
        b.Property(x => x.Remarks).HasColumnType("text");
        b.HasOne(x => x.PerformedByEmployee).WithMany(e => e.MaintenanceRecords).HasForeignKey(x => x.PerformedByEmployeeId);
    }
}

public class AppNotificationConfiguration : IEntityTypeConfiguration<AppNotification>
{
    public void Configure(EntityTypeBuilder<AppNotification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.RecipientId).HasMaxLength(100).IsRequired();
        b.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Message).HasColumnType("text").IsRequired();
        b.HasIndex(x => new { x.RecipientType, x.RecipientId, x.ReadStatus });
    }
}

public class SatisfactionSurveyConfiguration : IEntityTypeConfiguration<SatisfactionSurvey>
{
    public void Configure(EntityTypeBuilder<SatisfactionSurvey> b)
    {
        b.ToTable("satisfaction_surveys");
        b.HasKey(x => x.Id);
        b.Property(x => x.ImprovementFeedback).HasColumnType("text");
        b.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        // One survey per ticket — the portal only offers the form once per resolved ticket.
        b.HasIndex(x => x.TicketId).IsUnique();
    }
}

public class LoginSessionConfiguration : IEntityTypeConfiguration<LoginSession>
{
    public void Configure(EntityTypeBuilder<LoginSession> b)
    {
        b.ToTable("login_sessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(45).IsRequired();
        b.HasIndex(x => new { x.AccountType, x.AccountId, x.OnlineStatus });
        b.HasIndex(x => x.LastSeen);
    }
}

/// <summary>Refresh tokens are looked up by hash on every refresh call, and swept for expiry — both need an index.</summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(45).IsRequired();
        b.Property(x => x.RevokedByIp).HasMaxLength(45);
        b.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.AccountType, x.AccountId });
        b.HasIndex(x => x.ExpiresAt);
    }
}

/// <summary>Admin-editable configuration overrides — see SystemSetting for the key/value model.</summary>
public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> b)
    {
        b.ToTable("system_settings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(150).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100).IsRequired();
        b.Property(x => x.UpdatedByName).HasMaxLength(200);
        b.HasIndex(x => x.Key).IsUnique();
    }
}

/// <summary>Admin's "Password Reset Requests" queue — see PasswordResetRequest.</summary>
public class PasswordResetRequestConfiguration : IEntityTypeConfiguration<PasswordResetRequest>
{
    public void Configure(EntityTypeBuilder<PasswordResetRequest> b)
    {
        b.ToTable("password_reset_requests");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(50).IsRequired();
        b.Property(x => x.Note).HasMaxLength(500);
        b.Property(x => x.RequestIpAddress).HasMaxLength(45).IsRequired();
        b.Property(x => x.ResolvedByName).HasMaxLength(200);
        b.Property(x => x.DismissReason).HasMaxLength(500);
        b.HasIndex(x => new { x.AccountType, x.AccountId, x.Status });
        b.HasIndex(x => x.Status);
    }
}
/// <summary>Admin-managed options for the Region / City / Woreda dropdowns on client forms.</summary>
public class LocationEntryConfiguration : IEntityTypeConfiguration<LocationEntry>
{
    public void Configure(EntityTypeBuilder<LocationEntry> b)
    {
        b.ToTable("location_entries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => new { x.Type, x.Name }).IsUnique();
    }
}

/// <summary>Admin-managed failure types with expected resolution durations, chosen by clients on ticket submission.</summary>
public class FailureTypeConfiguration : IEntityTypeConfiguration<FailureType>
{
    public void Configure(EntityTypeBuilder<FailureType> b)
    {
        b.ToTable("failure_types");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.DurationUnit).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}

/// <summary>Binary content backing PostgresFileStorageService — ticket attachments and voice notes stored in-database rather than a third-party host.</summary>
public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> b)
    {
        b.ToTable("stored_files");
        b.HasKey(x => x.Id);
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        b.Property(x => x.Content).IsRequired();
    }
}
