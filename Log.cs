using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace TWS
{
    class Log
    {
        /// <summary>
        /// 文本输出一般日志信息
        /// </summary>
        /// <param name="info"></param>
        public static void WriteLine(string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\Log\Log.txt", true))
            {
                sw.WriteLine(info);
            }
        }

        /// <summary>
        /// 文本输出交易相关日志信息
        /// </summary>
        /// <param name="info"></param>
        public static void WriteLineTrader(string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\Log\LogTrader.txt", true))
            {
                sw.WriteLine(info);
            }
        }

        /// <summary>
        /// 文本输出策略交易相关日志信息
        /// </summary>
        /// <param name="info"></param>
        public static void WriteLineStrategyTrader(string strategyName, string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\" + strategyName + @"\Log\Trade.txt", true))
            {
                sw.WriteLine(info);
            }
        }


        /// <summary>
        /// 文本输出策略委托相关日志信息
        /// </summary>
        /// <param name="info"></param>
        public static void WriteLineStrategyOrder(string strategyName, string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\" + strategyName + @"\Log\Order.txt", true))
            {
                sw.WriteLine(info);
            }
        }

        /// <summary>
        /// 文本输出行情相关日志信息
        /// </summary>
        /// <param name="info"></param>
        public static void WriteLineMD(string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\Log\LogMD.txt", true))
            {
                sw.WriteLine(info);
            }
        }

        /// <summary>
        /// 文本输出交易信号
        /// </summary>
        /// <param name="info"></param>
        public static void WriteStrategySignal(string strategyName, string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\" + strategyName + @"\Log\Signal-" + ".txt", true))
            {
                sw.WriteLine(info);
            }
        }

        public static void WriteReport(string strategyName, string instrumentID, string info)
        {
            using (StreamWriter sw = new StreamWriter(Application.StartupPath + @"\" + strategyName + @"\FutureReportInfo\" + instrumentID + ".txt", true))
            {
                sw.WriteLine(info);
            }
        }

    }
}
