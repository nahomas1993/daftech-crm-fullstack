
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    internal static class CurrentModel
    {
        public static void Build(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("AgreementPlace").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<Guid>("AgreementTypeId").HasColumnType("uuid");
                b.Property<int>("BillingTier").HasColumnType("integer");
                b.Property<string>("Details").HasColumnType("text");
                b.Property<string>("DocumentNumber").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateOnly>("ExpiryDate").HasColumnType("date");
                b.Property<string>("ScannedFileUrl").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<DateOnly>("SignDate").HasColumnType("date");
                b.Property<int>("Status").HasColumnType("integer");
                b.Property<int>("SupportWindowMonths").HasColumnType("integer");
                b.Property<Guid>("SystemProductId").HasColumnType("uuid");
                b.HasKey("Id");
                b.HasIndex("AgreementTypeId");
                b.HasIndex("DocumentNumber").IsUnique();
                b.HasIndex("SystemProductId");
                b.ToTable("agreements");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.AgreementType", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<bool>("IsSystemDefined").HasColumnType("boolean");
                b.Property<string>("Name").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.HasKey("Id");
                b.HasIndex("Name").IsUnique();
                b.ToTable("agreement_types");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SystemProduct", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("ClientId").HasColumnType("uuid");
                b.Property<Guid?>("CatalogItemId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("DeletedAt").HasColumnType("timestamp with time zone");
                b.Property<DateOnly?>("DeploymentDate").HasColumnType("date");
                b.Property<DateOnly?>("ExpiryDate").HasColumnType("date");
                b.Property<string>("Description").HasColumnType("text");
                b.Property<bool>("IsDeleted").HasColumnType("boolean");
                b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("ReferenceNumber").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<int>("TrainingCompletionStatus").HasColumnType("integer");
                b.Property<DateTimeOffset?>("TrainingSubmittedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("ClientId");
                b.HasIndex("CatalogItemId");
                b.HasIndex("ReferenceNumber").IsUnique();
                b.ToTable("system_products");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.ProductCatalogItem", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<bool>("IsActive").HasColumnType("boolean");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("Name").IsUnique();
                b.ToTable("product_catalog_items");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TrainingAssignment", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<DateTimeOffset>("AssignedAt").HasColumnType("timestamp with time zone");
                b.Property<Guid>("SystemProductId").HasColumnType("uuid");
                b.Property<Guid>("TrainerEmployeeId").HasColumnType("uuid");
                b.HasKey("Id");
                b.HasIndex("SystemProductId");
                b.HasIndex("TrainerEmployeeId");
                b.HasIndex("SystemProductId", "TrainerEmployeeId").IsUnique();
                b.ToTable("training_assignments");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TrainingRecord", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("AgreementTypeId").HasColumnType("uuid");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("Description").IsRequired().HasColumnType("text");
                b.Property<DateTimeOffset?>("EndDateTime").HasColumnType("timestamp with time zone");
                b.Property<string>("FileName").HasMaxLength(300).HasColumnType("character varying(300)");
                b.Property<string>("FileStorageKey").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<DateTimeOffset?>("StartDateTime").HasColumnType("timestamp with time zone");
                b.Property<Guid>("SystemProductId").HasColumnType("uuid");
                b.Property<Guid>("TrainerEmployeeId").HasColumnType("uuid");
                b.Property<DateOnly>("TrainingDate").HasColumnType("date");
                b.HasKey("Id");
                b.HasIndex("AgreementTypeId");
                b.HasIndex("SystemProductId");
                b.HasIndex("TrainerEmployeeId");
                b.ToTable("training_records");
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
                b.Property<string>("AccountRefId").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<int>("AccountStatus").HasColumnType("integer");
                b.Property<string>("City").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateTimeOffset?>("DeletedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("Email").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("IdNumber").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<bool>("IsDeleted").HasColumnType("boolean");
                b.Property<string>("ItSupportContact").HasColumnType("text");
                b.Property<string>("KycContact").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("KycType").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Location").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<bool>("MustChangePassword").HasColumnType("boolean");
                b.Property<string>("Name").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Office").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<DateOnly>("OnboardingDate").HasColumnType("date");
                b.Property<DateTimeOffset?>("OtpExpiresAt").HasColumnType("timestamp with time zone");
                b.Property<string>("PasswordHash").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("RejectionReason").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("Region").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Username").HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<string>("Woreda").HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Zone").HasMaxLength(100).HasColumnType("character varying(100)");
                b.HasKey("Id");
                b.HasIndex("AccountRefId").IsUnique();
                b.HasIndex("IdNumber").IsUnique();
                b.HasIndex("Username").IsUnique();
                b.ToTable("clients");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Employee", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("AccountRefId").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<int>("AccountStatus").HasColumnType("integer");
                b.Property<string>("AllowedIpAddresses").IsRequired().HasColumnType("varchar(1000)");
                b.Property<DateTimeOffset?>("DeletedAt").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("DisabledAt").HasColumnType("timestamp with time zone");
                b.Property<string>("DisabledReason").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("Email").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("ExtraRoleLabels").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("FullName").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<bool>("IsDeleted").HasColumnType("boolean");
                b.Property<bool>("MustChangePassword").HasColumnType("boolean");
                b.Property<string>("PasswordHash").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("PhoneNumber").IsRequired().HasMaxLength(30).HasColumnType("character varying(30)");
                b.Property<string>("Roles").IsRequired().HasColumnType("varchar(200)");
                b.Property<string>("Specialization").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<DateTimeOffset?>("OtpExpiresAt").HasColumnType("timestamp with time zone");
                b.Property<string>("Username").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.HasKey("Id");
                b.HasIndex("AccountRefId").IsUnique();
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
                b.Property<string>("SatisfactionComment").HasColumnType("text");
                b.Property<DateTimeOffset>("SubmittedAt").HasColumnType("timestamp with time zone");
                b.Property<Guid>("TicketId").HasColumnType("uuid");
                b.HasKey("Id");
                b.HasIndex("ClientId");
                b.HasIndex("TicketId").IsUnique();
                b.ToTable("satisfaction_surveys");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SurveyAnswer", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("SatisfactionSurveyId").HasColumnType("uuid");
                b.Property<Guid?>("SurveyQuestionId").HasColumnType("uuid");
                b.Property<string>("QuestionText").IsRequired().HasColumnType("text");
                b.Property<int>("DisplayOrder").HasColumnType("integer");
                b.Property<int>("Rating").HasColumnType("integer");
                b.HasKey("Id");
                b.HasIndex("SatisfactionSurveyId");
                b.HasIndex("SurveyQuestionId");
                b.ToTable("survey_answers");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SurveyQuestion", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Text").IsRequired().HasColumnType("text");
                b.Property<int>("DisplayOrder").HasColumnType("integer");
                b.Property<bool>("IsActive").HasColumnType("boolean");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.HasIndex("DisplayOrder");
                b.ToTable("survey_questions");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Ticket", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<Guid>("AgreementId").HasColumnType("uuid");
                b.Property<Guid?>("SystemProductId").HasColumnType("uuid");
                b.Property<Guid?>("AssignedEmployeeId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("AssignedAt").HasColumnType("timestamp with time zone");
                b.Property<int?>("ExpectedResolutionMinutes").HasColumnType("integer");
                b.Property<DateTimeOffset?>("ExpectedResolutionBy").HasColumnType("timestamp with time zone");
                b.Property<string>("AttachmentStorageKey").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("AttachmentFileName").HasMaxLength(260).HasColumnType("character varying(260)");
                b.Property<int>("Category").HasColumnType("integer");
                b.Property<bool>("Chargeable").HasColumnType("boolean");
                b.Property<Guid>("ClientId").HasColumnType("uuid");
                b.Property<DateTimeOffset?>("ClientConfirmationDeadline").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("ClosedAt").HasColumnType("timestamp with time zone");
                b.Property<int?>("ClosureReason").HasColumnType("integer");
                b.Property<DateTimeOffset>("DateSubmitted").HasColumnType("timestamp with time zone");
                b.Property<string>("Description").IsRequired().HasColumnType("text");
                b.Property<Guid?>("ForwardedByEmployeeId").HasColumnType("uuid");
                b.Property<Guid?>("FailureTypeId").HasColumnType("uuid");
                b.Property<Guid?>("SupportTypeId").HasColumnType("uuid");
                b.Property<decimal?>("ChargeAmount").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
                b.Property<bool>("ChargeAcknowledged").HasColumnType("boolean");
                b.Property<DateTimeOffset?>("ResolvedAt").HasColumnType("timestamp with time zone");
                b.Property<int?>("SatisfactionScore").HasColumnType("integer");
                b.Property<decimal?>("SatisfactionStars").HasColumnType("numeric(2,1)");
                b.Property<int>("Status").HasColumnType("integer");
                b.Property<int>("Priority").HasColumnType("integer");
                b.Property<string>("VoiceNoteStorageKey").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("VoiceNoteFileName").HasMaxLength(300).HasColumnType("character varying(300)");
                b.HasKey("Id");
                b.HasIndex("AgreementId");
                b.HasIndex("SystemProductId");
                b.HasIndex("AssignedEmployeeId");
                b.HasIndex("ClientConfirmationDeadline");
                b.HasIndex("ClientId");
                b.HasIndex("ForwardedByEmployeeId");
                b.HasIndex("FailureTypeId");
                b.HasIndex("SupportTypeId");
                b.HasIndex("Status");
                b.HasIndex("Priority");
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

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SystemSetting", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Category").IsRequired().HasMaxLength(100).HasColumnType("character varying(100)");
                b.Property<string>("Key").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
                b.Property<DateTimeOffset>("UpdatedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("UpdatedByName").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("Value").IsRequired().HasMaxLength(500).HasColumnType("character varying(500)");
                b.HasKey("Id");
                b.HasIndex("Key").IsUnique();
                b.ToTable("system_settings");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.PasswordResetRequest", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<int>("AccountType").HasColumnType("integer");
                b.Property<Guid>("AccountId").HasColumnType("uuid");
                b.Property<string>("Username").IsRequired().HasMaxLength(50).HasColumnType("character varying(50)");
                b.Property<string>("Note").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<string>("RequestIpAddress").IsRequired().HasMaxLength(45).HasColumnType("character varying(45)");
                b.Property<int>("Status").HasColumnType("integer");
                b.Property<DateTimeOffset>("RequestedAt").HasColumnType("timestamp with time zone");
                b.Property<DateTimeOffset?>("ResolvedAt").HasColumnType("timestamp with time zone");
                b.Property<string>("ResolvedByName").HasMaxLength(200).HasColumnType("character varying(200)");
                b.Property<string>("DismissReason").HasMaxLength(500).HasColumnType("character varying(500)");
                b.HasKey("Id");
                b.HasIndex("AccountType", "AccountId", "Status");
                b.HasIndex("Status");
                b.ToTable("password_reset_requests");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.LocationEntry", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Type").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
                b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
                b.HasKey("Id");
                b.HasIndex("Type", "Name").IsUnique();
                b.ToTable("location_entries");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.FailureType", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<int>("Category").HasColumnType("integer");
                b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<decimal>("BasePrice").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
                b.Property<int>("DurationValue").HasColumnType("integer");
                b.Property<string>("DurationUnit").IsRequired().HasMaxLength(20).HasColumnType("character varying(20)");
                b.HasKey("Id");
                b.HasIndex("Name").IsUnique();
                b.ToTable("failure_types");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SupportType", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("Name").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
                b.Property<string>("Description").HasMaxLength(500).HasColumnType("character varying(500)");
                b.Property<decimal>("AdditionalFee").HasColumnType("numeric(12,2)").HasPrecision(12, 2);
                b.HasKey("Id");
                b.HasIndex("Name").IsUnique();
                b.ToTable("support_types");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.StoredFile", b =>
            {
                b.Property<Guid>("Id").HasColumnType("uuid");
                b.Property<string>("OriginalFileName").IsRequired().HasMaxLength(255).HasColumnType("character varying(255)");
                b.Property<string>("ContentType").IsRequired().HasMaxLength(150).HasColumnType("character varying(150)");
                b.Property<long>("SizeBytes").HasColumnType("bigint");
                b.Property<byte[]>("Content").IsRequired().HasColumnType("bytea");
                b.Property<DateTimeOffset>("CreatedAt").HasColumnType("timestamp with time zone");
                b.HasKey("Id");
                b.ToTable("stored_files");
            });

            // --- Relationships ---

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SystemProduct", b =>
            {
                // Same reasoning as the old Agreement->Client relationship —
                // Client is soft-delete only in practice.
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany("SystemProducts")
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                // Catalog entry a client's product was created from, if any
                // — a deleted catalog entry must not take down the
                // client's already-provisioned product record.
                b.HasOne("DaftechCrm.Domain.Entities.ProductCatalogItem", "CatalogItem")
                    .WithMany()
                    .HasForeignKey("CatalogItemId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.Navigation("Client");
                b.Navigation("CatalogItem");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                // SystemProduct is soft-delete only in practice
                // (SystemProductService.DeleteAsync sets IsDeleted/DeletedAt,
                // never a real DELETE) — Restrict, not Cascade, for the same
                // reason as the old Agreement->Client relationship: a
                // hard-delete of a system/product must not silently destroy
                // its agreement history.
                b.HasOne("DaftechCrm.Domain.Entities.SystemProduct", "SystemProduct")
                    .WithMany("Agreements")
                    .HasForeignKey("SystemProductId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                // AgreementTypeConfiguration explicitly Restricts this too —
                // an agreement type in use must be retyped/removed from its
                // agreements before the type itself can be deleted.
                b.HasOne("DaftechCrm.Domain.Entities.AgreementType", "AgreementType")
                    .WithMany("Agreements")
                    .HasForeignKey("AgreementTypeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("SystemProduct");
                b.Navigation("AgreementType");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TrainingAssignment", b =>
            {
                // Deleting the owning SystemProduct removes its training
                // roster too — see TrainingAssignmentConfiguration.
                b.HasOne("DaftechCrm.Domain.Entities.SystemProduct", "SystemProduct")
                    .WithMany("TrainingAssignments")
                    .HasForeignKey("SystemProductId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                // A trainer being deleted/soft-deleted must not delete
                // roster history — Restrict, since Employee soft-delete
                // never issues a real DELETE in practice.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "TrainerEmployee")
                    .WithMany()
                    .HasForeignKey("TrainerEmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("SystemProduct");
                b.Navigation("TrainerEmployee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TrainingRecord", b =>
            {
                // Deleting the owning SystemProduct removes its training
                // log too — same reasoning as TrainingAssignment above.
                b.HasOne("DaftechCrm.Domain.Entities.SystemProduct", "SystemProduct")
                    .WithMany("TrainingRecords")
                    .HasForeignKey("SystemProductId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                // A training record is a historical fact about who
                // conducted it — Restrict, not Cascade/SetNull, so
                // deleting that employee can't delete the record.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "TrainerEmployee")
                    .WithMany()
                    .HasForeignKey("TrainerEmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                // The admin-configured item name (e.g. "Attendance") this
                // record is logged against — Restrict so removing the
                // lookup value can't silently delete training history.
                b.HasOne("DaftechCrm.Domain.Entities.AgreementType", "AgreementType")
                    .WithMany()
                    .HasForeignKey("AgreementTypeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("SystemProduct");
                b.Navigation("TrainerEmployee");
                b.Navigation("AgreementType");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.DeviceSession", b =>
            {
                // Employee is only ever soft-deleted (EmployeeService.DeleteAsync
                // sets IsDeleted/AccountStatus=Disabled, never a real DELETE) —
                // Restrict for the same reason as the Client relationships above.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("DeviceSessions")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.LoginRecord", b =>
            {
                // Same reasoning — Employee is soft-delete only in practice.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("LoginRecords")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.MaintenanceRecord", b =>
            {
                // Same reasoning — Employee is soft-delete only in practice.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "PerformedByEmployee")
                    .WithMany("MaintenanceRecords")
                    .HasForeignKey("PerformedByEmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("PerformedByEmployee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SatisfactionSurvey", b =>
            {
                // Client is soft-delete only in practice (see above);
                // Ticket, on the other hand, is never soft-deleted at all —
                // if a Ticket row is ever genuinely removed, its survey
                // response is meaningless without it, so that edge stays a
                // real Cascade.
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany()
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.Ticket", "Ticket")
                    .WithMany()
                    .HasForeignKey("TicketId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Client");
                b.Navigation("Ticket");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SurveyAnswer", b =>
            {
                // SetNull, not Cascade — deleting an admin-authored
                // SurveyQuestion later must not delete historical answers;
                // QuestionText already snapshots what was asked.
                b.HasOne("DaftechCrm.Domain.Entities.SatisfactionSurvey", "SatisfactionSurvey")
                    .WithMany("Answers")
                    .HasForeignKey("SatisfactionSurveyId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.SurveyQuestion", "SurveyQuestion")
                    .WithMany()
                    .HasForeignKey("SurveyQuestionId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.Navigation("SatisfactionSurvey");
                b.Navigation("SurveyQuestion");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Ticket", b =>
            {
                // Agreement is never hard-deleted by any application code path
                // (AgreementService only ever removes stored file blobs, never
                // the Agreement row itself) — Restrict for the same reason as
                // the Client/Employee relationships above.
                b.HasOne("DaftechCrm.Domain.Entities.Agreement", "Agreement")
                    .WithMany("Tickets")
                    .HasForeignKey("AgreementId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                // SystemProduct is never hard-deleted by any application
                // code path (soft-delete only) — SetNull is purely a
                // belt-and-braces default, not expected to fire in
                // practice.
                b.HasOne("DaftechCrm.Domain.Entities.SystemProduct", "SystemProduct")
                    .WithMany()
                    .HasForeignKey("SystemProductId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "AssignedEmployee")
                    .WithMany("AssignedTickets")
                    .HasForeignKey("AssignedEmployeeId")
                    .OnDelete(DeleteBehavior.SetNull);
                // Client is soft-delete only in practice (see Agreement->Client
                // above) — Restrict, not Cascade.
                b.HasOne("DaftechCrm.Domain.Entities.Client", "Client")
                    .WithMany("Tickets")
                    .HasForeignKey("ClientId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "ForwardedByEmployee")
                    .WithMany()
                    .HasForeignKey("ForwardedByEmployeeId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.HasOne("DaftechCrm.Domain.Entities.FailureType", "FailureType")
                    .WithMany()
                    .HasForeignKey("FailureTypeId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.HasOne("DaftechCrm.Domain.Entities.SupportType", "SupportType")
                    .WithMany()
                    .HasForeignKey("SupportTypeId")
                    .OnDelete(DeleteBehavior.SetNull);
                b.Navigation("Agreement");
                b.Navigation("SystemProduct");
                b.Navigation("AssignedEmployee");
                b.Navigation("Client");
                b.Navigation("ForwardedByEmployee");
                b.Navigation("FailureType");
                b.Navigation("SupportType");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TicketAuditEntry", b =>
            {
                // Ticket itself is never soft- or hard-deleted by any
                // application code path today, but if it ever were, an audit
                // entry with no ticket to describe is meaningless — this one
                // stays a real Cascade, unlike the Client/Employee/Agreement
                // relationships above.
                b.HasOne("DaftechCrm.Domain.Entities.Ticket", "Ticket")
                    .WithMany("AuditTrail")
                    .HasForeignKey("TicketId")
                    .OnDelete(DeleteBehavior.Cascade)
                    .IsRequired();
                b.Navigation("Ticket");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.TimeLog", b =>
            {
                // Same reasoning as DeviceSession/LoginRecord/MaintenanceRecord
                // above — Employee is soft-delete only in practice.
                b.HasOne("DaftechCrm.Domain.Entities.Employee", "Employee")
                    .WithMany("TimeLogs")
                    .HasForeignKey("EmployeeId")
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
                b.Navigation("Employee");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Agreement", b =>
            {
                b.Navigation("Tickets");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.AgreementType", b =>
            {
                b.Navigation("Agreements");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SystemProduct", b =>
            {
                b.Navigation("Agreements");
                b.Navigation("TrainingAssignments");
                b.Navigation("TrainingRecords");
            });

            modelBuilder.Entity("DaftechCrm.Domain.Entities.Client", b =>
            {
                b.Navigation("SystemProducts");
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

            modelBuilder.Entity("DaftechCrm.Domain.Entities.SatisfactionSurvey", b =>
            {
                b.Navigation("Answers");
            });
        }
    }
}