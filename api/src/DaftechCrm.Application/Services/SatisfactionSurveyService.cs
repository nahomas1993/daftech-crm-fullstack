using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class SatisfactionSurveyService : ISatisfactionSurveyService
{
    private readonly IAppDbContext _db;
    public SatisfactionSurveyService(IAppDbContext db) => _db = db;

    public async Task<SatisfactionSurveyDto> SubmitAsync(SubmitSatisfactionSurveyRequest request, CancellationToken ct = default)
    {
        if (request.Answers is null || request.Answers.Count == 0)
            throw new InvalidOperationException("At least one question must be answered.");

        foreach (var a in request.Answers)
        {
            if (a.Rating is < 1 or > 5)
                throw new InvalidOperationException("Each rating must be between 1 and 5.");
        }

        // Snapshot the current question text/order so historical answers
        // stay accurate even if an admin later edits or deletes a question.
        var questionIds = request.Answers.Select(a => a.QuestionId).ToList();
        var questions = await _db.SurveyQuestions
            .Where(q => questionIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, ct);

        var existing = await _db.SatisfactionSurveys
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.TicketId == request.TicketId, ct);

        var comment = string.IsNullOrWhiteSpace(request.SatisfactionComment) ? null : request.SatisfactionComment.Trim();

        if (existing is not null)
        {
            foreach (var old in existing.Answers.ToList())
                _db.Remove(old);

            existing.SatisfactionComment = comment;
            existing.SubmittedAt = DateTimeOffset.UtcNow;
            existing.Answers = BuildAnswers(request.Answers, questions);
            foreach (var a in existing.Answers)
                a.SatisfactionSurveyId = existing.Id;
            foreach (var a in existing.Answers)
                _db.Add(a);

            _db.Update(existing);
            await _db.SaveChangesAsync(ct);
            return ToDto(existing);
        }

        var survey = new SatisfactionSurvey
        {
            TicketId = request.TicketId,
            ClientId = request.ClientId,
            SatisfactionComment = comment,
        };
        survey.Answers = BuildAnswers(request.Answers, questions);
        foreach (var a in survey.Answers)
            a.SatisfactionSurveyId = survey.Id;

        _db.Add(survey);
        foreach (var a in survey.Answers)
            _db.Add(a);

        await _db.SaveChangesAsync(ct);
        return ToDto(survey);
    }

    private static List<SurveyAnswer> BuildAnswers(IReadOnlyList<SubmitSurveyAnswerRequest> answers, Dictionary<Guid, SurveyQuestion> questions)
    {
        return answers.Select((a, i) =>
        {
            questions.TryGetValue(a.QuestionId, out var q);
            return new SurveyAnswer
            {
                SurveyQuestionId = a.QuestionId,
                QuestionText = q?.Text ?? "(question no longer available)",
                DisplayOrder = q?.DisplayOrder ?? i,
                Rating = a.Rating,
            };
        }).ToList();
    }

    public async Task<SatisfactionSurveyDto?> GetForTicketAsync(Guid ticketId, CancellationToken ct = default)
    {
        var survey = await _db.SatisfactionSurveys.AsNoTracking()
            .Include(s => s.Answers)
            .FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);
        return survey is null ? null : ToDto(survey);
    }

    public async Task<IReadOnlyList<SatisfactionSurveyDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.SatisfactionSurveys.AsNoTracking().Include(s => s.Answers).OrderByDescending(s => s.SubmittedAt).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PagedResult<SatisfactionSurveyDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.SatisfactionSurveys.CountAsync(ct);

        var items = await _db.SatisfactionSurveys
            .AsNoTracking()
            .Include(s => s.Answers)
            .OrderByDescending(s => s.SubmittedAt)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<SatisfactionSurveyDto>(items.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    private static SatisfactionSurveyDto ToDto(SatisfactionSurvey s) => new(
        s.Id, s.TicketId, s.ClientId, s.SubmittedAt,
        s.Answers.OrderBy(a => a.DisplayOrder)
            .Select(a => new SurveyAnswerDto(a.SurveyQuestionId, a.QuestionText, a.DisplayOrder, a.Rating))
            .ToList(),
        s.SatisfactionComment
    );
}
