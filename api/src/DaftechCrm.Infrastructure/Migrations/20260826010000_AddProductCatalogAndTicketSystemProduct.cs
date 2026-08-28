using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DaftechCrm.Infrastructure.Migrations;

/// <summary>
/// Adds three things, all in support of the Systems/Products configurability
/// and issue-submission work:
///
///   - product_catalog_items: a new admin-managed lookup table (matching
///     the FailureType/SupportType pattern) so an Admin can add, edit, and
///     retire system/product names from Settings without a code change.
///   - system_products.CatalogItemId: optional link from a client's
///     per-client SystemProduct back to the catalog entry it was created
///     from, plus system_products.ExpiryDate for showing each client
///     product's own expiry on the client dashboard.
///   - tickets.SystemProductId: which of the client's systems/products an
///     issue is about, so Admin can always see which product a ticket
///     belongs to.
///
/// Written with IF EXISTS / IF NOT EXISTS so it's safe to re-run against a
/// database that already has some of these changes applied by hand.
/// </summary>
public partial class AddProductCatalogAndTicketSystemProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS ""product_catalog_items"" (
                ""Id"" uuid NOT NULL,
                ""Name"" character varying(200) NOT NULL,
                ""Description"" character varying(500) NULL,
                ""IsActive"" boolean NOT NULL DEFAULT TRUE,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT ""PK_product_catalog_items"" PRIMARY KEY (""Id"")
            );
        ");

        migrationBuilder.Sql(@"
            CREATE UNIQUE INDEX IF NOT EXISTS ""IX_product_catalog_items_Name""
            ON ""product_catalog_items"" (""Name"");
        ");

        // Seed the catalog from whatever distinct SystemProduct names
        // already exist, so existing deployments aren't left with an empty
        // dropdown the first time an Admin opens the new Settings tab.
        migrationBuilder.Sql(@"
            INSERT INTO ""product_catalog_items"" (""Id"", ""Name"", ""IsActive"", ""CreatedAt"")
            SELECT gen_random_uuid(), sp.""Name"", TRUE, now()
            FROM (SELECT DISTINCT ""Name"" FROM ""system_products"") sp
            WHERE NOT EXISTS (
                SELECT 1 FROM ""product_catalog_items"" pci WHERE pci.""Name"" = sp.""Name""
            );
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""system_products"" ADD COLUMN IF NOT EXISTS ""CatalogItemId"" uuid NULL;
        ");
        migrationBuilder.Sql(@"
            ALTER TABLE ""system_products"" ADD COLUMN IF NOT EXISTS ""ExpiryDate"" date NULL;
        ");

        // Backfill CatalogItemId for existing rows by matching on Name,
        // now that every existing Name is guaranteed to have a catalog
        // entry from the seed step above.
        migrationBuilder.Sql(@"
            UPDATE ""system_products"" sp
            SET ""CatalogItemId"" = pci.""Id""
            FROM ""product_catalog_items"" pci
            WHERE sp.""CatalogItemId"" IS NULL AND pci.""Name"" = sp.""Name"";
        ");

        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'FK_system_products_product_catalog_items_CatalogItemId'
                ) THEN
                    ALTER TABLE ""system_products""
                    ADD CONSTRAINT ""FK_system_products_product_catalog_items_CatalogItemId""
                    FOREIGN KEY (""CatalogItemId"") REFERENCES ""product_catalog_items"" (""Id"") ON DELETE SET NULL;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_system_products_CatalogItemId""
            ON ""system_products"" (""CatalogItemId"");
        ");

        migrationBuilder.Sql(@"
            ALTER TABLE ""tickets"" ADD COLUMN IF NOT EXISTS ""SystemProductId"" uuid NULL;
        ");

        // Backfill SystemProductId on existing tickets from their
        // Agreement's SystemProductId, so historical tickets show a
        // product too, not just ones submitted after this migration.
        migrationBuilder.Sql(@"
            UPDATE ""tickets"" t
            SET ""SystemProductId"" = a.""SystemProductId""
            FROM ""agreements"" a
            WHERE t.""SystemProductId"" IS NULL AND a.""Id"" = t.""AgreementId"";
        ");

        migrationBuilder.Sql(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.table_constraints
                    WHERE constraint_name = 'FK_tickets_system_products_SystemProductId'
                ) THEN
                    ALTER TABLE ""tickets""
                    ADD CONSTRAINT ""FK_tickets_system_products_SystemProductId""
                    FOREIGN KEY (""SystemProductId"") REFERENCES ""system_products"" (""Id"") ON DELETE SET NULL;
                END IF;
            END $$;
        ");

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_tickets_SystemProductId""
            ON ""tickets"" (""SystemProductId"");
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"ALTER TABLE ""tickets"" DROP CONSTRAINT IF EXISTS ""FK_tickets_system_products_SystemProductId"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_tickets_SystemProductId"";");
        migrationBuilder.Sql(@"ALTER TABLE ""tickets"" DROP COLUMN IF EXISTS ""SystemProductId"";");

        migrationBuilder.Sql(@"ALTER TABLE ""system_products"" DROP CONSTRAINT IF EXISTS ""FK_system_products_product_catalog_items_CatalogItemId"";");
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_system_products_CatalogItemId"";");
        migrationBuilder.Sql(@"ALTER TABLE ""system_products"" DROP COLUMN IF EXISTS ""ExpiryDate"";");
        migrationBuilder.Sql(@"ALTER TABLE ""system_products"" DROP COLUMN IF EXISTS ""CatalogItemId"";");

        migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""product_catalog_items"";");
    }
}
