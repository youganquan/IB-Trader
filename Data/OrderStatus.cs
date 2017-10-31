using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TWS.Data
{
    /// <summary>
    /// 委托单实时状态
    /// </summary>
    public class OrderStatus
    {
        int orderId;

        public int OrderId
        {
            get { return orderId; }
            set { orderId = value; }
        }
        string status;

        public string Status
        {
            get { return status; }
            set { status = value; }
        }
        int filled;
        /// <summary>
        /// 成交数量
        /// </summary>
        public int Filled
        {
            get { return filled; }
            set { filled = value; }
        }
        int remaining;
        /// <summary>
        /// 未成交数量
        /// </summary>
        public int Remaining
        {
            get { return remaining; }
            set { remaining = value; }
        }
        double avgFillPrice;

        public double AvgFillPrice
        {
            get { return avgFillPrice; }
            set { avgFillPrice = value; }
        }
        int permId;

        public int PermId
        {
            get { return permId; }
            set { permId = value; }
        }
        int parentId;

        public int ParentId
        {
            get { return parentId; }
            set { parentId = value; }
        }
        double lastFillPrice;

        public double LastFillPrice
        {
            get { return lastFillPrice; }
            set { lastFillPrice = value; }
        }
        int clientId;

        public int ClientId
        {
            get { return clientId; }
            set { clientId = value; }
        }
        string whyHeld;

        public string WhyHeld
        {
            get { return whyHeld; }
            set { whyHeld = value; }
        }
    }
}
