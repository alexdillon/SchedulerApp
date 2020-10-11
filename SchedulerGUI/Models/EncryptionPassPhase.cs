﻿using System;
using SchedulerGUI.Enums;
using SchedulerGUI.Interfaces;

namespace SchedulerGUI.Models
{
    /// <summary>
    /// <see cref="EncryptionPassPhase"/> represents encryption phase of orbit.
    /// </summary>
    public class EncryptionPassPhase : IPassPhase
    {
        private const int MAXBYTES = 10000;

        private double maxEnergy;
        private double maxPower;

        /// <summary>
        /// Initializes a new instance of the <see cref="EncryptionPassPhase"/> class.
        /// </summary>
        /// <param name="startTime">Start of phase.</param>
        /// <param name="endTime">End of phase.</param>
        /// <param name="name">Name of phase.</param>
        /// <param name="maxEnergy">Max energy alloted for phase.</param>
        /// <param name="maxPower">Max power allotted for phase.</param>
        public EncryptionPassPhase(DateTime startTime, DateTime endTime, PhaseType name, double maxEnergy, double maxPower)
        {
            this.StartTime = startTime;
            this.EndTime = endTime;
            this.PhaseName = name;
            this.Duration = endTime - startTime;

            this.maxEnergy = maxEnergy;
            this.maxPower = maxPower;
        }

        /// <inheritdoc/>
        public TimeSpan Duration { get; set; }

        /// <inheritdoc/>
        public DateTime StartTime { get; set; }

        /// <inheritdoc/>
        public DateTime EndTime { get; set; }

        /// <inheritdoc/>
        public PhaseType PhaseName { get; set; }

        /// <inheritdoc/>
        public double TotalPower { get; set; }

        /// <inheritdoc/>
        public double TotalEnergy { get; set; }

        /// <summary>
        /// Gets or sets number of bytes to encrypt during phase.
        /// </summary>
        public int BytesToEncrypt { get; set; }

        /// <inheritdoc/>
        public void SetRandomValues(Random random)
        {
            this.TotalPower = random.NextDouble() * this.maxPower;
            this.TotalEnergy = random.NextDouble() * this.maxEnergy;
            this.BytesToEncrypt = random.Next(0, MAXBYTES);
        }
    }
}
