using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Describes one configurable setting: its key, default (sourced from the
/// bound Options classes so appsettings.json stays the single source of
/// truth for defaults), and how the Configuration page should present it.
/// </summary>
public record SettingDefinition(string Key, string Category, string Label, string Description, string ValueType, string DefaultValue);

public class SystemConfigurationService : ISystemConfigurationService
{
    private readonly IAppDbContext _db;
    private readonly List<SettingDefinition> _registry;

    public SystemConfigurationService(
        IAppDbContext db,
        IOptions<TicketWorkflowOptions> ticketWorkflow,
        IOptions<SessionOptions> session)
    {
        _db = db;

        var tw = ticketWorkflow.Value;
        var s = session.Value;

        // The full catalog of admin-configurable settings. Adding a new
        // knob elsewhere in the system means adding one line here — no
        // migration required, since values are stored as key/value rows.
        _registry = new List<SettingDefinition>
        {
            new("TicketWorkflow.OnTimeResolutionTargetDays", "Ticket Workflow", "On-time resolution target (days)",
                "How many days a technician has to resolve a ticket from the moment it's assigned before it counts as overdue.",
                "int", tw.OnTimeResolutionTargetDays.ToString()),

            new("TicketWorkflow.SlightlyOverdueGraceHours", "Ticket Workflow", "Slightly-overdue grace period (hours)",
                "If a ticket is resolved within this many hours after its deadline, it's reported as \"slightly overdue\" instead of \"overdue\".",
                "int", "4"),

            new("TicketWorkflow.MinimumSatisfactionScore", "Ticket Workflow", "Minimum satisfaction score to auto-close",
                "Client satisfaction score (0-100) required to close a ticket normally. Below this, the ticket escalates instead.",
                "int", tw.MinimumSatisfactionScore.ToString()),

            new("TicketWorkflow.ClientConfirmationWindowDays", "Ticket Workflow", "Client confirmation window (days)",
                "Days after a ticket is marked Resolved before an unanswered client confirmation auto-closes it.",
                "int", tw.ClientConfirmationWindowDays.ToString()),

            new("Session.OfflineAfterMinutes", "Sessions & Devices", "Mark offline after (minutes)",
                "Minutes of no activity ping before a logged-in session is marked offline.",
                "int", s.OfflineAfterMinutes.ToString()),

            new("Session.MaxDevicesPerAccount", "Sessions & Devices", "Max simultaneous devices",
                "Maximum number of devices a non-Admin account (staff or client) may be signed in on at once. Admin accounts are exempt.",
                "int", "4"),

            new("Auth.OtpExpiryMinutes", "Authentication", "OTP code expiry (minutes)",
                "How long a password-reset OTP emailed to a user stays valid.",
                "int", "15"),
        };
    }

    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct = default)
    {
        var overrides = await _db.SystemSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x, ct);

        return _registry.Select(def =>
        {
            overrides.TryGetValue(def.Key, out var row);
            return new SystemSettingDto(
                def.Key,
                row?.Value ?? def.DefaultValue,
                def.Category,
                def.Label,
                def.Description,
                def.ValueType,
                row?.UpdatedAt,
                row?.UpdatedByName
            );
        }).ToList();
    }

    public async Task<IReadOnlyList<SystemSettingDto>> UpdateAsync(UpdateSystemSettingsRequest request, string updatedByName, CancellationToken ct = default)
    {
        var validKeys = _registry.ToDictionary(d => d.Key, d => d);

        foreach (var update in request.Settings)
        {
            if (!validKeys.TryGetValue(update.Key, out var def))
                throw new InvalidOperationException($"Unknown setting key: {update.Key}");

            ValidateValue(def, update.Value);

            var existing = await _db.SystemSettings.FirstOrDefaultAsync(x => x.Key == update.Key, ct);
            if (existing is null)
            {
                _db.Add(new SystemSetting
                {
                    Key = update.Key,
                    Value = update.Value,
                    Category = def.Category,
                    UpdatedByName = updatedByName,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                existing.Value = update.Value;
                existing.UpdatedByName = updatedByName;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                _db.Update(existing);
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetAllAsync(ct);
    }

    public async Task<int> GetIntAsync(string key, CancellationToken ct = default)
    {
        var value = await ResolveRawAsync(key, ct);
        return int.TryParse(value, out var i) ? i : 0;
    }

    public async Task<bool> GetBoolAsync(string key, CancellationToken ct = default)
    {
        var value = await ResolveRawAsync(key, ct);
        return bool.TryParse(value, out var b) && b;
    }

    private async Task<string> ResolveRawAsync(string key, CancellationToken ct)
    {
        var def = _registry.FirstOrDefault(d => d.Key == key)
            ?? throw new InvalidOperationException($"Unknown setting key: {key}");

        var row = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, ct);
        return row?.Value ?? def.DefaultValue;
    }

    private static void ValidateValue(SettingDefinition def, string value)
    {
        switch (def.ValueType)
        {
            case "int":
                if (!int.TryParse(value, out var i) || i < 0)
                    throw new InvalidOperationException($"\"{def.Label}\" must be a non-negative whole number.");
                break;
            case "bool":
                if (!bool.TryParse(value, out _))
                    throw new InvalidOperationException($"\"{def.Label}\" must be true or false.");
                break;
        }
    }
}
