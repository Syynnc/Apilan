using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Apilan.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationsFeedbackAndAttendanceStatus : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Organizations, event feedback, and the present/late/absent verdict.
        ///
        /// Two parts of this are hand-written on top of what the scaffolder
        /// produced, because an existing database has rows that the new schema
        /// has to account for:
        ///
        ///   1. Events.OrganizationId is required, and its foreign key would be
        ///      violated by every event already on file. So the Organizations
        ///      table is created first, a placeholder org is inserted, existing
        ///      events are pointed at it, and only then is the key added. An
        ///      admin renames that org afterwards; nothing is orphaned and no
        ///      event has to be deleted to migrate.
        ///
        ///   2. LateAfterMinutes and CollectsFeedback are backfilled to the
        ///      model's own defaults rather than to SQL's zero/false. Otherwise
        ///      every existing session would have a zero-minute grace period —
        ///      a stricter rule than anyone asked for — and the flag would mean
        ///      different things on old and new rows.
        ///
        /// Existing attendance rows take Status = 0 (Present), which is what
        /// they already meant: a record used to exist only if somebody arrived.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EventSessionId",
                table: "AttendanceRecords");

            // Moved ahead of the Events columns by hand: the events need
            // somewhere to point before their foreign key goes on.
            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(140)", maxLength: 140, nullable: false),
                    Acronym = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Adviser = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ColorKey = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            // Only when there is history to keep. A fresh install gets its
            // organizations from the seeder instead, with real names.
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [Events])
                   AND NOT EXISTS (SELECT 1 FROM [Organizations])
                BEGIN
                    INSERT INTO [Organizations]
                        ([Name], [Acronym], [Description], [ColorKey], [IsActive], [CreatedOn])
                    VALUES
                        (N'Our Organization', N'ORG',
                         N'Created when organizations were introduced, to hold the events that already existed. Rename it in Master data - Organizations.',
                         N'talk', 1, SYSDATETIME());
                END");

            migrationBuilder.AddColumn<int>(
                name: "LateAfterMinutes",
                table: "EventSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "CollectsFeedback",
                table: "Events",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastEditedByUserId",
                table: "Events",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEditedOn",
                table: "Events",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "Events",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeIn",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AttendanceRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "AttendanceRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OrganizationId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EventFeedback",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationId = table.Column<int>(type: "int", nullable: false),
                    OverallRating = table.Column<int>(type: "int", nullable: false),
                    ContentRating = table.Column<int>(type: "int", nullable: false),
                    VenueRating = table.Column<int>(type: "int", nullable: false),
                    OrganizationRating = table.Column<int>(type: "int", nullable: false),
                    WouldRecommend = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Improvement = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventFeedback", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventFeedback_Registrations_RegistrationId",
                        column: x => x.RegistrationId,
                        principalTable: "Registrations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---- Hand-written backfill -------------------------------------

            // Point every existing event at the placeholder org.
            migrationBuilder.Sql(@"
                UPDATE [Events]
                   SET [OrganizationId] = (SELECT MIN([Id]) FROM [Organizations])
                 WHERE [OrganizationId] = 0
                   AND EXISTS (SELECT 1 FROM [Organizations])");

            // The model's defaults, not SQL's.
            migrationBuilder.Sql("UPDATE [EventSessions] SET [LateAfterMinutes] = 15");
            migrationBuilder.Sql("UPDATE [Events] SET [CollectsFeedback] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Events_LastEditedByUserId",
                table: "Events",
                column: "LastEditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_OrganizationId_StartsAt",
                table: "Events",
                columns: new[] { "OrganizationId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EventSessionId_Status",
                table: "AttendanceRecords",
                columns: new[] { "EventSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_EventFeedback_RegistrationId",
                table: "EventFeedback",
                column: "RegistrationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Acronym",
                table: "Organizations",
                column: "Acronym",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Name",
                table: "Organizations",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizationId",
                table: "AspNetUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AspNetUsers_LastEditedByUserId",
                table: "Events",
                column: "LastEditedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Organizations_OrganizationId",
                table: "Events",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_AspNetUsers_LastEditedByUserId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Organizations_OrganizationId",
                table: "Events");

            migrationBuilder.DropTable(
                name: "EventFeedback");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Events_LastEditedByUserId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_OrganizationId_StartsAt",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EventSessionId_Status",
                table: "AttendanceRecords");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LateAfterMinutes",
                table: "EventSessions");

            migrationBuilder.DropColumn(
                name: "CollectsFeedback",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LastEditedByUserId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LastEditedOn",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<DateTime>(
                name: "TimeIn",
                table: "AttendanceRecords",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EventSessionId",
                table: "AttendanceRecords",
                column: "EventSessionId");
        }
    }
}
