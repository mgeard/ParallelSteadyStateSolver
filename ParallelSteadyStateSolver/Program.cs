using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelSteadyStateSolver
{
    class Program
    {
        const int N = 100;
        static Random rand = new Random(42);

        static void Main(string[] args) //TODO: write code to get a proper average execution time
        {

            double[,] mchain =
            {
                {0.65, 0.15, 0.1},
                {0.25, 0.65, 0.4},
                {0.1,  0.2,  0.5},
            };

            MarkovChain m = new MarkovChain(mchain);
            Console.WriteLine(m);

            var solved = m.SteadyStateValues();
            foreach (var s in solved) Console.WriteLine($"pi_{s.Pi} = {s.Value}");
            
            var m2 = new MarkovChain(AllocateMatrix(N));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            var solved2 = m2.SteadyStateValues();
            //foreach (var s in solved) Console.WriteLine($"pi_{s.Pi} = {s.Value}");

            stopwatch.Stop();
            Console.WriteLine(stopwatch.ElapsedMilliseconds);

            Thread.Sleep(1000);

            Console.ReadLine();
        }

        //NOTE: not part of the algorithm but this may also be parallelisable
        static double[,] AllocateMatrix(int n, bool initialize = true)
        {
            var matrix = new double[n, n];
            if (initialize)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        matrix[i, j] = rand.NextDouble();
                    }
                }
            }
            return matrix;
        }
    }

    public class MarkovChain
    {
        private double[,] Matrix; //could possibly also flatten 2d arrays
        private SteadyStateEquation[] SteadyStateEquations; 
        private SteadyStateValue[] SolvedSteadyStateValues;
        private int Len;

        public MarkovChain(double[,] matrix)
        {
            Matrix = matrix;
            Len = matrix.GetLength(0);

            SteadyStateEquations = new SteadyStateEquation[Len];
            SolvedSteadyStateValues = new SteadyStateValue[Len];

            for (int i = 0; i < Len; i++)
            {
                double[] row = Enumerable.Range(0, Len) //NOTE: I could possibly use PLINQ here?
                    .Select(x => matrix[i, x])
                    .ToArray();

                SteadyStateEquations[i] = new SteadyStateEquation(i, row);
            }
        }

        public SteadyStateValue[] SteadyStateValues() //solve
        {
            //simplifcation can be done to make things simpler from the start
            foreach (SteadyStateEquation steadyStateEquation in SteadyStateEquations) 
                steadyStateEquation.Simplify();

            for (int i = 1; i < SteadyStateEquations.Length; i++)
            {
                Parallel.For(0, SteadyStateEquations.Length, (j) =>
                {
                    if (i != j) //NOTE: might even be faster to NOT have this line of code?
                        SteadyStateEquations[j].SubstituteEquation(SteadyStateEquations[i]);
                });
            }
            
            SolveAll(GetPi_0());

            return SolvedSteadyStateValues;
        }

        #region solving
        public double GetPi_0()
        {
            double sum = 1;

            //NOTE: potentially parallelisable using some tricky methods
            //break the iteration space into chunks, then sum those chunks, and add them together after
            for (int i = 1; i < SteadyStateEquations.Length; i++) 
                sum += SteadyStateEquations[i].SteadyStateValues.First().Value;

            return 1 / sum;
        }

        public void SolveAll(double pi_0Value)
        {
            SolvedSteadyStateValues[0] = new SteadyStateValue(0, pi_0Value);

            //NOTE: seems easily parallelisable
            for (int i = 1; i < Len; i++)
            {
                SteadyStateEquation equation = SteadyStateEquations[i];
                SolvedSteadyStateValues[equation.Equivalent] = new SteadyStateValue(equation.Equivalent, equation.SteadyStateValues.First().Value * pi_0Value);
            }
        }
        #endregion solving

        public override string ToString()
        {
            StringBuilder markovChainString = new StringBuilder();

            for (int i = 0; i < Len; i++)
            {
                for (int j = 0; j < Len; j++)
                    markovChainString.Append($"{Matrix[i, j]}  ");

                markovChainString.AppendLine();
            }

            return markovChainString.ToString();
        }
    }

    public class SteadyStateEquation
    {
        public int Equivalent { get; set; }
        public List<SteadyStateValue> SteadyStateValues { get; set; }

        public SteadyStateEquation(int equivalent, double[] values)
        {
            Equivalent = equivalent;

            SteadyStateValues = new List<SteadyStateValue>();

            //NOTE: parallelising here breaks the Simplify() method
            //Parallel.For(0, values.Length, (i) =>
            //{
            //    SteadyStateValues.Add(new SteadyStateValue(i, values[i]));
            //});
            for (int i = 0; i < values.Length; i++)
                SteadyStateValues.Add(new SteadyStateValue(i, values[i]));
        }

        #region substitution
        public void SubstituteEquation(SteadyStateEquation subEquation)
        {
            //NOTE: parallelism could possible by utilised here to find the required SteadyStateValue here,
            //but dealing with the unsuccessful threads will be a problem
            int substitutePi = subEquation.Equivalent;
            for (int i = SteadyStateValues.Count - 1; i >= 0; i--)
                if (SteadyStateValues[i].Pi == substitutePi)
                {
                    SubstituteValue(i, subEquation);
                    break;
                }
        }

        //NOTE: Utilising a hash table might make this algorithm way faster
        //TODO: figure out why the list of SteadyStateValues is not already a hash table
        private void SubstituteValue(int oldSteadyStateValueIndex, SteadyStateEquation SubEquation)
        {
            double multiplier = SteadyStateValues[oldSteadyStateValueIndex].Value;
            SteadyStateValues.RemoveAt(oldSteadyStateValueIndex);

            double compliment = 1; //1 - does not change value if new compliment is not found

            foreach (SteadyStateValue newSteadyStateValue in SubEquation.SteadyStateValues)
            {
                bool addedFlag = false; //was a value added in this iteration?
                int newPi = newSteadyStateValue.Pi; 
                double newVal = newSteadyStateValue.Value * multiplier;

                //NOTE: This NEEDS to be done with a hash table! 
                //Theta(1)!
                foreach (SteadyStateValue oldSteadyStateValue in SteadyStateValues) 
                {
                    if (newPi == oldSteadyStateValue.Pi)
                    {
                        oldSteadyStateValue.Value += newVal; //adds the new value to the old value
                        addedFlag = true;
                        break;
                    }
                }

                if (!addedFlag)
                    compliment = 1 - newVal;
            }

            //does the same thing as simplification
            //pi_k is already removed so it just scales the remaining values to the compliment
            for (int i = 0; i < SteadyStateValues.Count; i++) 
                SteadyStateValues[i].Value /= compliment;
        }

        public void Simplify()
        {
            double compliment = 1;

            for (int i = SteadyStateValues.Count - 1; i >= 0; i--)
                if (SteadyStateValues[i].Pi == Equivalent)
                {
                    compliment = 1 - SteadyStateValues[i].Value;
                    SteadyStateValues.RemoveAt(i);
                    break;
                }

            for (int i = 0; i < SteadyStateValues.Count; i++)
                SteadyStateValues[i].Value /= compliment;
        }
        #endregion substitution

    }

    public class SteadyStateValue
    {
        public int Pi { get; set; } //as an index
        public double Value { get; set; }

        public SteadyStateValue(int pi, double value)
        {
            Pi = pi;
            Value = value;
        }
    }
}
