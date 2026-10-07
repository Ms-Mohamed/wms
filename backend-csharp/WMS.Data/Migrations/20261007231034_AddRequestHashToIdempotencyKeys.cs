using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestHashToIdempotencyKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "IdempotencyKeys",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "IdempotencyKeys");
        }
    }
}
