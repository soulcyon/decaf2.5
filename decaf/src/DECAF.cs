using System;
using System.Diagnostics;

namespace decaf
{
    public class DECAF
    {
        // Optional statistical variables
        protected ulong TimeToInit = 0; /* Loading JSON and initializing StateList, nodeMap etc... */
        protected Stopwatch Timer = new Stopwatch();

        // Default file: data/input.json and no debug
        public DECAF() : this("../../data/input.json", false)
        {
        }

        // Default no debug
        public DECAF(String input) : this(input, false)
        {
        }

        // Default file: data/input.json
        public DECAF(bool debugflag) : this("../../data/input.json", debugflag)
        {
        }

        // Constructor
        public DECAF(String input, bool debugFlag)
        {
            if (!debugFlag) return;
            if (input == null) throw new ArgumentNullException("input");

            ConfigParser.Parse(input);
            Simulation.Run();
        }

        public double MTTF
        {
            get { return Simulation.CalculateMttf(); }
        }

        public double SSU
        {
            get { return Simulation.CalculateSsu(); }
        }

        internal void GenerateStatistics()
        {
            Console.WriteLine("\nQMatrix (EnvChange):                " + Simulation.TimeToEnvChange);
            Console.WriteLine("QMatrix (Repair):                   " + Simulation.TimeToRepair);
            Console.WriteLine("QMatrix (Failure+Trees):            " + Simulation.TimeToFail);
            Console.WriteLine("\nTotal Processing Time:              " + Simulation.TimeToQMatrix);
            Console.WriteLine("Total Generated Trees:              " + Simulation.NumberOfTrees);
            Console.WriteLine("Total States:                       " + Simulation.Length);
            Console.WriteLine("\nMTTF:                               " + MTTF);
            Console.WriteLine("Time to compute MTTF:               " + Simulation.TimeToMTTF);
            Console.WriteLine("\nSSU:                                " + SSU);
            Console.WriteLine("Time to compute SSU:                " + Simulation.TimeToSSU);
        }
    }
}