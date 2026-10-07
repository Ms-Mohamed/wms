using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Data.Migrations
{
    /// <summary>
    /// Installs the database integrity layer (constraints, atomic stock functions, sequences, ledger).
    /// The SQL lives in Sql/stock_integrity.sql, embedded in this assembly, so it is also what the
    /// database tests execute. The script is idempotent.
    /// </summary>
    [DbContext(typeof(WmsDbContext))]
    [Migration("20261008000000_StockIntegrity")]
    public partial class StockIntegrity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var assembly = typeof(StockIntegrity).Assembly;
            using var stream = assembly.GetManifestResourceStream("WMS.Data.Sql.stock_integrity.sql")
                ?? throw new InvalidOperationException("Embedded resource WMS.Data.Sql.stock_integrity.sql not found");
            using var reader = new StreamReader(stream);
            migrationBuilder.Sql(reader.ReadToEnd());
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Functions/view/sequences are safe to drop; constraints and the ledger column are
            // deliberately kept: removing them would silently re-enable overselling.
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS wms_stock_drift;
                DROP FUNCTION IF EXISTS wms_stock_transfer(int,int,numeric,text);
                DROP FUNCTION IF EXISTS wms_stock_get_or_create(int,int,int,numeric);
                DROP FUNCTION IF EXISTS wms_stock_adjust(int,numeric,text);
                DROP FUNCTION IF EXISTS wms_stock_receive(int,numeric,numeric,int,text,text);
                DROP FUNCTION IF EXISTS wms_stock_issue(int,numeric,int,text,text);
                DROP FUNCTION IF EXISTS wms_next_number(text);");
        }
    }
}
