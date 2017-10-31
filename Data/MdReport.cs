using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;

namespace TWS.Data
{
    /// <summary>
    /// 实时行情信息
    /// </summary>
    public class MdReport
    {
        Contract mdContract;
        /// <summary>
        /// 获取或设置期货代码
        /// </summary>
        public Contract MdContract
        {
            get { return mdContract; }
            set { mdContract = value; }
        }

        DateTime time;
        /// <summary>
        /// 获取或设置日期
        /// </summary>
        public DateTime Time
        {
            get { return time; }
            set { time = value; }
        }

        double curPrice = 0;
        /// <summary>
        /// 期货当前价格
        /// </summary>
        public double CurPrice
        {
            get { return curPrice; }
            set { curPrice = value; }
        }

        int bidSize;
        /// <summary>
        /// 买一量
        /// </summary>
        public int BidSize
        {
            get { return bidSize; }
            set { bidSize = value; }
        }

        double bidPrice;
        /// <summary>
        /// 买一价格
        /// </summary>
        public double BidPrice
        {
            get { return bidPrice; }
            set { bidPrice = value; }
        }

        int askSize;
        /// <summary>
        /// 卖一量
        /// </summary>
        public int AskSize
        {
            get { return askSize; }
            set { askSize = value; }
        }

        double askPrice;
        /// <summary>
        /// 卖一价格
        /// </summary>
        public double AskPrice
        {
            get { return askPrice; }
            set { askPrice = value; }
        }
    }
}
