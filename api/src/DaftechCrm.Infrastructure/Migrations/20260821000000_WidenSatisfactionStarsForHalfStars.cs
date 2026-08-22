using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations
{
    /// <summary>
    /// Widens tickets.SatisfactionStars from integer to numeric(2,1) so a
    /// client can rate in 0.5 increments (e.g. 4.5 stars), not just whole
    /// stars. Existing whole-star ratings (e.g. 4) convert losslessly to
    /// the equivalent decimal (4.0) — USING is only needed because
    /// Postgres won't implicitly narrow/widen integer<->numeric on ALTER
    /// COLUMN. SatisfactionScore (the derived 0-100 value) is untouched —
    /// 4.5 * 20 = 90 is still always a whole number, so it stays integer.
    /// </summary>
    public partial class WidenSatisfactionStarsForHalfStars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE tickets
                ALTER COLUMN ""SatisfactionStars"" TYPE numeric(2,1)
                USING ""SatisfactionStars""::numeric(2,1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Any half-star rating (e.g. 4.5) rounds to the nearest whole
            // star on rollback — reverting this migration is a deliberate
            // feature removal, not just a schema change, so some precision
            // loss here is expected and acceptable.
            migrationBuilder.Sql(@"
                ALTER TABLE tickets
                ALTER COLUMN ""SatisfactionStars"" TYPE integer
                USING ROUND(""SatisfactionStars"")::integer;
            ");
        }
    }
}
