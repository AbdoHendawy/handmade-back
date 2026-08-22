using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Handmade.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderGroupPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "order_groups",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CashOnDelivery");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "order_groups");
        }
    }
}
