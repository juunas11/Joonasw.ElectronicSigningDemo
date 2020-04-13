using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Joonasw.ElectronicSigningDemo.Data.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Subject = table.Column<string>(nullable: true),
                    Message = table.Column<string>(nullable: true),
                    DocumentName = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    WorkflowStartedAt = table.Column<DateTimeOffset>(nullable: true),
                    WorkflowCompletedAt = table.Column<DateTimeOffset>(nullable: true),
                    Workflow_Id = table.Column<string>(maxLength: 64, nullable: true),
                    Workflow_StatusQueryUrl = table.Column<string>(maxLength: 512, nullable: true),
                    Workflow_SendEventUrl = table.Column<string>(maxLength: 512, nullable: true),
                    Workflow_TerminateUrl = table.Column<string>(maxLength: 512, nullable: true),
                    Workflow_PurgeHistoryUrl = table.Column<string>(maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(nullable: true),
                    WaitForSignatureInstanceId = table.Column<string>(nullable: true),
                    Signed = table.Column<bool>(nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(nullable: true),
                    RequestId = table.Column<Guid>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Signers_Requests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "Requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Signers_RequestId",
                table: "Signers",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Signers_Email_RequestId",
                table: "Signers",
                columns: new[] { "Email", "RequestId" },
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Signers");

            migrationBuilder.DropTable(
                name: "Requests");
        }
    }
}
