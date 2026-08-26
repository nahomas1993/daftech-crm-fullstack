using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

/// <summary>
/// Replaces the hardcoded 4-question satisfaction survey with an
/// admin-configurable question set:
///   - survey_questions: admin-authored questions (text, display order,
///     active flag), edited from Settings → Configuration → Satisfaction
///     Survey.
///   - survey_answers: one row per question a client rated (1-5) on a
///     given SatisfactionSurvey, with the question text snapshotted so
///     historical answers stay correct even if a question is later
///     edited or deleted.
///   - satisfaction_surveys: the four old fixed rating columns
///     (ResponseSpeedRating, ProfessionalismRating,
///     CommunicationClarityRating, LikelihoodToRecommend) are dropped;
///     ImprovementFeedback is renamed to SatisfactionComment (same
///     purpose — the client's own words, kept as free text).
///
/// Written with IF EXISTS / IF NOT EXISTS so it's safe to re-run against a
/// database that already has some of these changes applied by hand.
/// </summary>
public partial class AddDynamicSatisfactionSurvey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS ""survey_questions"" (
                ""Id"" uuid NOT NULL,
                ""Text"" text NOT NULL,
                ""DisplayOrder"" integer NOT NULL DEFAULT 0,
                ""IsActive"" boolean NOT NULL DEFAULT TRUE,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT ""PK_survey_questions"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_survey_questions_DisplayOrder""
            ON ""survey_questions"" (""DisplayOrder"");
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS ""survey_answers"" (
                ""Id"" uuid NOT NULL,
                ""SatisfactionSurveyId"" uuid NOT NULL,
                ""SurveyQuestionId"" uuid NULL,
                ""QuestionText"" text NOT NULL,
                ""DisplayOrder"" integer NOT NULL DEFAULT 0,
                ""Rating"" integer NOT NULL,
                CONSTRAINT ""PK_survey_answers"" PRIMARY KEY (""Id""),
                CONSTRAINT ""FK_survey_answers_satisfaction_surveys_SatisfactionSurveyId""
                    FOREIGN KEY (""SatisfactionSurveyId"") REFERENCES ""satisfaction_surveys"" (""Id"") ON DELETE CASCADE,
                CONSTRAINT ""FK_survey_answers_survey_questions_SurveyQuestionId""
                    FOREIGN KEY (""SurveyQuestionId"") REFERENCES ""survey_questions"" (""Id"") ON DELETE SET NULL
            );
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_survey_answers_SatisfactionSurveyId""
            ON ""survey_answers"" (""SatisfactionSurveyId"");
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_survey_answers_SurveyQuestionId""
            ON ""survey_answers"" (""SurveyQuestionId"");
        ");

        // ImprovementFeedback -> SatisfactionComment (rename keeps existing free-text answers).
        migrationBuilder.Sql(@"
            ALTER TABLE ""satisfaction_surveys""
            RENAME COLUMN ""ImprovementFeedback"" TO ""SatisfactionComment"";
        ");

        // Best-effort migration of old fixed-question ratings into the new
        // answer rows, tagged against synthetic legacy questions, before the
        // old columns are dropped — preserves historical data instead of
        // silently discarding it.
        migrationBuilder.Sql(@"
            INSERT INTO ""survey_questions"" (""Id"", ""Text"", ""DisplayOrder"", ""IsActive"", ""CreatedAt"")
            SELECT gen_random_uuid(), v.text, v.ord, FALSE, now()
            FROM (VALUES
                ('How would you rate the speed of our response?', 0),
                ('How would you rate the technician''s professionalism?', 1),
                ('How clearly was the issue explained to you?', 2),
                ('How likely are you to recommend DAFTECH support to a colleague?', 3)
            ) AS v(text, ord)
            WHERE EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'satisfaction_surveys' AND column_name = 'ResponseSpeedRating')
              AND NOT EXISTS (SELECT 1 FROM ""survey_questions"" WHERE ""Text"" = v.text);
        ");

        migrationBuilder.Sql(@"
            DO $$
            DECLARE
                q_speed uuid; q_prof uuid; q_clarity uuid; q_recommend uuid;
            BEGIN
                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'satisfaction_surveys' AND column_name = 'ResponseSpeedRating') THEN
                    SELECT ""Id"" INTO q_speed FROM ""survey_questions"" WHERE ""Text"" = 'How would you rate the speed of our response?' LIMIT 1;
                    SELECT ""Id"" INTO q_prof FROM ""survey_questions"" WHERE ""Text"" = 'How would you rate the technician''s professionalism?' LIMIT 1;
                    SELECT ""Id"" INTO q_clarity FROM ""survey_questions"" WHERE ""Text"" = 'How clearly was the issue explained to you?' LIMIT 1;
                    SELECT ""Id"" INTO q_recommend FROM ""survey_questions"" WHERE ""Text"" = 'How likely are you to recommend DAFTECH support to a colleague?' LIMIT 1;

                    INSERT INTO ""survey_answers"" (""Id"", ""SatisfactionSurveyId"", ""SurveyQuestionId"", ""QuestionText"", ""DisplayOrder"", ""Rating"")
                    SELECT gen_random_uuid(), s.""Id"", q_speed, 'How would you rate the speed of our response?', 0, s.""ResponseSpeedRating"" FROM ""satisfaction_surveys"" s
                    UNION ALL
                    SELECT gen_random_uuid(), s.""Id"", q_prof, 'How would you rate the technician''s professionalism?', 1, s.""ProfessionalismRating"" FROM ""satisfaction_surveys"" s
                    UNION ALL
                    SELECT gen_random_uuid(), s.""Id"", q_clarity, 'How clearly was the issue explained to you?', 2, s.""CommunicationClarityRating"" FROM ""satisfaction_surveys"" s
                    UNION ALL
                    SELECT gen_random_uuid(), s.""Id"", q_recommend, 'How likely are you to recommend DAFTECH support to a colleague?', 3, s.""LikelihoodToRecommend"" FROM ""satisfaction_surveys"" s;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" DROP COLUMN IF EXISTS ""ResponseSpeedRating"";");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" DROP COLUMN IF EXISTS ""ProfessionalismRating"";");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" DROP COLUMN IF EXISTS ""CommunicationClarityRating"";");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" DROP COLUMN IF EXISTS ""LikelihoodToRecommend"";");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" ADD COLUMN IF NOT EXISTS ""ResponseSpeedRating"" integer NOT NULL DEFAULT 0;");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" ADD COLUMN IF NOT EXISTS ""ProfessionalismRating"" integer NOT NULL DEFAULT 0;");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" ADD COLUMN IF NOT EXISTS ""CommunicationClarityRating"" integer NOT NULL DEFAULT 0;");
        migrationBuilder.Sql(@"ALTER TABLE ""satisfaction_surveys"" ADD COLUMN IF NOT EXISTS ""LikelihoodToRecommend"" integer NOT NULL DEFAULT 0;");

        migrationBuilder.Sql(@"
            ALTER TABLE ""satisfaction_surveys""
            RENAME COLUMN ""SatisfactionComment"" TO ""ImprovementFeedback"";
        ");

        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""survey_answers"";");
        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""survey_questions"";");
    }
}
