using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class SurveyQuestionService : ISurveyQuestionService
{
    private readonly IAppDbContext _db;
    public SurveyQuestionService(IAppDbContext db) => _db = db;

    private static SurveyQuestionDto ToDto(SurveyQuestion q) => new(q.Id, q.Text, q.DisplayOrder, q.IsActive);

    public async Task<IReadOnlyList<SurveyQuestionDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.SurveyQuestions.AsNoTracking().OrderBy(q => q.DisplayOrder).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<SurveyQuestionDto>> GetActiveAsync(CancellationToken ct = default) =>
        (await _db.SurveyQuestions.AsNoTracking().Where(q => q.IsActive).OrderBy(q => q.DisplayOrder).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<SurveyQuestionDto> CreateAsync(CreateSurveyQuestionRequest request, CancellationToken ct = default)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Question text is required.");

        var maxOrder = await _db.SurveyQuestions.Select(q => (int?)q.DisplayOrder).MaxAsync(ct) ?? -1;

        var entry = new SurveyQuestion { Text = text, DisplayOrder = maxOrder + 1, IsActive = true };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<SurveyQuestionDto> UpdateAsync(Guid id, UpdateSurveyQuestionRequest request, CancellationToken ct = default)
    {
        var entry = await _db.SurveyQuestions.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new InvalidOperationException("Survey question not found.");

        var text = request.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Question text is required.");

        entry.Text = text;
        entry.IsActive = request.IsActive;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task ReorderAsync(ReorderSurveyQuestionsRequest request, CancellationToken ct = default)
    {
        var ids = request.OrderedQuestionIds;
        if (ids is null || ids.Count == 0)
            throw new InvalidOperationException("No question order was provided.");

        var questions = await _db.SurveyQuestions.Where(q => ids.Contains(q.Id)).ToListAsync(ct);
        var lookup = questions.ToDictionary(q => q.Id);

        for (var i = 0; i < ids.Count; i++)
        {
            if (lookup.TryGetValue(ids[i], out var q))
            {
                q.DisplayOrder = i;
                _db.Update(q);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.SurveyQuestions.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw new InvalidOperationException("Survey question not found.");

        _db.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}
