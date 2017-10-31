using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;

namespace TWS.Data
{
    class Entrust
    {
        private Contract entrustContract;
        /// <summary>
        /// 委托合约
        /// </summary>
        public Contract EntrustContract
        {
            get { return entrustContract; }
            set { entrustContract = value; }
        }

        private Order entrustOrder;
        /// <summary>
        /// 当前订单
        /// </summary>
        public Order EntrustOrder
        {
            get { return entrustOrder; }
            set { entrustOrder = value; }
        }
    }
}
