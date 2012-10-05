using System;
using System.Diagnostics;

namespace decaf
{
    public class DECAF
    {
        // Optional statistical variables
        protected uint NumberOfAvoidedTrees = 0; /* Number of trees not generated explicitly */
        protected uint NumberOfTransitions = 0; /* Number of failure transitions filled in the QMatrix */
        protected uint NumberOfTrees = 0; /* Number of all trees - identical to the value in Srini's code */
        protected uint NumberOfUniqueTrees = 0; /* Number of trees uniquely generated */
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
        }

        public double MTTF
        {
            get { return Simulation.CalculateMttf(); }
        }

        public double SSU
        {
            get { return Simulation.CalculateSsu(); }
        }
    }
}