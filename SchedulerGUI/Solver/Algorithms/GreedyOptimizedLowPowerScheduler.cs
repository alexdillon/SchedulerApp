﻿using System.Collections.Generic;
using System.Linq;
using SchedulerDatabase.Models;
using SchedulerGUI.Enums;
using SchedulerGUI.Interfaces;
using SchedulerGUI.Models;

namespace SchedulerGUI.Solver.Algorithms
{
    /// <summary>
    /// <see cref="GreedyOptimizedLowPowerScheduler"/> solves the scheduling problem using a greedy algorithm
    /// to always pick the lowest power device for each pass that can compute the AES encryption problem within the
    /// allowed time window. If a solution can be found, it will be globally optimized to use the minimal amount of power,
    /// however, it is NOT guaranteed to be optimized to be the fastest. Alternative profiles may be possible to allow
    /// for solving the problem with the the same or greater level of energy (but NOT less), but at a faster rate.
    /// </summary>
    public class GreedyOptimizedLowPowerScheduler : IScheduleSolver
    {
        private ScheduleSolution solution;
        private double currentCapacityJoules;

        /// <inheritdoc/>
        public string Name => "Low Power Optimized";

        /// <inheritdoc/>
        public object Tag { get; set; }

        /// <inheritdoc/>
        public ScheduleSolution Solve(IEnumerable<PassOrbit> passes, IEnumerable<IByteStreamProcessor> summarizedEncryptors, IEnumerable<IByteStreamProcessor> summarizedCompressors, Battery battery)
        {
            this.InitSolution(passes);

            var optimizedAES = this.BuildOptimizationMap(summarizedEncryptors);
            var optimizedCompression = this.BuildOptimizationMap(summarizedCompressors);

            this.currentCapacityJoules = 0;
            foreach (var pass in passes)
            {
                // Encryption and Datalink require special treatment - find them first for exacting key parameters.
                var encryptionPhase = pass.PassPhases.First(p => p.PhaseName == PhaseType.Encryption) as EncryptionPassPhase;
                var downlinkPhase = pass.PassPhases.First(p => p.PhaseName == PhaseType.Datalink);

                var succeededPhasesInPass = 0;

                // Run through every phase to check the cumulative energy status
                foreach (var phase in pass.PassPhases.OrderBy(p => p.StartTime))
                {
                    if (phase.PhaseName == PhaseType.Encryption)
                    {
                        // Handle encryption - Compute valid profile if possible, set energy, and apply
                        var foundViableAESProfile = this.FindProfileForPhase(optimizedAES, pass, encryptionPhase, encryptionPhase.BytesToEncrypt);
                        if (!foundViableAESProfile)
                        {
                            this.ReportProfileNotAvailable(pass, "AES encryption");
                        }
                        else
                        {
                            // Indicate that encryption was successful.
                            succeededPhasesInPass += 1;
                        }
                    }
                    else if (phase.PhaseName == PhaseType.Datalink)
                    {
                        // Handle datalink - Compute valid profile if possible, set energy, and apply
                        // Use the number of bytes from the encryption phase as the input size to the compressor.
                        var foundViableCompressionProfile = this.FindProfileForPhase(optimizedCompression, pass, downlinkPhase, encryptionPhase.BytesToEncrypt);
                        if (!foundViableCompressionProfile)
                        {
                            this.ReportProfileNotAvailable(pass, "data compression");
                        }
                        else
                        {
                            // Indicate that compression was successful.
                            succeededPhasesInPass += 1;
                        }
                    }
                    else
                    {
                        // Regular phase - just use the listed energy values and move along.
                        this.currentCapacityJoules -= phase.TotalEnergyUsed;
                        var success = this.EnforceBatteryLimits(battery, pass, phase);
                        if (success)
                        {
                            // Indicate that compression was successful.
                            succeededPhasesInPass += 1;
                        }
                    }
                }

                // Only mark the pass successful if each phase was done successfully
                pass.IsScheduledSuccessfully = succeededPhasesInPass == pass.PassPhases.Count;
            }

            return this.solution;
        }

        private void InitSolution(IEnumerable<PassOrbit> passes)
        {
            this.solution = new ScheduleSolution()
            {
                IsSolvable = true, // Start out assuming it is solvable unless we encounter a problem.
                ViableProfiles = new Dictionary<PassOrbit, Dictionary<PhaseType, IByteStreamProcessor>>(),
                Problems = new List<ScheduleSolution.SchedulerProblem>(),
            };

            // Zero out all the energies used before calculation.
            // That way, if the scheduler fails, the only values that will be set
            // are the passes that were completed successfully.
            foreach (var pass in passes)
            {
                pass.PassPhases.First(p => p.PhaseName == PhaseType.Encryption).TotalEnergyUsed = 0;
                pass.PassPhases.First(p => p.PhaseName == PhaseType.Datalink).TotalEnergyUsed = 0;
                pass.IsScheduledSuccessfully = false;

                this.solution.ViableProfiles[pass] = new Dictionary<PhaseType, IByteStreamProcessor>();
            }
        }

        /// <summary>
        /// Builds a dictionary of Byte Processor Profiles sorted according to energy efficency.
        /// </summary>
        /// <param name="summarizedProfiles">The profiles that need to be ordered.</param>
        /// <returns>A dictionary sorted by energy efficiency as the key, most efficient first.</returns>
        private SortedDictionary<double, IByteStreamProcessor> BuildOptimizationMap(IEnumerable<IByteStreamProcessor> summarizedProfiles)
        {
            var optimizationMap = new SortedDictionary<double, IByteStreamProcessor>();

            // Characterize each profile based on how low-power it can be
            foreach (var profile in summarizedProfiles)
            {
                if (optimizationMap.ContainsKey(profile.JoulesPerByte))
                {
                    var existingProfile = optimizationMap[profile.JoulesPerByte];
                    var fasterProfile = existingProfile.BytesPerSecond > profile.BytesPerSecond ? existingProfile : profile;
                    optimizationMap[profile.JoulesPerByte] = fasterProfile;

                    var fasterNumber = (fasterProfile == existingProfile) ? "1" : "2";

                    this.solution.Problems.Add(new ScheduleSolution.SchedulerProblem(
                        ScheduleSolution.SchedulerProblem.SeverityLevel.Warning,
                        $"Two profiles appear to offer identical power efficiency. Profile 1: {existingProfile.ShortProfileClassDescription}, Profile 2: {profile.ShortProfileClassDescription}. Selecting the faster one: Profile {fasterNumber}"));
                }
                else
                {
                    optimizationMap.Add(profile.JoulesPerByte, profile);
                }
            }

            return optimizationMap;
        }

        private bool FindProfileForPhase(SortedDictionary<double, IByteStreamProcessor> optimizationMap, PassOrbit pass, IPassPhase phase, long bytes)
        {
            var allowedTime = phase.EndTime.Subtract(phase.StartTime);

            // For the encryption phase, we have an abs max of currentCapacityJoules
            // and the time allocated to encryptionPhase.

            // Start with the most ideal profile and keep testing until we find one that fits
            var foundViableProfile = false;
            foreach (var profile in optimizationMap.Values)
            {
                var timeRequired = bytes / profile.BytesPerSecond;
                var energyRequired = bytes * profile.JoulesPerByte;

                if (timeRequired > allowedTime.TotalSeconds)
                {
                    // Out of time
                    foundViableProfile = false;
                }
                else if (energyRequired > this.currentCapacityJoules)
                {
                    // Out of power
                    foundViableProfile = false;
                }
                else
                {
                    // This solution works for this part
                    this.solution.ViableProfiles[pass][phase.PhaseName] = profile;
                    phase.TotalEnergyUsed = energyRequired;
                    foundViableProfile = true;
                    this.currentCapacityJoules -= energyRequired;
                    break;
                }
            }

            return foundViableProfile;
        }

        /// <summary>
        /// Applies a bounding function to the current capacity of the battery, to prohibit discharging below 0 joules,
        /// and the prevent charging above the maximum specified capabilities. If the battery exceeds the specification,
        /// a warning (for overcharge) or critical error (for discharged) will be logged to the solution.
        /// </summary>
        /// <param name="battery">The battery parameters to enforce.</param>
        /// <param name="pass">The last pass that was executed.</param>
        /// <returns>A value indicating whether the requested capacity could be accomplished successfully.</returns>
        private bool EnforceBatteryLimits(Battery battery, PassOrbit pass, IPassPhase phase)
        {
            var success = true;

            if (this.currentCapacityJoules < 0)
            {
                // Orbit parameters are impossible - we've run out of power.
                // Error is fatal - denote the problem and abort scheduling.
                this.solution.IsSolvable = false;
                this.solution.Problems.Add(new ScheduleSolution.SchedulerProblem(
                    ScheduleSolution.SchedulerProblem.SeverityLevel.Fatal,
                    $"Orbit parameters for {pass.Name} are impossible. No power remains while scheduling the {phase.PhaseName} for {pass.Name}."));

                this.currentCapacityJoules = 0;

                // Discharging completely is a fatal error that will prevent the pass from completing
                success = false;
            }
            else if (this.currentCapacityJoules > battery.EffectiveCapacityJ)
            {
                this.solution.Problems.Add(new ScheduleSolution.SchedulerProblem(
                    ScheduleSolution.SchedulerProblem.SeverityLevel.Warning,
                    $"The battery was a contraint during {pass.Name}. {this.currentCapacityJoules:n} J were available, but max charge capacity is {battery.EffectiveCapacityJ:n} J"));

                // Apply the cap.
                this.currentCapacityJoules = battery.EffectiveCapacityJ;

                // Overcharging doesn't prevent the mission from continuing, but indicates inefficiency
                success = true;
            }

            return success;
        }

        private void ReportProfileNotAvailable(PassOrbit pass, string operation)
        {
            // No profile could be found that fits the available power and time
            // All previous schedules have already been done with the lowest-power option that fits
            // so if we're out of power, there is no solution possible.
            // If we're out of time, faster devices are needed, or the phase needs lengthened.
            this.solution.IsSolvable = false;
            this.solution.Problems.Add(new ScheduleSolution.SchedulerProblem(
                      ScheduleSolution.SchedulerProblem.SeverityLevel.Fatal,
                      $"Orbit parameters for {pass.Name} are impossible. No devices are capable of performing the {operation} in the required power and time window."));
        }
    }
}
