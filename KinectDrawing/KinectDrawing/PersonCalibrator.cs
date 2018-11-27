using System;
using System.Collections.Generic;

namespace KinectDrawing
{
    internal class PersonCalibrator
    {
        private readonly List<double> distances = new List<double>();

        public PersonCalibrator()
        {
        }

        internal void AddDistanceFromCamera(double distance)
        {
            this.distances.Add(distance);
        }

        /// <summary>
        /// Calculate the median of the distances seen so far.
        /// Complexity: O(n*log(n)), could be improved to O(n)
        /// </summary>
        /// <returns></returns>
        internal double CalculateMedianDistance()
        {
            this.distances.Sort();
            int medianIndex = this.distances.Count / 2;
            return this.distances[medianIndex];
        }
    }
}