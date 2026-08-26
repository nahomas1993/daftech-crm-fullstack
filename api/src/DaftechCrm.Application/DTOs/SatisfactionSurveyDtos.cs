namespace DaftechCrm.Application.DTOs;

/// <summary>An admin-authored survey question, in display order.</summary>
public record SurveyQuestionDto(Guid Id, string Text, int DisplayOrder, bool IsActive);

public record CreateSurveyQuestionRequest(string Text);
public record UpdateSurveyQuestionRequest(string Text, bool IsActive);

/// <summary>Bulk reorder — the full list of question IDs in the new display order.</summary>
public record ReorderSurveyQuestionsRequest(IReadOnlyList<Guid> OrderedQuestionIds);

/// <summary>One 1-5 rating the client gave to one question.</summary>
public record SurveyAnswerDto(Guid? QuestionId, string QuestionText, int DisplayOrder, int Rating);

public record SatisfactionSurveyDto(
    Guid Id,
    Guid TicketId,
    Guid ClientId,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<SurveyAnswerDto> Answers,
    string? SatisfactionComment
);

/// <summary>One rating the client is submitting for a given question.</summary>
public record SubmitSurveyAnswerRequest(Guid QuestionId, int Rating);

public record SubmitSatisfactionSurveyRequest(
    Guid TicketId,
    Guid ClientId,
    IReadOnlyList<SubmitSurveyAnswerRequest> Answers,
    string? SatisfactionComment
);
