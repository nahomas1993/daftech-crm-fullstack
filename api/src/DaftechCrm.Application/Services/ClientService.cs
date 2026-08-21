using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class ClientService : IClientService
{
    private readonly IAppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly AccountCredentialService _credentials;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly AccountReferenceIdService _accountRefIds;

    public ClientService(IAppDbContext db, INotificationService notifications, AccountCredentialService credentials,
        ReferenceNumberService referenceNumbers, AccountReferenceIdService accountRefIds)
    {
        _db = db;
        _notifications = notifications;
        _credentials = credentials;
        _referenceNumbers = referenceNumbers;
        _accountRefIds = accountRefIds;
    }

    /// <summary>
    /// Self-service signup path. No credentials are issued here — the
    /// account has no way to log in until an Admin approves it, at which
    /// point ApproveAsync issues and emails the username/one-time password.
    /// </summary>
    public async Task<ClientDto> SubmitSignupAsync(CreateClientSignupRequest request, CancellationToken ct = default)
    {
        var client = new Client
        {
            Name = request.Name,
            IdNumber = await _referenceNumbers.GenerateClientIdNumberAsync(ct),
            AccountRefId = await _accountRefIds.GenerateForClientAsync(ct),
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Office = request.Office,
            Location = request.Location,
            Region = request.Region,
            Zone = request.Zone,
            City = request.City,
            Woreda = request.Woreda,
            KycType = "Pending Verification",
            KycContact = request.PhoneNumber,
            AccountStatus = ClientAccountStatus.Pending,
            OnboardingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Username = null,
            PasswordHash = null,
            MustChangePassword = true,
        };
        _db.Add(client);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(NotificationRecipientType.Admin, "ALL_ADMIN", "signup_request", $"{client.Name} submitted a signup request.", ct);
        return ToDto(client);
    }

    /// <summary>Admin registers a client directly — Approved, credentialed, and emailed in the same call, no separate approval step.</summary>
    public async Task<ClientRegisteredResult> RegisterAsync(RegisterClientRequest request, CancellationToken ct = default)
    {
        var issued = await _credentials.IssueForNameAsync(request.Name, ct);

        var client = new Client
        {
            Name = request.Name,
            IdNumber = await _referenceNumbers.GenerateClientIdNumberAsync(ct),
            AccountRefId = await _accountRefIds.GenerateForClientAsync(ct),
            PhoneNumber = request.PhoneNumber,
            Email = request.Email,
            Office = request.Office,
            Location = request.Location,
            Region = request.Region,
            Zone = request.Zone,
            City = request.City,
            Woreda = request.Woreda,
            KycType = request.KycType,
            KycContact = request.KycContact,
            ItSupportContact = request.ItSupportContact,
            AccountStatus = ClientAccountStatus.Approved,
            OnboardingDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Username = issued.Username,
            PasswordHash = PasswordHasher.Hash(issued.OneTimePassword),
            MustChangePassword = true,
        };
        _db.Add(client);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(
            client.Email, client.Name, issued.Username, issued.OneTimePassword, ct);

        return new ClientRegisteredResult(ToDto(client), issued.Username, issued.OneTimePassword, sent, error);
    }

    public async Task<ClientDto> ApproveAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException("Client not found.");

        client.AccountStatus = ClientAccountStatus.Approved;

        // A self-signup client has no credentials yet — issue and email them now, at the moment of approval.
        if (string.IsNullOrEmpty(client.Username))
        {
            var issued = await _credentials.IssueForNameAsync(client.Name, ct);
            client.Username = issued.Username;
            client.PasswordHash = PasswordHasher.Hash(issued.OneTimePassword);
            client.MustChangePassword = true;

            _db.Update(client);
            await _db.SaveChangesAsync(ct);

            await _credentials.SendCredentialEmailAsync(client.Email, client.Name, issued.Username, issued.OneTimePassword, ct);
        }
        else
        {
            _db.Update(client);
            await _db.SaveChangesAsync(ct);
        }

        await _notifications.NotifyAsync(NotificationRecipientType.Client, client.Id.ToString(), "signup_approved", "Your DAFTECH portal account has been approved. You can now log in.", ct);
        return ToDto(client);
    }

    public async Task<ClientDto> RejectAsync(Guid clientId, RejectClientRequest request, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException("Client not found.");
        client.AccountStatus = ClientAccountStatus.Rejected;
        client.RejectionReason = request.Reason;
        _db.Update(client);
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(NotificationRecipientType.Client, client.Id.ToString(), "signup_rejected", $"Your signup request was rejected: {request.Reason}", ct);
        return ToDto(client);
    }

    /// <summary>Admin retry — regenerates the OTP and resends the credential email (SRS v2.0 §4.3.1).</summary>
    public async Task<ResendClientCredentialEmailResult> ResendCredentialEmailAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException("Client not found.");
        if (string.IsNullOrEmpty(client.Username))
            throw new InvalidOperationException("This client has not been issued credentials yet — approve the signup first.");

        var newOneTimePassword = await _credentials.RegenerateOneTimePasswordAsync(ct);
        client.PasswordHash = PasswordHasher.Hash(newOneTimePassword);
        client.MustChangePassword = true;
        _db.Update(client);
        await _db.SaveChangesAsync(ct);

        var (sent, error) = await _credentials.SendCredentialEmailAsync(client.Email, client.Name, client.Username, newOneTimePassword, ct);
        return new ResendClientCredentialEmailResult(sent, error);
    }

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.Clients.AsNoTracking().Where(c => !c.IsDeleted).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PagedResult<ClientDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.Clients.CountAsync(c => !c.IsDeleted, ct);

        var items = await _db.Clients
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<ClientDto>(items.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<ClientDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return client is null ? null : ToDto(client);
    }

    public async Task<IReadOnlyList<ClientDto>> GetPendingAsync(CancellationToken ct = default) =>
        (await _db.Clients.AsNoTracking().Where(c => c.AccountStatus == ClientAccountStatus.Pending && !c.IsDeleted).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<ClientDto> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted, ct)
            ?? throw new InvalidOperationException("Client not found.");

        client.Name = request.Name;
        client.PhoneNumber = request.PhoneNumber;
        client.Email = request.Email;
        client.Office = request.Office;
        client.Location = request.Location;
        client.Region = request.Region;
        client.Zone = request.Zone;
        client.City = request.City;
        client.Woreda = request.Woreda;
        client.KycType = request.KycType;
        client.KycContact = request.KycContact;
        client.ItSupportContact = request.ItSupportContact;
        _db.Update(client);
        await _db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    public async Task DeleteAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted, ct)
            ?? throw new InvalidOperationException("Client not found.");

        client.IsDeleted = true;
        client.DeletedAt = DateTimeOffset.UtcNow;
        _db.Update(client);
        await _db.SaveChangesAsync(ct);
    }

    private static ClientDto ToDto(Client c) => new(
        c.Id, c.Name, c.IdNumber, c.PhoneNumber, c.Email, c.Office, c.Location,
        c.Region, c.Zone, c.City, c.Woreda,
        c.KycType, c.KycContact, c.ItSupportContact, c.AccountStatus, c.OnboardingDate, c.RejectionReason,
        c.Username, c.MustChangePassword, c.AccountRefId
    );
}
