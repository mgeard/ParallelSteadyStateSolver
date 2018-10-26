using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelSteadyStateSolver
{
    public static class Extensions
    {

        public static void Write(this StringBuilder csv, string fileName)
        {
            try
            {
                string path = fileName;

                if (File.Exists(path))
                    File.Delete(path);

                using (FileStream stream = File.Create(path))
                {
                    byte[] csvBytes = new UTF8Encoding(true).GetBytes(csv.ToString());
                    stream.Write(csvBytes, 0, csvBytes.Length);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

    }
}
