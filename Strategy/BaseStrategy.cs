using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TWS.Data;
using IBApi;
using System.Threading;

namespace TWS.Strategy
{
    /// <summary>
    /// 各种策略的抽象类，封装策略公用的方法与数据
    /// </summary>
    public abstract class BaseStrategy
    {
        protected string m_StrategyName;
        protected StrategyPool m_StrategyPool;
        protected int m_StartTickID;
        
        /// <summary>
        /// 合约数组
        /// </summary>
        protected Contract[] m_ContractList;

        private static object positionBufferLock = new object();
        /// <summary>
        /// 持仓缓存数组
        /// </summary>
        protected List<Position> m_PositionBuffers = new List<Position>();
        /// <summary>
        /// 策略持仓数组
        /// </summary>
        protected List<Position> m_Positions = new List<Position>();

        private static object reportListBufferLock = new object();
        /// <summary>
        /// 行情缓存数组,缓存区只存储最近的行情
        /// </summary>
        protected MdReport[] m_ReportBuffers = null;
        /// <summary>
        /// 策略行情数组
        /// </summary>
        protected List<MdReport>[] m_ReportList = null;
         
        /// <summary>
        /// 委托报单缓冲区锁，防止线程冲突
        /// </summary>
        private static object orderReportBufferLock = new object();
        /// <summary>
        /// 委托报单缓冲区，用以存储新收到，还没有来得及处理的委托报单数据
        /// </summary>
        private List<OrderReport> orderReportBuffer = new List<OrderReport>();
        /// <summary>
        /// 委托状态缓冲区，用以存储新收到，还没有来得及处理的委托状态数据
        /// </summary>
        private List<OrderStatus> orderStatusBuffer = new List<OrderStatus>();
        /// <summary>
        /// 活动队列中的委托单
        /// </summary>
        protected List<OrderReport> m_OrderReportList = new List<OrderReport>();

        private bool isUpdate = false;
        private static object runLock = new object();
        /// <summary>
        /// 存储策略各自的OrderID
        /// </summary>
        protected List<int> m_OrderIdList = new List<int>();

        /// <summary>
        /// 还没有返回委托回报的委托
        /// </summary>
        protected List<int> m_NotBackOrderIdList = new List<int>();

        /// <summary>
        /// 等待未成交回报委托单的最大次数
        /// </summary>
        const int Not_Back_Max_Count = 1000;
        /// <summary>
        /// 控制等待未返回回报的委托单的时间
        /// </summary>
        int waitNotBackCount = 0;

        Thread m_StrategyThread;
        /// <summary>
        /// 持仓数组,每个数组成员存放一个合约品种的持仓信息
        /// </summary>
        protected List<Position> m_PositionList = new List<Position>();

        public BaseStrategy(string strategyName, StrategyPool strategyPool, int startTickID)
        {
            m_StrategyName = strategyName;
            m_StrategyPool = strategyPool;
            m_StartTickID = startTickID;

            InitStrategyParm();

            ReqMktData();
            //初始化行情数组
            m_ReportBuffers = new MdReport[m_ContractList.Length];
            m_ReportList = new List<MdReport>[m_ContractList.Length];
            for (int i = 0; i < m_ContractList.Length; i++)
            {
                m_ReportBuffers[i] = new MdReport();
                m_ReportList[i] = new List<MdReport>();
            }
            m_StrategyThread = new Thread(Run);
            m_StrategyThread.Start();
        }

        protected abstract void ReqMktData();
        /// <summary>
        ///  通过文本初始化策略参数
        /// </summary>
        /// <returns></returns>
        protected abstract void InitStrategyParm();
        /// <summary>
        /// 用最新的数据进行策略分析、下单
        /// </summary>
        protected abstract void RunStrategy();

        protected abstract void UpdateMdReport();

        /// <summary>
        /// 启动策略线程
        /// </summary>
        private void Run()
        {
            Thread.Sleep(5000);//等待持仓等数据的接收
            while (true)
            {
                

                while (!isUpdate)
                {
                    lock (runLock)
                    {
                        Monitor.Wait(runLock);
                    }
                }
                
                UpdateData();
                //当有委托号还没有返回的委托时，说明上一次委托存在时间过短，此时不用跑策略
                if (m_NotBackOrderIdList.Count == 0)
                {
                    RunStrategy();
                    waitNotBackCount = 0;
                }
                else
                {
                    if (waitNotBackCount < Not_Back_Max_Count)
                    {
                        waitNotBackCount++;
                    }
                    else
                    {   
                        CancelAllNotBackOrder();
                    }
                }
                isUpdate = false;
            }
        }

        /// <summary>
        /// 限价委托下单
        /// </summary>
        protected void ReqOrderInsert(int orderId, Contract contract, Order order)
        {
            Log.WriteLineStrategyOrder(m_StrategyName, DateTime.Now.ToLongTimeString() + ":" + DateTime.Now.Millisecond + "  " + contract.Symbol + ", " + contract.SecType + ", " + contract.Strike + " @ " + contract.Exchange + ": " + order.Action + ", " + order.OrderType + " " + order.LmtPrice + " " + order.TotalQuantity);
            m_StrategyPool.ReqOrderInsert(orderId, contract, order);
            m_OrderIdList.Add(orderId);
            m_NotBackOrderIdList.Add(orderId);

            OrderReport orderReport = new OrderReport();
            orderReport.OrderId = orderId;
            orderReport.OrderContract = contract;
            orderReport.CurOrder = order;
            m_OrderReportList.Add(orderReport);
        }

        public void ReceiveTickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            if (price != -1)//过滤掉无效的价格数据
            {
                lock (reportListBufferLock)
                {
                    //判断行情是否属于此策略
                    if (tickerId >= m_StartTickID && tickerId < m_StartTickID + StrategyPool.TICK_ID_REGION)
                    {
                        int tickerIndex = tickerId - m_StartTickID;
                        bool isBidAsk = false;
                        switch (field)
                        {
                            case TickType.BID:
                                {
                                    m_ReportBuffers[tickerIndex].BidPrice = price;
                                    m_ReportBuffers[tickerIndex].CurPrice = price;
                                    m_ReportBuffers[tickerIndex].Time = DateTime.Now;
                                    isBidAsk = true;
                                    break;
                                }
                            case TickType.ASK:
                                {
                                    m_ReportBuffers[tickerIndex].AskPrice = price;
                                    m_ReportBuffers[tickerIndex].CurPrice = price;
                                    m_ReportBuffers[tickerIndex].Time = DateTime.Now;
                                    isBidAsk = true;
                                    break;
                                }
                        }
                        //缓存区接收了完整的行情才触发策略线程
                        if (isBidAsk && m_ReportBuffers[tickerIndex].AskPrice != 0 && m_ReportBuffers[tickerIndex].AskSize != 0
                            && m_ReportBuffers[tickerIndex].BidPrice != 0 && m_ReportBuffers[tickerIndex].BidSize != 0)
                        {
                            isUpdate = true;
                            //取消线程等待，继续运行线程
                            lock (runLock)
                            {
                                Monitor.PulseAll(runLock);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 接收委托量的变化信息（委托量的变化暂不触发策略线程运行）
        /// </summary>
        /// <param name="tickerId"></param>
        /// <param name="field"></param>
        /// <param name="size"></param>
        public void ReceiveTickSize(int tickerId, int field, int size)
        {
            lock (reportListBufferLock)
            {
                //判断行情是否属于此策略
                if (tickerId >= m_StartTickID && tickerId < m_StartTickID + StrategyPool.TICK_ID_REGION)
                {       
                    int tickerIndex = tickerId - m_StartTickID;
                    bool isInit = false;
                    switch (field)
                    {
                        case TickType.BID_SIZE:
                            {
                                if (m_ReportBuffers[tickerIndex].BidSize == 0)//判断是否第一次初始化
                                {
                                    isInit = true;
                                }
                                m_ReportBuffers[tickerIndex].BidSize = size;
                                m_ReportBuffers[tickerIndex].Time = DateTime.Now;
                                break;
                            }
                        case TickType.ASK_SIZE:
                            {
                                if (m_ReportBuffers[tickerIndex].AskSize == 0)//判断是否第一次初始化
                                {
                                    isInit = true;
                                }
                                m_ReportBuffers[tickerIndex].AskSize = size;
                                m_ReportBuffers[tickerIndex].Time = DateTime.Now;
                                break;
                            }
                    }
                    //初始化状态且缓存区接收了完整的行情才触发策略线程，平时委托量的变化太频繁，不触发行情
                    if (isInit && m_ReportBuffers[tickerIndex].AskPrice != 0 && m_ReportBuffers[tickerIndex].AskSize != 0
                        && m_ReportBuffers[tickerIndex].BidPrice != 0 && m_ReportBuffers[tickerIndex].BidSize != 0)
                    {
                        isUpdate = true;
                        //取消线程等待，继续运行线程
                        lock (runLock)
                        {
                            Monitor.PulseAll(runLock);
                        }
                    }
                }              
            }
        }

        /// <summary>
        /// 接收委托报单数据，并将当前策略的报单写入缓冲区中
        /// </summary>
        public void ReceiveOpenOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            //判断此委托报单是否属于此策略
            if (m_OrderIdList.Contains(orderId))
            {
                lock (orderReportBufferLock)
                {
                    OrderReport orderReport = new OrderReport();
                    orderReport.OrderId = orderId;
                    orderReport.OrderContract = contract;
                    orderReport.CurOrder = order;
                    orderReport.CurOrderState = orderState;
                    orderReportBuffer.Add(orderReport);
                }
            }
        }

        /// <summary>
        /// 接收委托状态数据，并将当前策略的委托状态写入缓冲区中
        /// </summary>
        public void ReceiveOrderStatus(int orderId, string status, int filled, int remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld)
        {
            //判断此委托报单是否属于此策略
            if (m_OrderIdList.Contains(orderId))
            {
                lock (orderReportBufferLock)
                {
                    OrderStatus orderStatus = new OrderStatus();
                    orderStatus.OrderId = orderId;
                    orderStatus.Status = status;
                    orderStatus.Filled = filled;
                    orderStatus.Remaining = remaining;
                    orderStatus.AvgFillPrice = avgFillPrice;
                    orderStatus.PermId = permId;
                    orderStatus.ParentId = parentId;
                    orderStatus.LastFillPrice = lastFillPrice;
                    orderStatus.ClientId = clientId;
                    orderStatus.WhyHeld = whyHeld;
                    orderStatusBuffer.Add(orderStatus);

                    isUpdate = true;
                    //取消线程等待，继续运行线程
                    lock (runLock)
                    {
                        Monitor.PulseAll(runLock);
                    }
                }
            }
        }

        /// <summary>
        /// 根据当前策略的合约更新持仓列表
        /// </summary>
        /// <param name="positionList"></param>
        public void ReceivePosition(string account, Contract contract, int pos, double avgCost)
        {
            lock (positionBufferLock)
            {
                for (int i = 0; i < m_ContractList.Length; i++)
                {
                    //判断是否属于此策略
                    if (IsEqual(m_ContractList[i],contract))
                    {
                        Position position = new Position();
                        position.Account = account;
                        position.PositionContract = m_ContractList[i];
                        position.PositionNum = pos;
                        position.AvgCost = avgCost;
                        m_PositionBuffers.Add(position);

                        isUpdate = true;
                        //取消线程等待，继续运行线程
                        lock (runLock)
                        {
                            Monitor.PulseAll(runLock);
                        }

                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 对所有委托单撤单
        /// </summary>
        protected void CancelAllQueueingOrder()
        {
            for (int i = 0; i < m_OrderReportList.Count; i++)
            {
                m_NotBackOrderIdList.Add(m_OrderReportList[i].OrderId);
                CancelOrder(m_OrderReportList[i].OrderId);
            }
        }

        /// <summary>
        /// 撤单
        /// </summary>
        protected void CancelOrder(int orderId)
        {
            m_NotBackOrderIdList.Add(orderId);
            m_StrategyPool.CancelOrder(orderId);
        }

        /// <summary>
        /// 对所有未返回回报委托单的撤单
        /// </summary>
        protected void CancelAllNotBackOrder()
        {
            m_StrategyPool.ClientSocket.reqGlobalCancel();
            m_NotBackOrderIdList.Clear();
        }

        /// <summary>
        /// 通过缓冲区更新行情、委托报单、成交报单等数据
        /// </summary>
        private void UpdateData()
        {
            UpdatePosition();
            UpdateMdReportBase();
            UpdateOrederReport();
            UpdateOrederStatus();
        }

        private void UpdatePosition()
        {
            lock (positionBufferLock)
            {
                for (int j = 0; j < m_PositionBuffers.Count; j++)
                {
                    Position position = m_PositionBuffers[j];
                    
                    //看已有持仓中是否有此合约，有则更新
                    bool isContain = false;
                    for (int i = 0; i < m_PositionList.Count; i++)
                    {
                        if (IsEqual(position.PositionContract,m_PositionList[i].PositionContract))
                        {
                            m_PositionList[i].PositionNum = position.PositionNum;
                            m_PositionList[i].AvgCost = position.AvgCost;
                            isContain = true;
                            break;
                        }
                    }
                    //已有持仓中没有此合约，则判断此合约是否属于本策略，是则添加
                    if (!isContain)
                    {
                        for (int i = 0; i < m_ContractList.Length; i++)
                        {
                            if (IsEqual(m_ContractList[i],position.PositionContract))
                            {
                                this.m_PositionList.Add(position);
                                break;
                            }
                        }
                    }
                }
                m_PositionBuffers.Clear();
            }
        }

        private void UpdateMdReportBase()
        {
            lock (reportListBufferLock)
            {
                UpdateMdReport();
            }
        }

        /// <summary>
        /// 通过委托报单缓冲区更新委托报单数组
        /// </summary>
        private void UpdateOrederReport()
        {
            lock (orderReportBufferLock)
            {
                for (int i = 0; i < orderReportBuffer.Count; i++)
                {
                    OrderReport orderReport = orderReportBuffer[i];

                    switch (orderReport.CurOrderState.Status)
                    {
                        case "Submitted"://交易进行中，增加或更新活动委托单信息
                                for (int k = 0; k < m_OrderReportList.Count; k++)
                                {
                                    if (m_OrderReportList[k].OrderId == orderReport.OrderId)
                                    {
                                        orderReport.CurOrderStatus = m_OrderReportList[k].CurOrderStatus;
                                        m_OrderReportList[k] = orderReport;
                                        break;
                                    }
                                }

                                for (int k = m_NotBackOrderIdList.Count - 1; k >= 0; k--)
                                {
                                    if (m_NotBackOrderIdList[k] == orderReport.OrderId)
                                    {
                                        m_NotBackOrderIdList.RemoveAt(k);
                                    }
                                }
                            break;
                        case "Filled"://交易进行中，增加或更新活动委托单信息
                            for (int k = 0; k < m_OrderReportList.Count; k++)
                            {
                                if (m_OrderReportList[k].OrderId == orderReport.OrderId)
                                {
                                    orderReport.CurOrderStatus = m_OrderReportList[k].CurOrderStatus;
                                    m_OrderReportList[k] = orderReport;
                                    break;
                                }
                            }

                            for (int k = m_NotBackOrderIdList.Count - 1; k >= 0; k--)
                            {
                                if (m_NotBackOrderIdList[k] == orderReport.OrderId)
                                {
                                    m_NotBackOrderIdList.RemoveAt(k);
                                }
                            }
                            break;

                        default:
                            break;
                    }
                }
                orderReportBuffer.Clear();
            }

        }

        /// <summary>
        /// 通过委托状态缓冲区更新委托报单数组
        /// </summary>
        private void UpdateOrederStatus()
        {
            lock (orderReportBufferLock)
            {
                for (int i = 0; i < orderStatusBuffer.Count; i++)
                {
                    OrderStatus orderStatus = orderStatusBuffer[i];
                    switch (orderStatus.Status)
                    {
                        case StatusType.Submitted://交易进行中，增加或更新活动委托单
                            for (int j = 0; j < m_OrderReportList.Count; j++)
                            {
                                if (m_OrderReportList[j].OrderId == orderStatus.OrderId)
                                {
                                    m_OrderReportList[j].CurOrderStatus = orderStatus;
                                    break;
                                }
                            }

                            for (int j = m_NotBackOrderIdList.Count - 1; j >= 0; j--)
                            {
                                if (m_NotBackOrderIdList[j] == orderStatus.OrderId)
                                {
                                    m_NotBackOrderIdList.RemoveAt(j);
                                }
                            }
                            break;
                        case StatusType.Cancelled://撤销委托单,此时有可能部分成交，应更新持仓，并从活动委托队列中移除该委托单
                            for (int j = 0; j < m_OrderReportList.Count; j++)
                            {
                                if (m_OrderReportList[j].OrderId == orderStatus.OrderId)
                                {
                                    //看已有持仓中是否有此合约，有则更新
                                    bool isContain = false;
                                    if (orderStatus.Filled != 0)
                                    {
                                        OrderReport curOrderReport = m_OrderReportList[j];                                        
                                        for (int k = 0; k < m_PositionList.Count; k++)
                                        {
                                            Position curPosition = m_PositionList[k];
                                            if(IsEqual(curPosition.PositionContract,curOrderReport.OrderContract))
                                            {
                                                isContain = true;
                                                switch (curOrderReport.CurOrder.Action)
                                                {
                                                    case OrderActionType.Buy:
                                                        curPosition.PositionNum += orderStatus.Filled;
                                                        break;
                                                    case OrderActionType.Sell:
                                                        curPosition.PositionNum -= orderStatus.Filled;
                                                        break;

                                                }
                                                break;
                                            }
                                        }
                                        //已有持仓中没有此合约，则判断此合约是否属于本策略，是则添加
                                        if (!isContain)
                                        {
                                            for (int k = 0; k < m_ContractList.Length; k++)
                                            {
                                                if (IsEqual(m_ContractList[k], curOrderReport.OrderContract))
                                                {
                                                    Position position = new Position();
                                                    position.Account = StrategyPool.Account;
                                                    position.AvgCost = orderStatus.AvgFillPrice;
                                                    position.PositionContract = m_ContractList[k];
                                                    switch (curOrderReport.CurOrder.Action)
                                                    {
                                                        case OrderActionType.Buy:
                                                            position.PositionNum += orderStatus.Filled;
                                                            break;
                                                        case OrderActionType.Sell:
                                                            position.PositionNum -= orderStatus.Filled;
                                                            break;
                                                    }
                                                    this.m_PositionList.Add(position);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    m_OrderReportList.RemoveAt(j);
                                    break;
                                }
                            }
                            for (int j = m_NotBackOrderIdList.Count - 1; j >= 0; j--)
                            {
                                if (m_NotBackOrderIdList[j] == orderStatus.OrderId)
                                {
                                    m_NotBackOrderIdList.RemoveAt(j);
                                }
                            }
                            break;
                        case StatusType.Filled://全部成交，应更新持仓,并从活动委托队列中移除该委托单                           
                            for (int j = 0; j < m_OrderReportList.Count; j++)
                            {
                                if (m_OrderReportList[j].OrderId == orderStatus.OrderId)
                                {
                                    //更新持仓
                                    if (orderStatus.Filled != 0)
                                    {
                                        OrderReport curOrderReport = m_OrderReportList[j];
                                        //看已有持仓中是否有此合约，有则更新
                                        bool isContain = false;
                                        for (int k = 0; k < m_PositionList.Count; k++)
                                        {
                                            Position curPosition = m_PositionList[k];
                                            if (IsEqual(curPosition.PositionContract, curOrderReport.OrderContract))
                                            {
                                                isContain = true;
                                                switch (curOrderReport.CurOrder.Action)
                                                {
                                                    case OrderActionType.Buy:
                                                        curPosition.PositionNum += orderStatus.Filled;
                                                        break;
                                                    case OrderActionType.Sell:
                                                        curPosition.PositionNum -= orderStatus.Filled;
                                                        break;
                                                }
                                                break;
                                            }
                                        }
                                        //已有持仓中没有此合约，则判断此合约是否属于本策略，是则添加
                                        if (!isContain)
                                        {
                                            for (int k = 0; k < m_ContractList.Length; k++)
                                            {
                                                if (IsEqual(m_ContractList[k], curOrderReport.OrderContract))
                                                {
                                                    Position position = new Position();
                                                    position.Account = StrategyPool.Account;
                                                    position.AvgCost = orderStatus.AvgFillPrice;
                                                    position.PositionContract = m_ContractList[k];
                                                    switch (curOrderReport.CurOrder.Action)
                                                    {
                                                        case OrderActionType.Buy:
                                                            position.PositionNum += orderStatus.Filled;
                                                            break;
                                                        case OrderActionType.Sell:
                                                            position.PositionNum -= orderStatus.Filled;
                                                            break;
                                                    }
                                                    this.m_PositionList.Add(position);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    m_OrderReportList.RemoveAt(j);

                                    break;
                                }
                            }

                            for (int j = m_NotBackOrderIdList.Count - 1; j >= 0; j--)
                            {
                                if (m_NotBackOrderIdList[j] == orderStatus.OrderId)
                                {
                                    m_NotBackOrderIdList.RemoveAt(j);
                                }
                            }
                            break;
                    }
                    
                }
                orderStatusBuffer.Clear();
            }
        }

        /// <summary>
        /// 判断两个合约是否相同
        /// </summary>
        /// <param name="one"></param>
        /// <param name="two"></param>
        /// <returns></returns>
        protected bool IsEqual(Contract one,Contract two)
        {
            if (one.Symbol == two.Symbol && one.Strike == two.Strike && one.Right == two.Right && one.Expiry == two.Expiry)
            {
                return true;
            }
            return false;
        }
    }
}

