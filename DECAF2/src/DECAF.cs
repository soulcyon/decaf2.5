using System;
using System.Diagnostics;

namespace decaf
{
    internal class DECAF
    {
        // Optional statistical variables
        private readonly Simulation _sim;
        protected uint NumberOfAvoidedTrees = 0; /* Number of trees not generated explicitly */
        protected uint NumberOfTransitions = 0; /* Number of failure transitions filled in the QMatrix */
        protected uint NumberOfTrees = 0; /* Number of all trees - identical to the value in Srini's code */
        protected uint NumberOfUniqueTrees = 0; /* Number of trees uniquely generated */
        protected ulong TimeToInit = 0; /* Loading JSON and initializing StateList, nodeMap etc... */
        protected ulong TimeToMTTF = 0; /* Calculating mean time to failure */
        protected ulong TimeToQMatrix = 0; /* Filling repair and env-change in QMatrix */
        protected ulong TimeToSSU = 0; /* Calculating steady-state unavailability */
        protected ulong TimeToStates = 0; /* Generating all combinations of states */
        protected ulong TimeToTreeGen = 0; /* Filling failure transitions with trees */
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

            // timeToInit
            Timer.Start();
            _sim = ConfigParser.Parse(input);
            Timer.Stop();
            TimeToInit = (ulong) Timer.ElapsedMilliseconds;

            // timeToStates
            Timer.Start();
            _sim.GenerateStates();
            Timer.Stop();
            TimeToStates = (ulong) Timer.ElapsedMilliseconds;

            // timeToQMatrix
            Timer.Start();
            _sim.BuildQMatrix();
            Timer.Stop();
            TimeToQMatrix = (ulong) Timer.ElapsedMilliseconds;
        }

        public double MTTF
        {
            get { return _sim.CalculateMttf(); }
        }

        public double SSU
        {
            get { return _sim.CalculateSsu(); }
        }

        public double FindMeanTimeToFailure()
        {
            return 0.0;
        }

        public double FindSteadyStateUnavailability()
        {
            return 0.0;
        }
    }
}