using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGTwitchBot.DAL.Migrations.Migrations
{
    public partial class PCGAddWeight : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Weight",
                table: "PCG",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Weight",
                table: "PCG");
        }
    }
}
