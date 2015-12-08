using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Jupiter
{
    /// <summary>
    /// General log utility, inferior version of general logger.
    /// Provide basic funtion of writing logs in certain format.
    /// </summary>
    public class LoggerLite
    {
        private static object locker = new object();
        private static bool loaded = false;

        private static Dictionary<Type, LoggerLite> loggerDic = new Dictionary<Type, LoggerLite>();

        private static StreamWriter sw = null;
        private static Queue<string> LogEntries = new Queue<string>();

        private string mClassName = null;

        public static LoggerLite GetInstance(Type type)
        {
            if (!loaded)
            {
                lock (locker)
                {
                    if (!loaded)
                    {
                        loaded = true;
                        Initialize();
                    }
                }
            }

            if (!loggerDic.ContainsKey(type))
            {
                lock (loggerDic)
                {
                    if (!loggerDic.ContainsKey(type))
                    {
                        loggerDic[type] = new LoggerLite(type);
                    }
                }
            }
            return loggerDic[type];
        }

        public void Debug(string log, params object[] args)
        {
            Write(LogLevel.DEBUG, log, args);
        }

        public void Warn(string log, params object[] args)
        {
            Write(LogLevel.WARN, log, args);
        }

        public void Trace(string log, params object[] args)
        {
            Write(LogLevel.TRACE, log, args);
        }


        private void Write(LogLevel level, string log, params object[] args)
        {
            lock (LogEntries)
            {
                LogEntries.Enqueue(FormatLog(string.Format(log, args), level));
            }
        }

        private static void Initialize()
        {
            Thread worker = new Thread(WorkerThread);
            worker.IsBackground = true;
            worker.Start();
        }

        private LoggerLite(Type type)
        {
            this.mClassName = type.Name.ToString();
        }

        private string FormatLog(string log, LogLevel level)
        {
            return string.Format("{0}\t{1}\t{2}\t{3}\t{4}",
                level.ToString(), DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss.fff"), Thread.CurrentThread.ManagedThreadId, this.mClassName, log);
        }

        private static void WorkerThread()
        {
            string filename = string.Format("{0}_{1}.log",
                    Process.GetCurrentProcess().ProcessName, DateTime.Now.ToString("yyyyMMddhhmmss"));
            sw = new StreamWriter(filename, true, Encoding.UTF8);
            sw.AutoFlush = false;

            try
            {
                while (true)
                {
                    if (LogEntries.Count > 0)
                    {
                        string[] tempEntries = null;
                        lock (LogEntries)
                        {
                            tempEntries = LogEntries.ToArray();
                            LogEntries.Clear();
                        }
                        foreach (string entry in tempEntries)
                        {
                            sw.WriteLine(entry);
                        }
                        sw.Flush();
                    }
                    else
                    {
                        Thread.Sleep(300);
                    }
                }
            }
            finally
            {
                sw.Dispose();
            }
        }

        enum LogLevel
        {
            TRACE,
            DEBUG,
            WARN,
            ERROR
        }
    }
}
