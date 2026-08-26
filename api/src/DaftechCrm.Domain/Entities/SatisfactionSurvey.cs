namespace DaftechCrm.Domain.Entities;

/// <summary>
/// An admin-authored question shown on the client satisfaction survey.
/// Each is answered on a 1-5 scale (1-Poor, 2-Satisfactory, 3-Good,
/// 4-Very good, 5-Excellent). Admins can add, edit, reorder, and delete
/// questions from Settings → Configuration → Satisfaction Survey — there
/// is no fixed/hardcoded question set. Deleting a question does not
/// remove it from surveys already submitted; see SurveyAnswer.QuestionText.
/// </summary>
public class SurveyQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Text { get; set; } = default!;

    /// <summary>Controls the order questions are shown to the client, ascending.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// An optional satisfaction survey a client can fill out after a ticket
/// closes. This does NOT feed the 90/100 CSAT gate — that's driven solely
/// by Ticket.SatisfactionStars/Score. This is additional qualitative/
/// quantitative feedback for reporting. The question set is fully
/// admin-configurable (see SurveyQuestion) rather than hardcoded; answers
/// are stored in SurveyAnswer, one row per question the client rated.
/// </summary>
public class SatisfactionSurvey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public Ticket Ticket { get; set; } = default!;

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<SurveyAnswer> Answers { get; set; } = new();

    /// <summary>
    /// The client's own words describing their experience — a short
    /// paragraph (roughly five lines), free text, optional.
    /// </summary>
    public string? SatisfactionComment { get; set; }
}

/// <summary>
/// One rating (1-5) a client gave to one SurveyQuestion as part of a
/// SatisfactionSurvey. QuestionText is snapshotted at submission time so
/// historical answers still display correctly even if an admin later
/// edits or deletes the question.
/// </summary>
public class SurveyAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SatisfactionSurveyId { get; set; }
    public SatisfactionSurvey SatisfactionSurvey { get; set; } = default!;

    /// <summary>Nullable — preserved for reporting even if the question is later deleted.</summary>
    public Guid? SurveyQuestionId { get; set; }
    public SurveyQuestion? SurveyQuestion { get; set; }

    /// <summary>Snapshot of the question's text at the time this was answered.</summary>
    public string QuestionText { get; set; } = default!;

    public int DisplayOrder { get; set; }

    /// <summary>1-Poor, 2-Satisfactory, 3-Good, 4-Very good, 5-Excellent.</summary>
    public int Rating { get; set; }
}
