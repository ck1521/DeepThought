using Jupiter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace DeepThought
{
    public class ChicagoTypewriter
    {
        private LoggerLite logger = LoggerLite.GetInstance(typeof(ChicagoTypewriter));
        private ConcurrentDictionary<string, Term> words = new ConcurrentDictionary<string, Term>(StringComparer.CurrentCultureIgnoreCase);
        private ConcurrentDictionary<int, int> docs = new ConcurrentDictionary<int, int>();
        private int docCnt = 0;
        private double AvgDocLen = 0;
        private int minWordLen = 3;

        private Stopwatch sw = new Stopwatch();

        private object locker = new object();
        private int point = 0;

        private int maxThreadCnt = 3;
        private int curThreadCnt = 0;

        private int k = 1;
        private double b = 1;

        /// <summary>
        /// hmmm......
        /// </summary>
        /// <param name="k"></param>
        /// <param name="b"></param>
        public ChicagoTypewriter(int k = 1, double b = 1)
        {
            this.k = k;
            this.b = b;
        }

        #region Private Method

        /// <summary>
        /// ThreadMode: SingleThread
        /// 
        /// Read input, get words and their frequency
        /// TODO: Word Stemming/Normalize
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private Dictionary<string, int> Lock(string input)
        {
            int len = input.Length;
            StringBuilder sb = new StringBuilder();

            Dictionary<string, int> frequecyCounter = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            for (int i = 0; i < len; i++)
            {
                if (char.IsLetterOrDigit(input[i]))
                {
                    sb.Append(input[i]);
                }

                if (!char.IsLetterOrDigit(input[i]) || i == len - 1)
                {
                    #region Modulize here

                    if (minWordLen < sb.Length)
                    {
                        //  TODO : Word Stemming
                        string key = sb.ToString();

                        if (frequecyCounter.ContainsKey(key))
                        {
                            frequecyCounter[key]++;
                        }
                        else
                        {
                            frequecyCounter[key] = 1;
                        }
                    }
                    else
                    {
                        //  filtered
                    }

                    sb.Clear();

                    #endregion
                }
            }

            return frequecyCounter;
        }

        /// <summary>
        /// Not Using
        /// Wait for further implementation
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        private string FitChamber(string word)
        {
            return word.ToUpperInvariant();
        }

        /// <summary>
        /// Why do i need a proxy...?
        /// </summary>
        public void Load(string input, int i)
        {
            Thread worker = new Thread(() => LoadRound(input, i));
            while (curThreadCnt >= maxThreadCnt)
            {
                Thread.Sleep(100);

            }
            worker.Start();
        }

        /// <summary>
        /// ThreadMode: Multi-Thread
        /// 
        /// May be called mutiple times.
        /// </summary>
        /// <param name="input">document content</param>
        /// <param name="id">represents the document</param>
        private void LoadRound(string input, int id)
        {
            Interlocked.Increment(ref this.curThreadCnt);

            logger.Write("initiating loader on document {0}, current thread count:{1}", id + 1, curThreadCnt);

            Dictionary<string, int> frequency = Lock(input);

            #region Working with document

            docs[id] = frequency.Keys.Count;

            #endregion

            #region Working with Terms

            foreach (string key in frequency.Keys)
            {
                if (!words.ContainsKey(key))
                {
                    lock (locker)
                    {
                        if (!words.ContainsKey(key))
                        {
                            words[key] = new Term();
                        }
                    }
                }
                Term word = words[key];

                lock (locker)
                {
                    word.WordHz[id] = frequency[key];
                }

                //  IncrementLock
                Interlocked.Increment(ref word.DocHz);
            }

            #endregion

            Interlocked.Decrement(ref this.curThreadCnt);
            Interlocked.Increment(ref this.point);

            logger.Write("document {0} load complete, current progress: {1}/{2} , current thread count:{3}",
                id + 1, this.point, this.docCnt, this.curThreadCnt);
        }

        /// <summary>
        /// Because of Lambda...
        /// </summary>
        /// <param name="i"></param>
        /// <param name="pairs"></param>
        private void Charge(int i, ConcurrentDictionary<int, List<string>> pairs)
        {
            Thread worker = new Thread(() => ChargeRound(i, pairs));
            while (curThreadCnt >= maxThreadCnt)
            {
                Thread.Sleep(100);
            }
            worker.Start();
        }

        /// <summary>
        /// BOOM!
        /// Main function works here:
        /// (K+1) * WordHz[DocId] * Log(DocCnt / DocHz) / (WordHz[DocId] + K(1 - b + b * DocLen/Avg(DocLen))
        /// </summary>
        /// <returns> DocId : Top 10 matched keywords </returns>
        public void ChargeRound(int i, ConcurrentDictionary<int, List<string>> pairs)
        {
            Interlocked.Increment(ref this.curThreadCnt);

            logger.Write("initiating charger on document {0}, current thread count:{1}", i, curThreadCnt);

            SortedList<double, string> keywords = new SortedList<double, string>(10, new DuplicateKeyComparer<double>());

            foreach (string key in words.Keys)
            {
                Term t = words[key];
                if (!t.WordHz.ContainsKey(i))
                {
                    continue;
                }

                double weight =
                    (k + 1) * t.WordHz[i]                                       // Term Frequency
                    * Math.Log10((this.docCnt + 1) / 1.0 / t.DocHz)             // Invert Document Frequency
                    / (t.WordHz[i] + k * (1 - b + b * docs[i] / AvgDocLen));    // Weighted by document length in average

                #region Compare Weight, Remove, Add

                if (keywords.Count < 10)
                {
                    while (keywords.ContainsKey(weight))
                    {
                        //  This shouldn't happen!
                        throw new Exception("Is it an error or you forgot the custom comparer?");
                    }

                    keywords.Add(weight, key);
                }
                else
                {
                    if (weight > keywords.Keys[10 - 1])
                    {
                        keywords.RemoveAt(10 - 1);
                        keywords.Add(weight, key);
                    }
                }

                #endregion
            }

            pairs[i] = keywords.Values.ToList<string>();
            Interlocked.Decrement(ref this.curThreadCnt);
            Interlocked.Increment(ref this.point);

            logger.Write("document {0} charged, current progress: {1}/{2}", i, this.point, this.docCnt);
        }

        #endregion

        /// <summary>
        /// Full-Magazine
        /// </summary>
        /// <param name="path"></param>
        public string Fire(string path)
        {
            DirectoryInfo di = new DirectoryInfo(path);
            FileInfo[] files = di.GetFiles("*.txt", SearchOption.AllDirectories);

            this.docCnt = files.Length;

            logger.Write("Total Document Count:{0}", this.docCnt);

            sw.Restart();

            #region Lock n' Load

            for (int i = 0; i < files.Length; i++)
            {
                string input = File.ReadAllText(files[i].FullName);

                Load(input, i);
            }

            while (this.point < this.docCnt)
            {
                Thread.Sleep(500);
            }

            #endregion

            sw.Stop();

            logger.Write("Lock & Load time cost:{0}ms, Avg:{1}ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / this.docCnt);

            logger.Write("All documents loaded.");

            this.point = 0;

            int sumLen = 0;
            for (int i = 0; i < docCnt; i++)
            {
                sumLen += docs[i];
            }

            this.AvgDocLen = sumLen / 1.0 / docCnt;

            logger.Write("Average document length:{0} words", this.AvgDocLen);

            ConcurrentDictionary<int, List<string>> resultPairs = new ConcurrentDictionary<int, List<string>>();

            sw.Restart();

            #region Trigger Shots

            for (int i = 0; i < docCnt; i++)
            {
                Charge(i, resultPairs);
            }

            while (this.point < this.docCnt)
            {
                Thread.Sleep(500);
            }

            #endregion

            sw.Stop();

            logger.Write("Trigger time cost: {0}ms, Avg:{1}ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / this.docCnt);

            #region Output Results

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < files.Length; i++)
            {
                sb.AppendFormat("{0} : ", files[i].Name);
                resultPairs[i].ForEach(x => sb.AppendFormat("{0} ", x));
                sb.AppendLine();
            }

            return sb.ToString();

            #endregion
        }


    }

    /// <summary>
    /// Comparer for comparing two keys, handling equality as being greater
    /// Use this Comparer e.g. with SortedLists or SortedDictionaries, that don't allow duplicate keys
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    internal class DuplicateKeyComparer<TKey> : IComparer<TKey> where TKey : IComparable
    {
        #region IComparer<TKey> Members

        public int Compare(TKey x, TKey y)
        {
            int result = x.CompareTo(y);

            if (result == 0)
                return 1;   // Handle equality as being greater
            else
                return result;
        }

        #endregion
    }
}
