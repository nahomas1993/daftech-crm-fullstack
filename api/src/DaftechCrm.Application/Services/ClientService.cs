using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class ClientService : IClientService
{
    private readonly IAppDbContext _db;
    private readonly AccountCredentialService _credentials;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly AccountReferenceIdService _accountRefIds;

    public ClientService(IAppDbContext db, AccountCredentialService credentials,
        ReferenceNumberService referenceNumbers, AccountReferenceIdService accountRefIds)
    {
        _db = db;
        _credentials = credentials;
        _referenceNumbers = referenceNumbers;
        _accountRefIds = accountRefIds;
    }

    /// <summary>Admin registers a client directly — Approved, credentialed, and emailed in the same call, no separate approval step.</summary>
    public async Task<ClientRegisteredResult> RegisterAsync(RegisterClientRequest request, CancellationToken ct = default)
    {
        // ItSupportContact remains optional — not every client has a
        // separate IT contact. Region/Zone/City/Woreda are required as
        // of this change (previously optional) — see RequiredFieldValidator.
        RequiredFieldValidator.EnsureAllPresent(
            ("Name / Organization", request.Name),
            ("Phone Number", request.PhoneNumber),
            ("Email", request.Email),
            ("Office", request.Office),
            ("Location", request.Location),
            ("Region", request.Region),
            ("Zone", request.Zone),
            ("City", request.City),
            ("Woreda", request.Woreda),
            ("KYC Type", request.KycType),
            ("KYC Contact", request.KycContact)
        );
        RequiredFieldValidator.EnsureGmailAddress(request.Email);

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

    /// <summary>Admin retry — regenerates the OTP and resends the credential email (SRS v2.0 §4.3.1).</summary>
    public async Task<ResendClientCredentialEmailResult> ResendCredentialEmailAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId, ct)
            ?? throw new InvalidOperationException("Client not found.");
        if (string.IsNullOrEmpty(client.Username))
            throw new InvalidOperationException("This client has not been issued credentials yet.");

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
        var baseQuery = _db.Clients.AsNoTracking().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            baseQuery = baseQuery.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Email, $"%{term}%") ||
                EF.Functions.ILike(c.PhoneNumber, $"%{term}%"));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
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

    public async Task<ClientDto> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && !c.IsDeleted, ct)
            ?? throw new InvalidOperationException("Client not found.");

        RequiredFieldValidator.EnsureAllPresent(
            ("Name / Organization", request.Name),
            ("Phone Number", request.PhoneNumber),
            ("Email", request.Email),
            ("Office", request.Office),
            ("Location", request.Location),
            ("Region", request.Region),
            ("Zone", request.Zone),
            ("City", request.City),
            ("Woreda", request.Woreda),
            ("KYC Type", request.KycType),
            ("KYC Contact", request.KycContact)
        );
        RequiredFieldValidator.EnsureGmailAddress(request.Email);

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
