using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;

namespace TWS.Data
{
    /// <summary>
    /// 持仓信息
    /// </summary>
    public class Position
    {
        private string account;

        public string Account
        {
            get { return account; }
            set { account = value; }
        }

        private Contract positionContract;

        public Contract PositionContract
        {
            get { return positionContract; }
            set { positionContract = value; }
        }

        private int positionNum;
        /// <summary>
        /// 总持仓,正数表示做多，负数表示做空
        /// </summary>
        public int PositionNum
        {
            get { return positionNum; }
            set { positionNum = value; }
        }

        private double avgCost;
        /// <summary>
        /// 开仓平均成本
        /// </summary>
        public double AvgCost
        {
            get { return avgCost; }
            set { avgCost = value; }
        }
    }
}
