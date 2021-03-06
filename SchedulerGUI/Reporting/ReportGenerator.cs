﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Wpf;
using SchedulerDatabase.Helpers;
using SchedulerDatabase.Models;
using SchedulerGUI.Enums;
using SchedulerGUI.Models;
using SchedulerGUI.Solver;
using SchedulerGUI.ViewModels.Controls;

namespace SchedulerGUI.Reporting
{
    /// <summary>
    /// <see cref="ReportGenerator"/> provides scheduling report generation capabilities.
    /// </summary>
    public class ReportGenerator
    {
        /// <summary>
        /// Generates a report to summarize all the entered and computed values for the scheduling process.
        /// </summary>
        /// <param name="passes">All orbital passes.</param>
        /// <param name="batterySpecs">The battery specifications used in the schedule.</param>
        /// <param name="solarSpecs">The solar panel specifications used in the schedule.</param>
        /// <param name="schedulerSolution">The computed schedule.</param>
        /// <returns>A <see cref="FlowDocument"/> report.</returns>
        public static FlowDocument GenerateReport(IEnumerable<PassOrbit> passes, Battery batterySpecs, SolarPanel solarSpecs, ScheduleSolution schedulerSolution)
        {
            var report = new FlowDocument()
            {
                PagePadding = new Thickness(100),
            };

            report.Blocks.Add(ReportTheme.MakeTitle("Scheduler App Report"));

            // Pass Data
            report.Blocks.Add(ReportTheme.MakeHeader1("Pass Configuration"));
            report.Blocks.Add(GeneratePassDataSection(passes));

            // Battery Capacity Graph
            var graphSection = new Section
            {
                BreakPageBefore = true,
            };
            graphSection.Blocks.Add(ReportTheme.MakeHeader1("Battery Utilization Graph"));
            graphSection.Blocks.Add(GenerateEnergyCapacityGraphExport(passes, batterySpecs));
            graphSection.Blocks.Add(ReportTheme.MakeHeader1("Power Specifications"));
            graphSection.Blocks.Add(GenerateEnergySourceSection(batterySpecs, solarSpecs));
            report.Blocks.Add(graphSection);

            // Scheduler results
            var resultsSection = new Section
            {
                BreakPageBefore = true,
            };
            resultsSection.Blocks.Add(ReportTheme.MakeHeader1("Computed Schedule"));
            resultsSection.Blocks.Add(ReportTheme.MakeHeader1("Info, Warnings, and Errors"));
            resultsSection.Blocks.Add(GenerateWarningsSection(schedulerSolution));
            resultsSection.Blocks.Add(GenerateOptimizedProfileSection(schedulerSolution, PhaseType.Encryption, "Optimized AES Profile: ", PhaseType.Datalink, "Optimized Compression Profile: "));
            report.Blocks.Add(resultsSection);

            // Scheduler results
            var aboutSection = new Section();
            resultsSection.Blocks.Add(ReportTheme.MakeHeader1("About Scheduler App"));
            report.Blocks.Add(new Paragraph()
            {
                Inlines =
                {
                    new Run($"Generated by UAH CPE656 (Fall 2020) Satellite Power Scheduler App\n"),
                    new Run($"Report Generated on {DateTime.Now:MM/dd/yyyy hh:mm:ss tt}\n"),
                    new Run($"Application build {GlobalAssemblyInfo.InformationalVersion}\n"),
                },
            });
            report.Blocks.Add(aboutSection);

            return report;
        }

        private static Block GeneratePassDataSection(IEnumerable<PassOrbit> passes)
        {
            var passDataSection = new Section();

            var passesList = new List();
            foreach (var pass in passes)
            {
                var passDescription = new Paragraph();
                passDescription.Inlines.Add(ReportTheme.GetSuccessIcon(pass.IsScheduledSuccessfully));
                passDescription.Inlines.Add(new Bold(new Run(pass.Name)));
                passDescription.Inlines.Add(new LineBreak());
                passDescription.Inlines.Add(new Run($"Starting Time: {pass.StartTime:MM/dd/yyyy hh:mm:ss tt}"));
                passDescription.Inlines.Add(new LineBreak());
                passDescription.Inlines.Add(new Run($"Ending Time: {pass.EndTime:MM/dd/yyyy hh:mm:ss tt}"));
                passDescription.Inlines.Add(new LineBreak());

                var phasesList = new List();
                foreach (var phase in pass.PassPhases)
                {
                    var phaseDescription = new Paragraph();
                    phaseDescription.Inlines.Add(new Run($"Phase: {phase.PhaseName}"));
                    phaseDescription.Inlines.Add(new LineBreak());
                    phaseDescription.Inlines.Add(new Run($"Energy Used: {MetricUtils.MetricValueAxisLabelFormatter(phase.TotalEnergyUsed, "J")}"));
                    phaseDescription.Inlines.Add(new LineBreak());
                    phaseDescription.Inlines.Add(new Run($"Start Time: {phase.StartTime:hh:mm:ss tt}"));
                    phaseDescription.Inlines.Add(new LineBreak());
                    phaseDescription.Inlines.Add(new Run($"End Time: {phase.EndTime:hh:mm:ss tt}"));
                    phaseDescription.Inlines.Add(new LineBreak());

                    if (phase is EncryptionPassPhase enc)
                    {
                        phaseDescription.Inlines.Add(new Run($"Total Data Encrypted: {enc.BytesToEncrypt:n}"));
                        phaseDescription.Inlines.Add(new LineBreak());
                    }

                    phasesList.ListItems.Add(new ListItem(phaseDescription));
                }

                var passListItem = new ListItem();
                passListItem.Blocks.Add(passDescription);
                passListItem.Blocks.Add(phasesList);

                passesList.ListItems.Add(passListItem);
            }

            passDataSection.Blocks.Add(passesList);

            return passDataSection;
        }

        private static Block GenerateEnergyCapacityGraphExport(IEnumerable<PassOrbit> passes, Battery battery)
        {
            var historyGraphVM = new HistoryGraphViewModel()
            {
                Battery = battery,
            };

            historyGraphVM.Passes = passes;

            historyGraphVM.PlotModel.TextColor = OxyColors.Black;
            historyGraphVM.PlotModel.TitleColor = OxyColors.Black;
            historyGraphVM.PlotModel.PlotAreaBorderColor = OxyColors.Black;

            var stream = new MemoryStream();
            var pngExporter = new PngExporter { Width = 800, Height = 400, Background = OxyColors.White };
            pngExporter.Export(historyGraphVM.PlotModel, stream);

            return new BlockUIContainer(new Image() { Source = Converters.ImageUtils.BytesToImageSource(stream.ToArray()) });
        }

        private static Block GenerateEnergySourceSection(Battery battery, SolarPanel solar)
        {
            var section = new Section();
            section.Blocks.Add(ReportTheme.MakeHeader2("Battery Specs"));

            var batterySource = new Paragraph();
            batterySource.Inlines.Add(new Bold(new Run("Battery Capacity: ")));
            batterySource.Inlines.Add($"{battery.CapacitymAh:n} mAh");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new Bold(new Run("Battery Capacity: ")));
            batterySource.Inlines.Add($"{battery.CapacityJ:n} J");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new Bold(new Run("Nominal Voltage: ")));
            batterySource.Inlines.Add($"{battery.Voltage:n} V");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new Bold(new Run("Derated Percentage: ")));
            batterySource.Inlines.Add($"{battery.DeratedPct:n} %");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new Bold(new Run("Effective Battery Capacity: ")));
            batterySource.Inlines.Add($"{battery.EffectiveCapacitymAh:n} mAh");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new Bold(new Run("Effective Battery Capacity: ")));
            batterySource.Inlines.Add($"{battery.EffectiveCapacityJ:n} J");
            batterySource.Inlines.Add(new LineBreak());

            batterySource.Inlines.Add(new LineBreak());
            batterySource.Inlines.Add(new LineBreak());

            section.Blocks.Add(batterySource);

            section.Blocks.Add(ReportTheme.MakeHeader2("Solar Specs"));

            var solarPerformance = new Paragraph();
            solarPerformance.Inlines.Add(new Bold(new Run("Panel Name: ")));
            solarPerformance.Inlines.Add($"{solar.Name}");
            solarPerformance.Inlines.Add(new LineBreak());

            solarPerformance.Inlines.Add(new Bold(new Run("Voltage: ")));
            solarPerformance.Inlines.Add($"{solar.Voltage} V");
            solarPerformance.Inlines.Add(new LineBreak());

            solarPerformance.Inlines.Add(new Bold(new Run("Current: ")));
            solarPerformance.Inlines.Add($"{solar.Current} A");
            solarPerformance.Inlines.Add(new LineBreak());

            solarPerformance.Inlines.Add(new Bold(new Run("Total Power: ")));
            solarPerformance.Inlines.Add($"{solar.PowerW} W");
            solarPerformance.Inlines.Add(new LineBreak());

            solarPerformance.Inlines.Add(new Bold(new Run("Derated Percentage: ")));
            solarPerformance.Inlines.Add($"{solar.DeratedPct:n} %");
            solarPerformance.Inlines.Add(new LineBreak());

            solarPerformance.Inlines.Add(new Bold(new Run("Effective Power: ")));
            solarPerformance.Inlines.Add($"{solar.EffectivePowerW} W");
            solarPerformance.Inlines.Add(new LineBreak());

            section.Blocks.Add(solarPerformance);

            return section;
        }

        private static Block GenerateWarningsSection(ScheduleSolution schedulerSolution)
        {
            var results = new Section();

            var infoIcon = new InlineUIContainer(new Image() { Source = (ImageSource)Application.Current.Resources["VS2017Icons.StatusInformation"], Width = 16, Height = 16 });
            var warningIcon = new InlineUIContainer(new Image() { Source = (ImageSource)Application.Current.Resources["VS2017Icons.StatusWarning"], Width = 16, Height = 16 });
            var failedIcon = new InlineUIContainer(new Image() { Source = (ImageSource)Application.Current.Resources["VS2017Icons.TestCoveringFailed"], Width = 16, Height = 16 });
            var criticalIcon = new InlineUIContainer(new Image() { Source = (ImageSource)Application.Current.Resources["VS2017Icons.TestCoveringFailed"], Width = 16, Height = 16 });

            var warningHeader = ReportTheme.MakeHeader2(string.Empty, new List<Inline>() { warningIcon, new Run("Warning Messages") });
            var errorHeader = ReportTheme.MakeHeader2(string.Empty, new List<Inline>() { failedIcon, new Run("Error Messages") });
            var fatalErrorHeader = ReportTheme.MakeHeader2(string.Empty, new List<Inline>() { criticalIcon, new Run("Critical Error Messages") });

            var warnings = schedulerSolution.Problems
                .Where(p => p.Level == ScheduleSolution.SchedulerProblem.SeverityLevel.Warning)
                .Select(x => new ListItem(new Paragraph(new Run(x.Message))));

            var errors = schedulerSolution.Problems
                .Where(p => p.Level == ScheduleSolution.SchedulerProblem.SeverityLevel.Error)
                .Select(x => new ListItem(new Paragraph(new Run(x.Message))));

            var fatal = schedulerSolution.Problems
                .Where(p => p.Level == ScheduleSolution.SchedulerProblem.SeverityLevel.Fatal)
                .Select(x => new ListItem(new Paragraph(new Run(x.Message))));

            var warningList = new List();
            var errorList = new List();
            var fatalList = new List();

            warningList.ListItems.AddRange(warnings);
            errorList.ListItems.AddRange(errors);
            fatalList.ListItems.AddRange(fatal);

            InsertEmptyListPlaceholder(warningList);
            InsertEmptyListPlaceholder(errorList);
            InsertEmptyListPlaceholder(fatalList);

            results.Blocks.Add(warningHeader);
            results.Blocks.Add(warningList);
            results.Blocks.Add(errorHeader);
            results.Blocks.Add(errorList);
            results.Blocks.Add(fatalErrorHeader);
            results.Blocks.Add(fatalList);

            return results;
        }

        private static Block GenerateOptimizedProfileSection(ScheduleSolution schedulerSolution, PhaseType typeOne, string typeOneText, PhaseType typeTwo, string typeTwoText)
        {
            var passesList = new List();
            foreach (var pair in schedulerSolution.ViableProfiles)
            {
                var pass = pair.Key;
                var profile = pair.Value;
                bool hasPhaseOne = profile.TryGetValue(typeOne, out IByteStreamProcessor _);
                bool hasPhaseTwo = profile.TryGetValue(typeTwo, out IByteStreamProcessor _);

                if (!hasPhaseOne && !hasPhaseTwo)
                {
                    continue;
                }

                var solutionDescription = new Paragraph();
                solutionDescription.Inlines.Add(ReportTheme.GetSuccessIcon(pass.IsScheduledSuccessfully));
                solutionDescription.Inlines.Add(new Bold(new Run($"{pass.Name}\n")));
                ListItem passSolutionItem;
                solutionDescription.Inlines.Add(new Bold(new Run(typeOneText)));
                if (hasPhaseOne)
                {
                    var deviceProfile = new Paragraph(new Run($"\nDevice: {profile[typeOne]?.FullProfileDescription}\n"))
                    {
                        Margin = new Thickness(20, 0, 0, 0),
                    };

                    passSolutionItem = new ListItem();
                    passSolutionItem.Blocks.Add(solutionDescription);
                    passSolutionItem.Blocks.Add(deviceProfile);
                }
                else
                {
                    solutionDescription.Inlines.Add(new Run("No viable solutions\n"));

                    passSolutionItem = new ListItem();
                    passSolutionItem.Blocks.Add(solutionDescription);
                }

                solutionDescription.Inlines.Add(new Bold(new Run(typeTwoText)));
                if (hasPhaseTwo)
                {
                    var deviceProfile = new Paragraph(new Run($"Device: {profile[typeTwo]?.FullProfileDescription}\n"))
                    {
                        Margin = new Thickness(20, 0, 0, 0),
                    };

                    passSolutionItem = new ListItem();
                    passSolutionItem.Blocks.Add(solutionDescription);
                    passSolutionItem.Blocks.Add(deviceProfile);
                }
                else
                {
                    solutionDescription.Inlines.Add(new Run("No viable solutions\n"));

                    passSolutionItem = new ListItem();
                    passSolutionItem.Blocks.Add(solutionDescription);
                }

                passesList.ListItems.Add(passSolutionItem);
            }

            return passesList;
        }

        private static void InsertEmptyListPlaceholder(List list)
        {
            if (list.ListItems.Count == 0)
            {
                list.ListItems.Add(new ListItem(new Paragraph(new Italic(new Run("(None)")))));
            }
        }
    }
}
