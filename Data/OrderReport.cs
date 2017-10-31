using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;

namespace TWS.Data
{
    /// <summary>
    /// 委托报单
    /// </summary>
    public class OrderReport
    {
        DateTime time;
        /// <summary>
        /// 获取或设置日期
        /// </summary>
        public DateTime Time
        {
            get { return time; }
            set { time = value; }
        }

        private int orderId;
        /// <summary>
        /// 委托编号
        /// </summary>
        public int OrderId
        {
            get { return orderId; }
            set { orderId = value; }
        }

        private Contract orderContract;
        /// <summary>
        /// 委托合约
        /// </summary>
        public Contract OrderContract
        {
            get { return orderContract; }
            set { orderContract = value; }
        }

        private Order curOrder;
        /// <summary>
        /// 当前订单
        /// </summary>
        public Order CurOrder
        {
            get { return curOrder; }
            set { curOrder = value; }
        }

        private OrderState curOrderState;
        /// <summary>
        /// 价格
        /// </summary>
        public OrderState CurOrderState
        {
            get { return curOrderState; }
            set { curOrderState = value; }
        }

        private OrderStatus curOrderStatus;

        public OrderStatus CurOrderStatus
        {
            get { return curOrderStatus; }
            set { curOrderStatus = value; }
        }
    }
}
