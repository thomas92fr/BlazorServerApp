using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Model.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorToPerson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MentorId",
                table: "Persons",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Persons_MentorId",
                table: "Persons",
                column: "MentorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Persons_Persons_MentorId",
                table: "Persons",
                column: "MentorId",
                principalTable: "Persons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Persons_Persons_MentorId",
                table: "Persons");

            migrationBuilder.DropIndex(
                name: "IX_Persons_MentorId",
                table: "Persons");

            migrationBuilder.DropColumn(
                name: "MentorId",
                table: "Persons");
        }
    }
}
