using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GGTwitch.DAL.Migrations.Migrations
{
    public partial class PokecatchersCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pokecatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StreamerUsername = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CatcherUsername = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pokecatches", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pokecatches");
        }
    }
}
