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
        const int N_TRIALS = 100;
        const int MAX_MATRIX_SIZE = 100;

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

            //RecordExecutionTimes();

            Thread.Sleep(1000);

            Console.ReadLine();
        }

        public static void RecordExecutionTimes()
        {
            StringBuilder results = new StringBuilder();
            Stopwatch stopwatch = new Stopwatch();


            for (int i = 0; i < N_TRIALS; i++)
            {
                Console.WriteLine(i);
                for (int j = 0; j <= MAX_MATRIX_SIZE; j++)
                {
                    var m = new MarkovChain(AllocateMatrix(N));

                    stopwatch.Start();
                    m.SteadyStateValues();
                    stopwatch.Stop();

                    results.Append($"{stopwatch.ElapsedTicks}, ");

                    stopwatch.Reset();

                }

                results.AppendLine("");

            }

            results.Write("BruteForceMedianExecutionTimeResults.csv");
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
        private double[,] Matrix; //could possibly also flatten 2d arrays - (this matrix is not used - maybe get rid of it)
        private SteadyStateEquation[] SteadyStateEquations; 
        private SteadyStateValue[] SolvedSteadyStateValues;
        private int Len;

        public MarkovChain(double[,] matrix)
        {
            Matrix = matrix;
            Len = matrix.GetLength(0);

            SteadyStateEquations = new SteadyStateEquation[Len];
            SolvedSteadyStateValues = new SteadyStateValue[Len];

            //NOTE: Parallelising the i loop breaks the code SOMETIMES
            //^ need to ensure that pi_0 in in index 0

            for (int i = 0; i < Len; i++) //NOTE: is this loop also parallelisable? Find out!
            {
                //NOTE: adding AsParallel() breaks the code.
                double[] row = Enumerable.Range(0, Len).AsParallel().AsOrdered()
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

            //i loop cannot be parallelised
            //actually... there may be some sneaky stuff I could do here...
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
            //double sum = 1;

            ////NOTE: potentially parallelisable using some tricky methods
            ////break the iteration space into chunks, then sum those chunks, and add them together after
            //for (int i = 1; i < SteadyStateEquations.Length; i++)
            //    sum += SteadyStateEquations[i].SteadyStateValues.First().Value;

            SteadyStateEquations[0].SteadyStateValues.Add(new SteadyStateValue(0, 1)); //ghetto solution - perhaps change later

            //pretty sure this works
            double sum = SteadyStateEquations.AsParallel().Sum(x =>
            {
                return x.SteadyStateValues.First().Value;
            });

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
        //private void SubstituteValue(int oldSteadyStateValueIndex, SteadyStateEquation SubEquation)
        //{
        //    double multiplier = SteadyStateValues[oldSteadyStateValueIndex].Value;
        //    SteadyStateValues.RemoveAt(oldSteadyStateValueIndex);

        //    double compliment = 1; //1 - does not change value if new compliment is not found

        //    var substitutionValues = SubEquation.SteadyStateValues;
        //    Parallel.For(0, SubEquation.SteadyStateValues.Count, (i) =>
        //    {
        //        bool addedFlag = false; //was a value added in this iteration?
        //        int newPi = substitutionValues[i].Pi;
        //        double newVal = substitutionValues[i].Value * multiplier;

        //        //Parallel.For(0, SteadyStateValues.Count, (j) =>
        //        //  {
        //        //    //SteadyStateValue oldSteadyStateValue = SteadyStateValues[j];
        //        //    if (newPi == SteadyStateValues[j].Pi)
        //        //      {
        //        //          SteadyStateValues[j].Value += newVal; //adds the new value to the old value
        //        //        addedFlag = true;
        //        //      }
        //        //  });

        //        //Parallelising this loop results in a massive performance loss
        //        foreach (SteadyStateValue oldSteadyStateValue in SteadyStateValues)
        //        {
        //            if (newPi == oldSteadyStateValue.Pi)
        //            {
        //                oldSteadyStateValue.Value += newVal; //adds the new value to the old value
        //                addedFlag = true;
        //                break;
        //            }
        //        }

        //        if (!addedFlag)
        //            compliment = 1 - newVal;
        //    });
        //    for (int i = 0; i < SteadyStateValues.Count; i++)
        //        SteadyStateValues[i].Value /= compliment;
        //}

        //NOTE: Utilising a hash table might make this algorithm way faster
        //TODO: figure out why the list of SteadyStateValues is not already a hash table
        private void SubstituteValue(int oldSteadyStateValueIndex, SteadyStateEquation SubEquation) //parallelising everything here hurts performance
        {
            double multiplier = SteadyStateValues[oldSteadyStateValueIndex].Value;
            SteadyStateValues.RemoveAt(oldSteadyStateValueIndex);

            double compliment = 1; //1 - does not change value if new compliment is not found

            //a soltuion here might be to copy the Subequation into a new object


            foreach (SteadyStateValue newSteadyStateValue in SubEquation.SteadyStateValues) //System.InvalidOperationException: 'Collection was modified; enumeration operation may not execute.'
            {
                bool addedFlag = false; //was a value added in this iteration?
                int newPi = newSteadyStateValue.Pi;
                double newVal = newSteadyStateValue.Value * multiplier;

                foreach (SteadyStateValue oldSteadyStateValue in SteadyStateValues) ///aha! when two threads are trying to substitute into the same sub-equation, there seems to be a race condition
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
            for (int i = 0; i < SteadyStateValues.Count; i++) //NOTE: Parallelising this loop hurts performance
                SteadyStateValues[i].Value /= compliment;

            //Parallel.For(0, SteadyStateValues.Count, (i) =>
            //{
            //    SteadyStateValues[i].Value /= compliment;
            //});
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
