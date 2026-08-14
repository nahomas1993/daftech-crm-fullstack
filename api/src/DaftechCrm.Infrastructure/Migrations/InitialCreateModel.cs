
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    internal static class InitialCreateModel
    {
        public static void Build(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("AgreementPlace").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<int>("BillingTier").HasColumnType("integer");
                b.Property<Guid>("ClientId").HasColumnType("uuid");
                b.Property<string>("DocumentNumber").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateOnly>("ExpiryDate").HasColumnType("date");
                b.Property<string>("ScannedFileUrl").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<DateOnly>("SignDate").HasColumnType("date");
                b.Property<int>("Status").HasColumnType("integer");
                b.Property<int>("SupportWindowMonths").HasColumnType("integer");
                b.HasKey("Id");
                b.HasIndex("ClientId");
                b.HasIndex("DocumentNumber").IsUnique();
                b.ToTable("agreements");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.AppNotification", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<DateTimeOffset>("DateSent").HasColumnType("timestamp with time zone");
                b.Property<string>("EventType").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Message").IsRequired().HasColumnType("text");
                b.Property<bool>("ReadStatus").HasColumnType("boolean");
                b.Property<string>("RecipientId").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<int>("RecipientType").HasColumnType("integer");
                b.HasKey("Id");
                b.HasIndex("RecipientType", "RecipientId", "ReadStatus");
                b.ToTable("notifications");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Client", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<int>("AccountStatus").HasColumnType("integer");
                b.Property<string>("Email").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("IdNumber").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("ItSupportContact").HasColumnType("text");
                b.Property<string>("KycContact").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("KycType").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Location").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<bool>("MustChangePassword").HasColumnType("boolean");
                b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Office").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<DateOnly>("OnboardingDate").HasColumnType("date");
                b.Property<string>("PasswordHash").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("RejectionReason").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("Username").HasMaxLength(50).HasColumnType("character varying(50)");
                b.HasKey("Id");
                b.HasIndex("IdNumber").IsUnique();
                b.HasIndex("Username").IsUnique();
                b.ToTable("clients");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Employee", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<int>("AccountStatus").HasColumnType("integer");
                b.Property<string>("AllowedIpAddresses").IsRequired().HasColumnType("varchar(1000)");
                b.Property<DateTimeOffset?>("DisabledAt").HasColumnType("timestamp with time zone");
                b.Property<string>("DisabledReason").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("Email").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("FullName").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<bool>("MustChangePassword").HasColumnType("boolean");
                b.Property<string>("PasswordHash").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("Roles").IsRequired().HasColumnType("varchar(200)");
                b.Property<string>("Specialization").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Username").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.HasKey("Id");
                b.HasIndex("Email").IsUnique();
                b.HasIndex("Username").IsUnique();
                b.ToTable("employees");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.DeviceSession", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<int>("AccessStatus").HasColumnType("integer");
                b.Property<string>("DeviceIdentifier").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<int>("DeviceType").HasColumnType("integer");
                b.Property<Guid>("EmployeeId").HasColumnType("uuid");
                b.Property<string>("IpAddress").IsRequired().HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<DateTimeOffset>("LastSeen").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("EmployeeId");
                b.ToTable("device_sessions");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.LoginRecord", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<bool>("Allowed").HasColumnType("boolean");
                b.Property<string>("DeviceIdentifier").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<int>("DeviceType").HasColumnType("integer");
                b.Property<Guid>("EmployeeId").HasColumnType("uuid");
                b.Property<string>("IpAddress").IsRequired().HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<string>("Reason").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<DateTimeOffset>("Timestamp").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("EmployeeId");
                b.HasIndex("Timestamp");
                b.ToTable("login_records");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.LoginSession", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("AccountId").HasColumnType("uuid");
                b.Property<int>("AccountType").HasColumnType("integer");
                b.Property<string>("IpAddress").IsRequired().HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<DateTimeOffset>("LastSeen").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset>("LoginTime").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("LogoutTime").HasColumnType("timestamp with time zone");
                b.Property<bool>("OnlineStatus").HasColumnType("boolean");
                b.HasKey("Id");
                b.HasIndex("AccountType", "AccountId", "OnlineStatus");
                b.HasIndex("LastSeen");
                b.ToTable("login_sessions");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.MaintenanceRecord", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Category").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateOnly>("Date").HasColumnType("date");
                b.Property<string>("Description").IsRequired().HasColumnType("text");
                b.Property<Guid>("PerformedByEmployeeId").HasColumnType("uuid");
                b.Property<string>("Remarks").HasColumnType("text");
                b.Property<int>("Status").HasColumnType("integer");
                b.HasKey("Id");
                b.HasIndex("PerformedByEmployeeId");
                b.ToTable("maintenance_records");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.RefreshToken", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("AccountId").HasColumnType("uuid");
                b.Property<int>("AccountType").HasColumnType("integer");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("CreatedByIp").IsRequired().HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<DateTimeOffset>("ExpiresAt").HasColumnType("timestamp with time zone");
                b.Property<string>("ReplacedByTokenHash").HasMaxLength(64).HasColumnType("character varying(64)");
                b.Property<DateTimeOffset?>("RevokedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("RevokedByIp").HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<string>("TokenHash").IsRequired().HasMaxLength(64).HasColumnType("character varying(64)");
                b.HasKey("Id");
                b.HasIndex("AccountType", "AccountId");
                b.HasIndex("ExpiresAt");
                b.HasIndex("TokenHash").IsUnique();
                b.ToTable("refresh_tokens");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SatisfactionSurvey", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("ClientId").HasColumnType("uuid");
                b.Property<int>("CommunicationClarityRating").HasColumnType("integer");
                b.Property<string>("ImprovementFeedback").HasColumnType("text");
                b.Property<int>("LikelihoodToRecommend").HasColumnType("integer");
                b.Property<int>("ProfessionalismRating").HasColumnType("integer");
                b.Property<int>("ResponseSpeedRating").HasColumnType("integer");
                b.Property<DateTimeOffset>("SubmittedAt").HasColumnType("timestamp with time zone");
                b.Property<Guid>("TicketId").HasColumnType("uuid");
                b.HasKey("Id");
                b.HasIndex("ClientId");
                b.HasIndex("TicketId").IsUnique();
                b.ToTable("satisfaction_surveys");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Ticket", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("AgreementId").HasColumnType("uuid");
                b.Property<Guid?>("AssignedEmployeeId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("AssignedAt").HasColumnType("timestamp with time zone");
                b.Property<int>("Category").HasColumnType("integer");
                b.Property<bool>("Chargeable").HasColumnType("boolean");
                b.Property<Guid>("ClientId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("ClientConfirmationDeadline").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("ClosedAt").HasColumnType("timestamp with time zone");
                b.Property<int?>("ClosureReason").HasColumnType("integer");
                b.Property<DateTimeOffset>("DateSubmitted").HasColumnType("timestamp with time zone");
                b.Property<string>("Description").IsRequired().HasColumnType("text");
                b.Property<Guid?>("ForwardedByEmployeeId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("ResolvedAt").HasColumnType("timestamp with time zone");
                b.Property<int?>("SatisfactionScore").HasColumnType("integer");
                b.Property<int?>("SatisfactionStars").HasColumnType("integer");
                b.Property<int>("Status").HasColumnType("integer");
                b.HasKey("Id");
                b.HasIndex("AgreementId");
                b.HasIndex("AssignedEmployeeId");
                b.HasIndex("ClientConfirmationDeadline");
                b.HasIndex("ClientId");
                b.HasIndex("ForwardedByEmployeeId");
                b.HasIndex("Status");
                b.ToTable("tickets");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TicketAuditEntry", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Action").IsRequired().HasColumnType("text");
                b.Property<string>("Actor").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<Guid>("TicketId").HasColumnType("uuid");
                b.Property<DateTimeOffset>("Timestamp").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("TicketId");
                b.ToTable("ticket_audit_entries");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TimeLog", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<DateOnly>("Date").HasColumnType("date");
                b.Property<Guid>("EmployeeId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("FinishTime").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("StartTime").HasColumnType("timestamp with time zone");
                b.Property<double?>("TotalHours").HasColumnType("double precision");
                b.HasKey("Id");
                b.HasIndex("EmployeeId", "Date");
                b.ToTable("time_logs");
            });

            // --- Relationships ---

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany("Agreements")
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Client");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.DeviceSession", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("DeviceSessions")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.LoginRecord", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("LoginRecords")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.MaintenanceRecord", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "PerformedByEmployee")
                    .WithMany("MaintenanceRecords")
                    .HasForeignKey("PerformedByEmployeeId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("PerformedByEmployee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SatisfactionSurvey", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany()
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.Ticket", "Ticket")
                    .WithMany()
                    .HasForeignKey("TicketId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Client");
                b.Navigation("Ticket");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Ticket", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Agreement", "Agreement")
                    .WithMany("Tickets")
                    .HasForeignKey("AgreementId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "AssignedEmployee")
                    .WithMany("AssignedTickets")
                    .HasForeignKey("AssignedEmployeeId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany("Tickets")
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "ForwardedByEmployee")
                    .WithMany()
                    .HasForeignKey("ForwardedByEmployeeId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.Navigation("Agreement");
                b.Navigation("AssignedEmployee");
                b.Navigation("Client");
                b.Navigation("ForwardedByEmployee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TicketAuditEntry", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Ticket", "Ticket")
                    .WithMany("AuditTrail")
                    .HasForeignKey("TicketId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Ticket");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TimeLog", b =>
            {
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("TimeLogs")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                b.Navigation("Tickets");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Client", b =>
            {
                b.Navigation("Agreements");
                b.Navigation("Tickets");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Employee", b =>
            {
                b.Navigation("AssignedTickets");
                b.Navigation("DeviceSessions");
                b.Navigation("LoginRecords");
                b.Navigation("MaintenanceRecords");
                b.Navigation("TimeLogs");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Ticket", b =>
            {
                b.Navigation("AuditTrail");
            });
        }
    }
}
