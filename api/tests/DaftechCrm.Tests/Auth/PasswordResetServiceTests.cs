using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Services;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DaftechCrm.Tests.Auth;

/// <summary>
/// Covers PasswordResetService's core invariants: anonymous submission
/// never reveals whether a username exists (no enumeration), issuing an
/// OTP actually rotates the password hash and forces MustChangePassword,
/// and a request can only be actioned once.
/// </summary>
public class PasswordResetServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly FakeEmailSender _email = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly FakeSystemConfigurationService _config = new();
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var credentials = new AccountCredentialService(_db, _email);
        _sut = new PasswordResetService(_db, credentials, _notifications, _config);
    }

    private async Task<Employee> SeedEmployeeAsync()
    {
        var employee = new Employee
        {
            FullName = "Jordan Doe",
            Email = "jordan@daftech.et",
            PhoneNumber = "0911000000",
            Specialization = "Back-end",
            Username = "jd1234",
            PasswordHash = PasswordHasher.Hash("OriginalPass1"),
            MustChangePassword = false,
            Roles = new List<EmployeeRole> { EmployeeRole.ItSupport },
            AccountRefId = "DAF-EMP-9001",
        };
        _db.Add(employee);
        await _db.SaveChangesAsync();
        return employee;
    }

    [Fact]
    public async Task SubmitAsync_for_an_unknown_username_still_returns_the_generic_message_and_creates_no_request()
    {
        var result = await _sut.SubmitAsync(
            new SubmitPasswordResetRequest(SessionAccountType.Employee, "no-such-user", null), "127.0.0.1");

        result.Message.Should().NotBeNullOrWhiteSpace();
        (await _db.PasswordResetRequests.CountAsync()).Should().Be(0, "an unknown username must not be distinguishable from a known one");
    }

    [Fact]
    public async Task SubmitAsync_for_a_known_username_creates_a_pending_request_and_notifies_admins()
    {
        var employee = await SeedEmployeeAsync();

        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, "locked out"), "10.0.0.1");

        var stored = await _db.PasswordResetRequests.SingleAsync();
        stored.AccountId.Should().Be(employee.Id);
        stored.Status.Should().Be(PasswordResetRequestStatus.Pending);
        stored.Note.Should().Be("locked out");
        _notifications.Notifications.Should().ContainSingle(n => n.EventType == "password_reset_requested");
    }

    [Fact]
    public async Task SubmitAsync_does_not_create_a_second_pending_request_for_the_same_account()
    {
        var employee = await SeedEmployeeAsync();

        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, null), "10.0.0.1");
        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, null), "10.0.0.1");

        (await _db.PasswordResetRequests.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IssueOtpAsync_rotates_the_password_hash_and_forces_a_change_on_next_login()
    {
        var employee = await SeedEmployeeAsync();
        var oldHash = employee.PasswordHash;
        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, null), "10.0.0.1");
        var request = await _db.PasswordResetRequests.SingleAsync();

        var result = await _sut.IssueOtpAsync(request.Id, "Admin Alice");

        result.OneTimePassword.Should().NotBeNullOrWhiteSpace();
        result.EmailSent.Should().BeTrue();

        var updatedEmployee = await _db.Employees.SingleAsync(e => e.Id == employee.Id);
        updatedEmployee.PasswordHash.Should().NotBe(oldHash);
        updatedEmployee.MustChangePassword.Should().BeTrue();
        PasswordHasher.Verify(result.OneTimePassword, updatedEmployee.PasswordHash).Should().BeTrue();

        var updatedRequest = await _db.PasswordResetRequests.SingleAsync();
        updatedRequest.Status.Should().Be(PasswordResetRequestStatus.OtpIssued);
        updatedRequest.ResolvedByName.Should().Be("Admin Alice");
    }

    [Fact]
    public async Task IssueOtpAsync_throws_if_the_request_was_already_actioned()
    {
        var employee = await SeedEmployeeAsync();
        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, null), "10.0.0.1");
        var request = await _db.PasswordResetRequests.SingleAsync();

        await _sut.IssueOtpAsync(request.Id, "Admin Alice");

        var act = async () => await _sut.IssueOtpAsync(request.Id, "Admin Bob");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DismissAsync_marks_the_request_dismissed_without_touching_the_password()
    {
        var employee = await SeedEmployeeAsync();
        var oldHash = employee.PasswordHash;
        await _sut.SubmitAsync(new SubmitPasswordResetRequest(SessionAccountType.Employee, employee.Username, null), "10.0.0.1");
        var request = await _db.PasswordResetRequests.SingleAsync();

        var dto = await _sut.DismissAsync(request.Id, "Admin Alice", new DismissPasswordResetRequest("Verified with the employee by phone"));

        dto.Status.Should().Be(PasswordResetRequestStatus.Dismissed);
        (await _db.Employees.SingleAsync(e => e.Id == employee.Id)).PasswordHash.Should().Be(oldHash);
    }

    public void Dispose() => _db.Dispose();

    private class FakeSystemConfigurationService : ISystemConfigurationService
    {
        public Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SystemSettingDto>>(new List<SystemSettingDto>());

        public Task<IReadOnlyList<SystemSettingDto>> UpdateAsync(UpdateSystemSettingsRequest request, string updatedByName, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SystemSettingDto>>(new List<SystemSettingDto>());

        // A fixed 30-minute OTP expiry is enough for these tests — none of
        // them exercise expiry behavior itself, they just need a real value
        // for IssueOtpAsync to compute an expiry timestamp with.
        public Task<int> GetIntAsync(string key, CancellationToken ct = default) => Task.FromResult(30);
        public Task<bool> GetBoolAsync(string key, CancellationToken ct = default) => Task.FromResult(false);
    }

    private class FakeEmailSender : IEmailSender
    {
        public Task<EmailSendResult> SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default) =>
            Task.FromResult(new EmailSendResult(true, null));
    }

    private class FakeNotificationService : INotificationService
    {
        public List<(NotificationRecipientType RecipientType, string RecipientId, string EventType, string Message)> Notifications { get; } = new();

        public Task NotifyAsync(NotificationRecipientType recipientType, string recipientId, string eventType, string message, CancellationToken ct = default)
        {
            Notifications.Add((recipientType, recipientId, eventType, message));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<NotificationDto>> GetForRecipientAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<NotificationDto>>(new List<NotificationDto>());

        public Task MarkReadAsync(Guid notificationId, CancellationToken ct = default) => Task.CompletedTask;

        public Task MarkAllReadAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default) => Task.CompletedTask;
    }
}
