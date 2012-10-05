using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;

namespace decaf
{
    public static class Simulation
    {
        #region Generic parameters

        public static double[,] Environments;
        public static double[,] QMatrix;
        public static int Length;
        public static int MaxEnvironment;
        public static Dictionary<string, byte> MaxVector;
        public static Dictionary<State, ushort> StateMap;
        public static Dictionary<String, Node> Components;
        public static List<State> StateList;
        public static List<String> TypeList;
        public static Stopwatch Timer;
        #endregion

        #region Statistics
        public static ulong TimeToStates = 0; /* Generating all combinations of states */
        public static ulong TimeToTreeGen = 0; /* Filling failure transitions with trees */
        public static ulong TimeToQMatrix = 0; /* Filling repair and env-change in QMatrix */
        public static ulong TimeToMTTF = 0; /* Calculating mean time to failure */
        public static ulong TimeToSSU = 0; /* Calculating steady-state unavailability */
        
        #endregion

        #region Internal Variables
        private static Dictionary<String, List<String>> _binaryEcache = new Dictionary<String, List<String>>(); 
        #endregion

        #region Main methods

        public static void Setup(Json intialJson)
        {
            #region Integrity Check

            // Assert environments variable is defined
            if (intialJson.environments == null || intialJson.environments.LongLength == 0)
            {
                throw new Exception("Env-change matrix not found or empty in configuration");
            }

            // Assert env-change matrix is a square
            if (Math.Sqrt(intialJson.environments.LongLength) % 1 > 0)
            {
                throw new Exception("Env-change matrix should be a square matrix");
            }

            // Assert env-change matrix diagonal consists of zeros
            for (var i = 0; i < intialJson.environments.GetLength(0); i++)
            {
                if (Math.Abs(intialJson.environments[i, i]) > 0)
                {
                    throw new Exception("Env-change matrix diagonal should only be ZERO");
                }
            }

            // Assert components variable is defined
            if (intialJson.components == null || intialJson.components.Keys.Count == 0)
            {
                throw new Exception("Components object not found or empty in configuration");
            }

            // Per-component integrity checks
            foreach (var sNkvp in intialJson.components)
            {
                // Assert equal number of failure rates as environments
                if (sNkvp.Value.Failure.Length != intialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".failure rates should be defined for each Environment.");
                }
                // Assert equal number of repair rates as environments
                if (sNkvp.Value.Repair.Length != intialJson.environments.GetLength(0))
                {
                    throw new Exception("Comp:" + sNkvp.Key + ".repair rates should be defined for each Environment.");
                }
                // Assert all cascading components are valid components
                foreach (var sDkvp in sNkvp.Value.Cascading)
                {
                    if (!intialJson.components.ContainsKey(sDkvp.Key))
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

            Environments = intialJson.environments;
            Components = intialJson.components;
            MaxEnvironment = Environments.GetLength(0);
        }

        public static void Run()
        {
            GenerateStates();

            BuildQMatrix();
        }

        public static void GenerateStates()
        {
            if (Components.Count < 1)
            {
                throw new InvalidOperationException("Please run Setup before GenerateStates");
            }
            Timer.Start();

            var initialState = new List<byte>();
            TypeList = new List<String>();
            StateList = new List<State>();
            StateMap = new Dictionary<State, ushort>();
            foreach (var kvp in Components)
            {
                TypeList.Add(kvp.Key);
            }
            Haircomb(0, initialState);
            MaxVector = StateList.Last().Vector;
            Length = StateList.Count;
            QMatrix = new double[Length, Length];

            Timer.Stop();
            TimeToStates = (ulong)Timer.ElapsedMilliseconds;
        }

        public static void BuildQMatrix()
        {
            if (Length < 1 || StateMap.Count < 1 || TypeList.Count < 1)
            {
                throw new InvalidOperationException("Please run GenerateStates before BuildQMatrix");
            }
            Timer.Start();

            // Fill Env-change rates
            for (int t = 0; t < Length; t += MaxEnvironment)
            {
                for (int i = 0; i < MaxEnvironment; i++)
                {
                    for (int j = 0; j < MaxEnvironment; j++)
                    {
                        QMatrix[t + i, t + j] = Environments[i, j];
                    }
                }
            }

            // Fill Repair rates
            var validRepairs = new State[TypeList.Count];
            for( var i = 0; i < TypeList.Count; i++ )
            {
                var temp = new State(0);
                temp.Vector.Add(TypeList[i], 1);
                validRepairs[i] = temp;
            }
            for( var i = Length - 1; i >= 0; i-- )
            {
                foreach (var t in validRepairs)
                {
                    t.Environment = StateList[i].Environment;
                    var fromState = StateList[i] + t;
                    if (StateMap.ContainsKey(fromState))
                    {
                        QMatrix[StateMap[fromState], i] = t * fromState;
                    }
                }
            }
            GenerateTrees();
            Timer.Stop();
            TimeToQMatrix = (ulong)Timer.ElapsedMilliseconds;

            // Print QMatrix - Debug
            var result = "";
            for (var i = 0; i < Length; i++)
            {
                for (var j = 0; j < Length; j++)
                {
                    result += QMatrix[i, j] + "\t";
                }
                result += "\n";
            }
            MessageBox.Show(result);
        }

        private static void GenerateTrees()
        {
            Timer.Start();
            foreach (var type in TypeList)
            {
                _binaryEcache[type] = PowerSet(Components[type].Cascading);
            }
            foreach(var type in TypeList)
            {
                var levels = new List<List<string>> { new List<string> {"1:" + type} };

                var initialFt = new State(0);
                initialFt.Vector.Add(type, 1);

                var bfhMap = TypeList.ToDictionary(k => k, k => new List<string>());
                bfhMap[type].Add("|");

                RecurseChildren(levels, initialFt, 1.0, bfhMap);
            }
            Timer.Stop();
            TimeToTreeGen = (ulong)Timer.ElapsedMilliseconds;
        }

        private static List<string> PowerSet(Dictionary<string, double> cascading)
        {
            var result = new List<string>();
            if( cascading == null || cascading.Count == 0 )
            {
                return result;
            }
            for( var i = 0; i < Math.Pow(2, cascading.Count); i++ )
            {
                var binary = String.Join("", BitConverter.GetBytes(i)).PadLeft(cascading.Count);
                var block = new StringBuilder();
                foreach (var t in binary)
                {
                    block.Append(t).Append(":").Append(cascading.Count).Append(",");
                }
                result.Add(block.ToString());
            }
            return result;
        }

        private static void RecurseChildren(List<List<string>> levels, State initialFt, double d, Dictionary<string, List<string>> bfhMap)
        {

        }

        public static double CalculateMttf()
        {
            throw new NotImplementedException();
        }

        public static double CalculateSsu()
        {
            throw new NotImplementedException();
        }

        private static void Haircomb(int index, IReadOnlyList<byte> current)
        {
            if (current == null)
            {
                throw new ArgumentNullException("current");
            }

            // If the Vector is the correct Length, then add to StateMap and StateList
            if (current.Count == Components.Count)
            {
                for (var i = 0; i < MaxEnvironment; i++)
                {
                    var temp = new State(i);
                    for (var j = 0; j < current.Count; j++)
                    {
                        temp.Vector.Add(TypeList[j], current[j]);
                    }
                    StateMap.Add(temp, (ushort) StateList.Count);
                    StateList.Add(temp);
                }
            }

            // If the Index exceeds the number of components, break out
            if (index >= Components.Count)
            {
                return;
            }

            // Loop through the required number of redundancies for the Index-th component
            for (var i = 0; i <= Components[TypeList[index]].Redundancy; i++)
            {
                // Create new Vector and push to next recursion
                var newCurrent = new List<byte>(current) {(byte) i};
                Haircomb(index + 1, newCurrent);
            }
        }

        #endregion
    }
}