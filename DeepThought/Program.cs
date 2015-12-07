using Jupiter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DeepThought
{
    class Program
    {
        static void Main(string[] args)
        {
            LoggerLite logger = LoggerLite.GetInstance(typeof(ChicagoTypewriter));

            Sheriff.Assert(args.Length > 0, "Please input a valid path where txts located.");

            int k = 2;
            double b = 0.2;
            string fileName = string.Format("Results_{0}_{1}_Log10.txt", k, b);

            ChicagoTypewriter ctw = new ChicagoTypewriter(k, b);

            try
            {
                string workPath = Path.GetFullPath(args[0]);
                string result = ctw.Fire(workPath);

                File.WriteAllText(workPath + @"Result\" + fileName, result);
                Console.WriteLine("All Done.");
            }
            catch (Exception ex)
            {
                logger.Debug("Error: {0}", ex.ToString());
                Console.WriteLine("Some error occured.");
            }
            Console.ReadKey();
            Thread.Sleep(2000);
        }
    }
}
