using System;
using System.Collections.Generic;
using System.Linq;

namespace decaf
{
    internal class Simulation : DECAF
    {
        #region JSON Initialized variables

        public Dictionary<String, Node> components;
        public Boolean debug;
        public double[,] environments;

        #endregion

        #region All other variables

        public int Length;
        protected int MaxEnvironment;
        protected Dictionary<string, byte> MaxVector;
        protected List<State> StateList;
        protected Dictionary<State, ushort> StateMap;
        protected List<String> TypeList;
        private Boolean _init;

        public Boolean Initialized
        {
            get { return _init; }
            set { _init = value; }
        }

        #endregion

        #region Main methods

        public void Setup()
        {
            #region Integrity Check

            // Invalid usage of DECAF, notify developers
            if (!_init)
            {
                throw new Exception("Please intialize Simulation through ConfigParser.Parse before calling Initialize()");
            }

            // Assert environments variable is defined
            if (environments == null || environments.LongLength == 0)
            {
                throw new Exception("Env-change matrix not found or empty in configuration");
            }

            // Assert env-change matrix is a square
            if (Math.Sqrt(environments.LongLength)%1 == 0)
            {
                throw new Exception("Env-change matrix should be a square matrix");
            }

            // Assert env-change matrix diagonal consists of zeros
            for (int i = 0; i < environments.GetLength(0); i++)
            {
                if (environments[i, i] != 0)
                {
                    throw new Exception("Env-change matrix diagonal should only be ZERO");
                }
            }

            // Assert components variable is defined
            if (components == null || components.Keys.Count == 0)
            {
                throw new Exception("Components object not found or empty in configuration");
            }

            // Per-component integrity checks
            foreach (var sNkvp in components)
            {
                // Assert equal number of failure rates as environments
                if (sNkvp.Value.failure.Length != environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".failure rates should be defined for each Environment.");
                }
                // Assert equal number of repair rates as environments
                if (sNkvp.Value.repair.Length != environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".repair rates should be defined for each Environment.");
                }
                // Assert all cascading components are valid components
                foreach (var sDkvp in sNkvp.Value.cascading)
                {
                    if (!components.ContainsKey(sDkvp.Key))
                    {
                        throw new Exception("Comp:" + sNkvp.Key + ".cascading:" + sDkvp.Key +
                                            " is an invalid cascading component.");
                    }
                }
            }

            // TypeList, StateList and StateMap should not be defined at this point - scare off hackers
            if (!(TypeList == null && StateList == null && StateMap == null))
            {
                throw new Exception("Hack attempt!");
            }

            #endregion

            MaxEnvironment = environments.Length;
        }

        public void GenerateStates()
        {
            var initialState = new List<byte>();
            TypeList = new List<String>();
            StateList = new List<State>();
            StateMap = new Dictionary<State, ushort>();
            foreach (var kvp in components)
            {
                TypeList.Add(kvp.Key);
            }
            Haircomb(0, initialState);
            MaxVector = StateList.Last().Vector;
            Length = StateList.Count;
        }

        public void BuildQMatrix()
        {
            throw new NotImplementedException();
        }

        public double CalculateMttf()
        {
            throw new NotImplementedException();
        }

        public double CalculateSsu()
        {
            throw new NotImplementedException();
        }

        private void Haircomb(int index, IReadOnlyList<byte> current)
        {
            if (current == null) throw new ArgumentNullException("current");

            // If the Vector is the correct Length, then add to StateMap and StateList
            if (current.Count == components.Count)
            {
                for (int i = 0; i < environments.GetLength(0); i++)
                {
                    var temp = new State(i);
                    for (int j = 0; j < current.Count; j++)
                    {
                        temp.Vector.Add(TypeList[j], current[j]);
                    }
                    StateMap.Add(temp, (ushort) StateList.Count);
                    StateList.Add(temp);
                }
            }

            // If the Index exceeds the number of components, break out
            if (components.Count <= index)
            {
                return;
            }

            // Loop through the required number of redundancies for the Index-th component
            for (int i = 0; i <= components[TypeList[index]].redundancy; i++)
            {
                // Create new Vector and push to next recursion
                var newCurrent = new List<byte>(current) {(byte) i};
                Haircomb(index + 1, newCurrent);
            }
        }

        #endregion
    }
}