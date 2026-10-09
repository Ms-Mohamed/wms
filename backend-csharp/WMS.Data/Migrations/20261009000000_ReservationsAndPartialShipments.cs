using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Data.Migrations
{
    /// <summary>
    /// Reservations, partial shipments (Sql/stock_reservations.sql) and the covering index for the
    /// analytics /predict query (Sql/analytics_indexes.sql). Both scripts are idempotent and embedded.
    /// </summary>
    [DbContext(typeof(WmsDbContext))]
    [Migration("20261009000000_ReservationsAndPartialShipments")]
    public partial class ReservationsAndPartialShipments : Migration
    {
        private static string Load(string name)
        {
            var assembly = typeof(ReservationsAndPartialShipments).Assembly;
            using var stream = assembly.GetManifestResourceStream("WMS.Data.Sql." + name)
                ?? throw new InvalidOperationException($"Embedded resource WMS.Data.Sql.{name} not found");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Load("stock_reservations.sql"));
            migrationBuilder.Sql(Load("analytics_indexes.sql"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reservations are business data: the table and the ShippedQuantity column are kept on purpose.
            migrationBuilder.Sql(@"
                DROP VIEW IF EXISTS wms_reservation_drift;
                DROP FUNCTION IF EXISTS wms_order_cancel(int);
                DROP FUNCTION IF EXISTS wms_order_ship_line(int,int,numeric,text);
                DROP FUNCTION IF EXISTS wms_order_release(int);
                DROP FUNCTION IF EXISTS wms_order_reserve(int);
                DROP INDEX IF EXISTS ix_orders_id_cover;");
        }
    }
}
