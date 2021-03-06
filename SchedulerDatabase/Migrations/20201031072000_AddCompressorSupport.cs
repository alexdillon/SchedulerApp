﻿// <auto-generated/>

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SchedulerDatabase.Migrations
{
    public partial class AddCompressorSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompressorProfiles",
                columns: table => new
                {
                    ProfileId = table.Column<Guid>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    Author = table.Column<string>(nullable: true),
                    PlatformName = table.Column<string>(nullable: true),
                    AdditionalUniqueInfo = table.Column<string>(nullable: true),
                    TestedCompressionMode = table.Column<int>(nullable: false),
                    TotalTestedByteSize = table.Column<long>(nullable: false),
                    TotalTestedEnergyJoules = table.Column<double>(nullable: false),
                    TotalTestTime = table.Column<TimeSpan>(nullable: false),
                    TestedFrequency = table.Column<int>(nullable: false),
                    AverageVoltage = table.Column<double>(nullable: false),
                    AverageCurrent = table.Column<double>(nullable: false),
                    NumCores = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompressorProfiles", x => x.ProfileId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompressorProfiles");
        }
    }
}
