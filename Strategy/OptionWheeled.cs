using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;
using System.IO;
using TextTool;
using TWS.Data;

namespace TWS.Strategy
{
    public class OptionWheeled : BaseStrategy
    {
        /// <summary>
        /// 基准持仓多少手，以"C"或"P"为基准
        /// </summary>
        int m_BaseMktQty = 1;

        /// <summary>
        /// 期权轮动阀值
        /// </summary>
        double m_WheeledThreshold = 1;

        /// <summary>
        /// 默认的基准持仓方向，"C"或"P"，用来控制先平"C"或"P"
        /// </summary>
        string m_BasePostionRight = ContractRight.Call;

        public OptionWheeled(string strategyName, StrategyPool strategyPool, int startTickID)
            : base(strategyName, strategyPool, startTickID)
        {
        }

        protected override void RunStrategy()
        {
            int strikeCount = m_ContractList.Length/2;

            #region 计算总持仓手数
            //持仓总手数
            int totalMktQty = 0;

            //各个行权价的持仓
            int[] optMktQty = new int[strikeCount];
            for (int i = 0; i < strikeCount; i++)
            {
                for (int j = 0; j < m_PositionList.Count; j++)
                {
                    Position curPosition = m_PositionList[j];
                    if (curPosition.PositionContract.Right == m_BasePostionRight && curPosition.PositionContract.Strike == m_ContractList[i * 2].Strike)
                    {
                        int postionNum = Math.Abs(curPosition.PositionNum);
                        optMktQty[i] += postionNum; 
                        totalMktQty +=  postionNum;
                    }     
                }
            }
            #endregion

            #region 通过买入对手价计算每对期权对应的价值,并找到价值最高的品种
         
            double[] optValues = new double[strikeCount];
            int maxValueIndex = -1;
            double maxValue = double.MinValue;
            for (int i = 0; i < strikeCount; i++)
            {
                optValues[i] = double.MinValue;
                //C、P期权无价格或持仓市值达到上限时，不参与最低价格的计算
                if (m_ReportList[i * 2][0].AskPrice != 0 && m_ReportList[i * 2 + 1][0].BidPrice != 0)
                {
                    if(optMktQty[i] < m_BaseMktQty)
                    {
                        optValues[i] = m_ReportList[i * 2 + 1][0].BidPrice - m_ReportList[i * 2][0].AskPrice - m_ContractList[i * 2].Strike;
                        if (optValues[i] > maxValue)
                        {
                            maxValue = optValues[i];
                            maxValueIndex = i;
                        }
                    }
                }
                else
                {
                    return;//如果行情还没有准备好，不用继续跑策略的逻辑
                }
            }

            #endregion

            //优先考虑单腿的情况
            Entrust sellPutEntrust = null;//卖出看跌期权订单
            Entrust buyPutEntrust = null;//买入看涨期权订单
            Entrust sellCallEntrust = null;
            Entrust buyCallEntrust = null;

            #region 根据基准持仓方向获取单腿订单，同时在不需要单腿处理的时候根据买卖价差调整基准买卖方向

            if (m_BasePostionRight == ContractRight.Call)
            {
                sellPutEntrust = GetSellPutEntrust();
                if (sellPutEntrust == null)
                {
                    buyPutEntrust = GetBuyPutEntrust();
                }
                //当不存在单腿且不用开新仓时，通过持仓品种的买卖价差确定基准持仓方向
                if (sellPutEntrust == null && buyPutEntrust == null && m_BaseMktQty <= totalMktQty)
                {
                    for (int j = 0; j < m_PositionList.Count; j++)
                    {
                        Position callPosition = m_PositionList[j];
                        if (callPosition.PositionContract.Right == ContractRight.Call && callPosition.PositionNum > 0)
                        {
                            //遍历得到Call的买卖价差
                            double callBidAskOffset = double.MaxValue;
                            for (int i = 0; i < m_ContractList.Length / 2; i++)
                            {
                                if (m_ReportList[i * 2][0].MdContract.Strike == callPosition.PositionContract.Strike)//m_Reports[i * 2]是Call
                                {
                                    callBidAskOffset = m_ReportList[i * 2][0].AskPrice - m_ReportList[i * 2][0].BidPrice;
                                    break;
                                }
                            }
                            //寻找Call持仓对应的Put持仓并遍历得到Put的买卖价差
                            Position putPosition = null;
                            double putBidAskOffset = double.MaxValue;
                            for (int k = 0; k < m_PositionList.Count; k++)
                            {
                                if (m_PositionList[k].PositionContract.Strike == callPosition.PositionContract.Strike && m_PositionList[k].PositionContract.Expiry == callPosition.PositionContract.Expiry && m_PositionList[k].PositionContract.Right == ContractRight.Put)
                                {
                                    putPosition = m_PositionList[k];
                                    for (int i = 0; i < m_ContractList.Length / 2; i++)
                                    {
                                        if (m_ReportList[i * 2 + 1][0].MdContract.Strike == callPosition.PositionContract.Strike)//m_Reports[i * 2 + 1]是Put
                                        {
                                            putBidAskOffset = m_ReportList[i * 2 + 1][0].AskPrice - m_ReportList[i * 2 + 1][0].BidPrice;
                                            break;
                                        }
                                    }
                                    break;
                                }
                            }
                            if (callBidAskOffset < putBidAskOffset)
                            {
                                m_BasePostionRight = ContractRight.Put;
                                break;
                            }
                        }
                    }
                }
            }
            else
            {
                sellCallEntrust = GetSellCallEntrust();
                if (sellCallEntrust == null)
                {
                    buyCallEntrust = GetBuyCallEntrust();
                }
                //当不存在单腿且不用开新仓时，通过持仓品种的买卖价差确定基准持仓方向
                if (sellCallEntrust == null && buyCallEntrust == null && m_BaseMktQty <= totalMktQty)
                {
                    for (int j = 0; j < m_PositionList.Count; j++)
                    {
                        Position putPosition = m_PositionList[j];
                        if (putPosition.PositionContract.Right == ContractRight.Put && putPosition.PositionNum > 0)
                        {
                            //遍历得到Put的买卖价差
                            double putBidAskOffset = double.MaxValue;
                            for (int i = 0; i < m_ContractList.Length / 2; i++)
                            {
                                if (m_ReportList[i * 2 + 1][0].MdContract.Strike == putPosition.PositionContract.Strike)//m_Reports[i * 2 + 1]是Put
                                {
                                    putBidAskOffset = m_ReportList[i * 2 + 1][0].AskPrice - m_ReportList[i * 2 + 1][0].BidPrice;
                                    break;
                                }
                            }
                            //寻找Put持仓对应的Call持仓并遍历得到Call的买卖价差
                            Position callPosition = null;
                            double callBidAskOffset = double.MaxValue;
                            for (int k = 0; k < m_PositionList.Count; k++)
                            {
                                if (m_PositionList[k].PositionContract.Strike == putPosition.PositionContract.Strike && m_PositionList[k].PositionContract.Expiry == putPosition.PositionContract.Expiry && m_PositionList[k].PositionContract.Right == ContractRight.Call)
                                {
                                    callPosition = m_PositionList[k];
                                    for (int i = 0; i < m_ContractList.Length / 2; i++)
                                    {
                                        if (m_ReportList[i * 2][0].MdContract.Strike == putPosition.PositionContract.Strike)//m_Reports[i * 2]是Call
                                        {
                                            callBidAskOffset = m_ReportList[i * 2][0].AskPrice - m_ReportList[i * 2][0].BidPrice;
                                            break;
                                        }
                                    }
                                    break;
                                }
                            }
                            if (putBidAskOffset < callBidAskOffset)
                            {
                                m_BasePostionRight = ContractRight.Call;
                                break;
                            }
                        }
                    }
                }
            }

            #endregion

            Entrust[] entrusts = null;//轮动的买卖Call订单
            if (sellPutEntrust == null && buyPutEntrust == null && sellCallEntrust == null && buyCallEntrust == null)
            {
                if (m_BasePostionRight == ContractRight.Call)
                {
                    entrusts = GetCallOrders(optValues, maxValueIndex);
                }
                else
                {
                    entrusts = GetPutOrders(optValues, maxValueIndex);
                }
                if (m_BaseMktQty > totalMktQty)//需要开仓
                {
                    int offsetPosition = m_BaseMktQty - totalMktQty;
                    if(entrusts == null)
                    {
                        entrusts = new Entrust[1];
                        entrusts[0] = new Entrust();
                        Order entrustOrder = new Order();
                        entrustOrder.OrderType = "LMT";
                        entrustOrder.TotalQuantity = offsetPosition;
                        entrustOrder.Account = StrategyPool.Account;
                        if (m_BasePostionRight == ContractRight.Call)
                        {
                            entrustOrder.Action = OrderActionType.Buy;
                            entrustOrder.LmtPrice = m_ReportList[maxValueIndex * 2][0].AskPrice;
                            entrusts[0].EntrustContract = m_ReportList[maxValueIndex * 2][0].MdContract;
                        }
                        else
                        {
                            entrustOrder.Action = OrderActionType.Sell;
                            entrustOrder.LmtPrice = m_ReportList[maxValueIndex * 2 + 1][0].BidPrice;
                            entrusts[0].EntrustContract = m_ReportList[maxValueIndex * 2 + 1][0].MdContract;
                        }
                        entrusts[0].EntrustOrder = entrustOrder;
                        
                    }
                    Log.WriteLine(DateTime.Now.ToString() + " 买入行权价  " + m_ReportList[maxValueIndex * 2][0].MdContract.Strike + "   买入C价格  " + m_ReportList[maxValueIndex * 2][0].AskPrice + "   卖出P价格  " + m_ReportList[maxValueIndex * 2 + 1][0].BidPrice + " 价值  " + maxValue);
                }
            }

            if (sellPutEntrust != null || buyPutEntrust != null || sellCallEntrust != null || buyCallEntrust != null || entrusts != null)
            {
                if (m_OrderReportList.Count > 0)
                {
                    CancelQueueingOrder(sellPutEntrust, buyPutEntrust,sellCallEntrust,buyCallEntrust, entrusts);
                }
                else
                {
                    if (sellPutEntrust != null)
                    {
                        ReqOrderInsert(m_StrategyPool.GetValidId(), sellPutEntrust.EntrustContract, sellPutEntrust.EntrustOrder);
                    }
                    else if (buyPutEntrust != null)
                    {
                        ReqOrderInsert(m_StrategyPool.GetValidId(), buyPutEntrust.EntrustContract, buyPutEntrust.EntrustOrder);
                    }
                    else if (sellCallEntrust != null)
                    {
                        ReqOrderInsert(m_StrategyPool.GetValidId(), sellCallEntrust.EntrustContract, sellCallEntrust.EntrustOrder);
                    }
                    else if (buyCallEntrust != null)
                    {
                        ReqOrderInsert(m_StrategyPool.GetValidId(), buyCallEntrust.EntrustContract, buyCallEntrust.EntrustOrder);
                    }
                    else if (entrusts != null)
                    {
                        if (m_BaseMktQty > totalMktQty)//需要开仓
                        {
                            for (int i = 0; i < entrusts.Length; i++)
                            {
                                if ((m_BasePostionRight == ContractRight.Call && entrusts[i].EntrustOrder.Action == OrderActionType.Buy)
                                    || (m_BasePostionRight == ContractRight.Put && entrusts[i].EntrustOrder.Action == OrderActionType.Sell))
                                {
                                    ReqOrderInsert(m_StrategyPool.GetValidId(), entrusts[i].EntrustContract, entrusts[i].EntrustOrder);
                                    break;
                                }
                            }
                        }
                        else//需要平仓
                        {
                            for (int i = 0; i < entrusts.Length; i++)
                            {
                                if ((m_BasePostionRight == ContractRight.Call && entrusts[i].EntrustOrder.Action == OrderActionType.Sell)
                                    || (m_BasePostionRight == ContractRight.Put && entrusts[i].EntrustOrder.Action == OrderActionType.Buy))

                                {
                                    ReqOrderInsert(m_StrategyPool.GetValidId(), entrusts[i].EntrustContract, entrusts[i].EntrustOrder);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                CancelAllQueueingOrder();
            }
            //通过卖出对手价计算持仓对应的平价价格，如果可以轮动则轮动
            //if (tempPositionIndex != minValueIndex)
            //{
            //    if (m_Reports[tempPositionIndex * 2].BidPrice != 0 && m_Reports[tempPositionIndex * 2 + 1].AskPrice != 0)
            //    {
            //        double positionValue = m_Reports[tempPositionIndex * 2 + 1].AskPrice - m_Reports[tempPositionIndex * 2].BidPrice - m_ContractList[tempPositionIndex * 2].Strike;
            //        if (positionValue > minValue)
            //        {
            //            Log.WriteLine(DateTime.Now.ToString() +  "  卖出持仓行权价 " + m_ContractList[tempPositionIndex*2].Strike + "   卖出平价价格    " + positionValue + " 买入行权价  " + m_ContractList[minValueIndex * 2].Strike + "   买入平价价格  " + minValue);
            //            tempPositionIndex = minValueIndex;
            //        }
            //    }
            //}
        }

        /// <summary>
        ///  通过文本初始化策略参数
        /// </summary>
        /// <returns></returns>
        protected override void InitStrategyParm()
        {
            string filePath = System.Windows.Forms.Application.StartupPath + @"\" + m_StrategyName + @"\Strategy.txt";
            if (File.Exists(filePath))
            {
                TextFile textFile = new TextFile("\t");
                textFile.OpenFile(filePath);
                StringAnalyse tokens = textFile.GetElementAt(0);
                m_BaseMktQty = Convert.ToInt32(tokens.GetElementAt(0));

                tokens = textFile.GetElementAt(1);
                m_WheeledThreshold = Convert.ToDouble(tokens.GetElementAt(0));
            }
        }

        protected override void ReqMktData()
        {
            m_ContractList = new Contract[24];

            m_ContractList[0] = GetOptionK200Call(232.5);
            m_ContractList[1] = GetOptionK200Put(232.5);
            m_ContractList[2] = GetOptionK200Call(235);
            m_ContractList[3] = GetOptionK200Put(235);
            m_ContractList[4] = GetOptionK200Call(237.5);
            m_ContractList[5] = GetOptionK200Put(237.5);
            m_ContractList[6] = GetOptionK200Call(240);
            m_ContractList[7] = GetOptionK200Put(240);
            m_ContractList[8] = GetOptionK200Call(242.5);
            m_ContractList[9] = GetOptionK200Put(242.5);
            m_ContractList[10] = GetOptionK200Call(245);
            m_ContractList[11] = GetOptionK200Put(245);
            m_ContractList[12] = GetOptionK200Call(247.5);
            m_ContractList[13] = GetOptionK200Put(247.5);
            m_ContractList[14] = GetOptionK200Call(250);
            m_ContractList[15] = GetOptionK200Put(250);
            m_ContractList[16] = GetOptionK200Call(252.5);
            m_ContractList[17] = GetOptionK200Put(252.5);
            m_ContractList[18] = GetOptionK200Call(255);
            m_ContractList[19] = GetOptionK200Put(255);
            m_ContractList[20] = GetOptionK200Call(257.5);
            m_ContractList[21] = GetOptionK200Put(257.5);
            m_ContractList[22] = GetOptionK200Call(260);
            m_ContractList[23] = GetOptionK200Put(260);

            for (int i = 0; i < m_ContractList.Length; i++)
            {
                m_StrategyPool.ClientSocket.reqMktData(m_StartTickID + i, m_ContractList[i], "", false, new List<TagValue>());
            }
        }

        /// <summary>
        /// 通过行情缓冲区更新行情数组
        /// </summary>
        protected override void UpdateMdReport()
        {
            for (int i = 0; i < m_ReportBuffers.Length; i++)
            {
                if (m_ReportList[i].Count == 0)
                {
                    m_ReportBuffers[i].MdContract = m_ContractList[i];
                    m_ReportList[i].Add(m_ReportBuffers[i]);
                }
                else
                {
                    m_ReportBuffers[i].MdContract = m_ContractList[i];
                    m_ReportList[i][0] = m_ReportBuffers[i];
                }
            }
        }

        /// <summary>
        /// 根据C的持仓获取P的追卖量
        /// </summary>
        /// <returns></returns>
        private Entrust GetSellPutEntrust()
        {
            Entrust curEntrust = null;
            //判断是否存在C单腿的品种,有则需要追卖P
            for (int i = 0; i < m_PositionList.Count; i++)
            {
                Position callPosition = m_PositionList[i];
                if (callPosition.PositionContract.Right == ContractRight.Call)
                {
                    //寻找Call持仓对应的Put持仓
                    Position putPosition = null;
                    for (int k = 0; k < m_PositionList.Count; k++)
                    {
                        if (m_PositionList[k].PositionContract.Strike == callPosition.PositionContract.Strike && m_PositionList[k].PositionContract.Expiry == callPosition.PositionContract.Expiry && m_PositionList[k].PositionContract.Right == ContractRight.Put)
                        {
                            putPosition = m_PositionList[k];

                            int offsetAB = putPosition.PositionNum + callPosition.PositionNum;
                            
                            if (offsetAB > 0)
                            {
                                curEntrust = new Entrust();
                                curEntrust.EntrustContract = putPosition.PositionContract;
                                Order order = new Order();
                                order.Action = "SELL";
                                order.OrderType = "LMT";
                                order.TotalQuantity = offsetAB;
                                order.Account = StrategyPool.Account;
                                curEntrust.EntrustOrder = order;
                                //遍历得到委托价格
                                for (int j = 0; j < m_ContractList.Length; j++)
                                {
                                    if (m_ReportList[j][0].BidPrice > 0 && m_ReportList[j][0].MdContract.Strike == putPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == callPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Put)
                                    {
                                        order.LmtPrice = m_ReportList[j][0].BidPrice;
                                        break;
                                    }
                                }
                            }
                            break;
                        }         
                    }
                    if (putPosition == null)//无对应的Put持仓
                    {
                        int offsetAB = callPosition.PositionNum;

                        if (offsetAB > 0)
                        {
                            //遍历得到委托价格，合约等信息
                            for (int j = 0; j < m_ContractList.Length; j++)
                            {
                                if (m_ReportList[j][0].BidPrice > 0 && m_ReportList[j][0].MdContract.Strike == callPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == callPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Put)
                                {
                                    curEntrust = new Entrust();
                                    curEntrust.EntrustContract = m_ReportList[j][0].MdContract;
                                    Order order = new Order();
                                    order.Action = "SELL";
                                    order.OrderType = "LMT";
                                    order.TotalQuantity = offsetAB;
                                    order.Account = StrategyPool.Account;
                                    order.LmtPrice = m_ReportList[j][0].BidPrice;
                                    curEntrust.EntrustOrder = order;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return curEntrust;
        }

        /// <summary>
        /// 根据C的持仓获取P的追买量
        /// </summary>
        /// <returns></returns>
        private Entrust GetBuyPutEntrust()
        {
            Entrust curEntrust = null;
            //判断是否存在C单腿的品种,有则需要追买P
            for (int i = 0; i < m_PositionList.Count; i++)
            {
                Position callPosition = m_PositionList[i];
                if (callPosition.PositionContract.Right == ContractRight.Call)
                {
                    //寻找Call持仓对应的Put持仓
                    for (int k = 0; k < m_PositionList.Count; k++)
                    {
                        Position putPosition = m_PositionList[k];
                        if (putPosition.PositionContract.Strike == callPosition.PositionContract.Strike && putPosition.PositionContract.Expiry == callPosition.PositionContract.Expiry && putPosition.PositionContract.Right == ContractRight.Put)
                        {
                            //计算AB差值时
                            int offsetAB = Math.Abs(putPosition.PositionNum) - callPosition.PositionNum;

                            if (offsetAB > 0)
                            {
                                curEntrust = new Entrust();
                                curEntrust.EntrustContract = putPosition.PositionContract;
                                Order order = new Order();
                                order.Action = "BUY";
                                order.OrderType = "LMT";
                                order.TotalQuantity = offsetAB;
                                order.Account = StrategyPool.Account;

                                //遍历得到委托价格
                                for (int j = 0; j < m_ContractList.Length; j++)
                                {
                                    if (m_ReportList[j][0].AskPrice > 0 && m_ReportList[j][0].MdContract.Strike == putPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == callPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Put)
                                    {
                                        order.LmtPrice = m_ReportList[j][0].AskPrice;
                                        break;
                                    }
                                }
                                curEntrust.EntrustOrder = order;
                            }
                            break;
                        }
                    }
                }
            }
            return curEntrust;
        }

        /// <summary>
        /// 根据P的持仓获取C的追卖量
        /// </summary>
        /// <returns></returns>
        private Entrust GetSellCallEntrust()
        {
            Entrust curEntrust = null;
            //判断是否存在P单腿的品种,有则需要追卖C
            for (int i = 0; i < m_PositionList.Count; i++)
            {
                Position putPosition = m_PositionList[i];
                if (putPosition.PositionContract.Right == ContractRight.Put)
                {
                    //寻找Put持仓对应的Call持仓
                    Position callPosition = null;
                    for (int k = 0; k < m_PositionList.Count; k++)
                    {
                        if (m_PositionList[k].PositionContract.Strike == putPosition.PositionContract.Strike && m_PositionList[k].PositionContract.Expiry == putPosition.PositionContract.Expiry && m_PositionList[k].PositionContract.Right == ContractRight.Call)
                        {
                            callPosition = m_PositionList[k];

                            int offsetAB = callPosition.PositionNum - Math.Abs(putPosition.PositionNum);

                            if (offsetAB > 0)
                            {
                                curEntrust = new Entrust();
                                curEntrust.EntrustContract = callPosition.PositionContract;
                                Order order = new Order();
                                order.Action = OrderActionType.Sell;
                                order.OrderType = "LMT";
                                order.TotalQuantity = offsetAB;
                                order.Account = StrategyPool.Account;
                                curEntrust.EntrustOrder = order;
                                //遍历得到委托价格
                                for (int j = 0; j < m_ContractList.Length; j++)
                                {
                                    if (m_ReportList[j][0].BidPrice > 0 && m_ReportList[j][0].MdContract.Strike == callPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == putPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Call)
                                    {
                                        order.LmtPrice = m_ReportList[j][0].BidPrice;
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }
            return curEntrust;
        }

        /// <summary>
        /// 根据P的持仓获取C的追买量
        /// </summary>
        /// <returns></returns>
        private Entrust GetBuyCallEntrust()
        {
            Entrust curEntrust = null;
            //判断是否存在P单腿的品种,有则需要追买Call
            for (int i = 0; i < m_PositionList.Count; i++)
            {
                Position putPosition = m_PositionList[i];
                if (putPosition.PositionContract.Right == ContractRight.Put)
                {
                    Position callPosition = null;
                    //寻找Put持仓对应的Call持仓
                    for (int k = 0; k < m_PositionList.Count; k++)
                    {
                        callPosition = m_PositionList[k];
                        if (callPosition.PositionContract.Strike == putPosition.PositionContract.Strike && callPosition.PositionContract.Expiry == putPosition.PositionContract.Expiry && callPosition.PositionContract.Right == ContractRight.Call)
                        {
                            //计算AB差值时
                            int offsetAB = Math.Abs(putPosition.PositionNum) - callPosition.PositionNum;

                            if (offsetAB > 0)
                            {
                                curEntrust = new Entrust();
                                curEntrust.EntrustContract = callPosition.PositionContract;
                                Order order = new Order();
                                order.Action = OrderActionType.Buy;
                                order.OrderType = "LMT";
                                order.TotalQuantity = offsetAB;
                                order.Account = StrategyPool.Account;

                                //遍历得到委托价格
                                for (int j = 0; j < m_ContractList.Length; j++)
                                {
                                    if (m_ReportList[j][0].AskPrice > 0 && m_ReportList[j][0].MdContract.Strike == callPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == putPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Put)
                                    {
                                        order.LmtPrice = m_ReportList[j][0].AskPrice;
                                        break;
                                    }
                                }
                                curEntrust.EntrustOrder = order;
                            }
                            break;
                        }
                    }
                    if (callPosition == null)//无对应的Call持仓
                    {
                        int offsetAB = Math.Abs(putPosition.PositionNum);

                        if (offsetAB > 0)
                        {
                            //遍历得到委托价格，合约等信息
                            for (int j = 0; j < m_ContractList.Length; j++)
                            {
                                if (m_ReportList[j][0].AskPrice > 0 && m_ReportList[j][0].MdContract.Strike == putPosition.PositionContract.Strike && m_ReportList[j][0].MdContract.Expiry == putPosition.PositionContract.Expiry && m_ReportList[j][0].MdContract.Right == ContractRight.Call)
                                {
                                    curEntrust = new Entrust();
                                    curEntrust.EntrustContract = m_ReportList[j][0].MdContract;
                                    Order order = new Order();
                                    order.Action = OrderActionType.Buy;
                                    order.OrderType = "LMT";
                                    order.TotalQuantity = offsetAB;
                                    order.Account = StrategyPool.Account;
                                    order.LmtPrice = m_ReportList[j][0].AskPrice;
                                    curEntrust.EntrustOrder = order;
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return curEntrust;
        }

        /// <summary>
        /// 根据现有的委托信号确定是否取消活动委托单
        /// </summary>
        /// <param name="sellPutEntrust"></param>
        /// <param name="buyPutEntrust"></param>
        /// <param name="entrusts"></param>
        private void CancelQueueingOrder(Entrust sellPutEntrust, Entrust buyPutEntrust,Entrust sellCallEntrust,Entrust buyCallEntrust, Entrust[] entrusts)
        {
            if (sellPutEntrust != null || buyPutEntrust != null || sellCallEntrust != null || buyCallEntrust != null)
            {
                Entrust curEntrust = null;
                if (sellPutEntrust != null)
                {
                    curEntrust = sellPutEntrust;
                }
                else if (buyPutEntrust != null)
                {
                    curEntrust = buyPutEntrust;
                }
                else if (sellCallEntrust != null)
                {
                    curEntrust = sellCallEntrust;
                }
                else if (buyCallEntrust != null)
                {
                    curEntrust = buyCallEntrust;
                }
                for (int i = 0; i < m_OrderReportList.Count; i++)
                {
                    if (IsEqual(m_OrderReportList[i].OrderContract, curEntrust.EntrustContract))
                    {
                        if ((m_OrderReportList[i].CurOrder.Action == OrderActionType.Sell && m_OrderReportList[i].CurOrder.LmtPrice > curEntrust.EntrustOrder.LmtPrice)
                            || (m_OrderReportList[i].CurOrder.Action == OrderActionType.Buy && m_OrderReportList[i].CurOrder.LmtPrice < curEntrust.EntrustOrder.LmtPrice))
                        {
                            CancelOrder(m_OrderReportList[i].OrderId);
                        }
                    }
                    else
                    {
                        CancelOrder(m_OrderReportList[i].OrderId);
                    }
                }
            }
            else if (entrusts != null)
            {
                for (int i = 0; i < m_OrderReportList.Count; i++) 
                {
                    bool isContain = false;
                    for (int j = 0; j < entrusts.Length; j++)
                    {
                        if (IsEqual(m_OrderReportList[i].OrderContract, entrusts[j].EntrustContract))
                        {
                            isContain = true;
                            if ((m_OrderReportList[i].CurOrder.Action == OrderActionType.Sell && m_OrderReportList[i].CurOrder.LmtPrice > entrusts[j].EntrustOrder.LmtPrice)
                            || (m_OrderReportList[i].CurOrder.Action == OrderActionType.Buy && m_OrderReportList[i].CurOrder.LmtPrice < entrusts[j].EntrustOrder.LmtPrice))
                            {
                                CancelOrder(m_OrderReportList[i].OrderId);
                            }
                        }
                    }
                    if (!isContain)
                    {
                        CancelOrder(m_OrderReportList[i].OrderId);
                    }
                }
            }
        }

        /// <summary>
        /// 获取轮动买卖Call的订单,根据对价、排队价、挂单价依次判断
        /// </summary>
        /// <returns></returns>
        private Entrust[] GetCallOrders(double[] optValues, int maxValueIndex)
        {
            List<Entrust> entrustList = null;
            Entrust oneEntrust = null;
            Entrust twoEntrust = null;

            //遍历得到溢价最高的持仓
            Position closePosition = null;//卖出价值最低的持仓
            double closeValue1 = double.MaxValue;//对价平仓价值
            double closeValue2 = double.MaxValue;//排队价平仓价值
            double closeValue3 = double.MaxValue;//委托价平仓价值
            int closeIndex = -1;//低卖出价值对应的品种索引
            MdReport closeReport = null;
            double orderLmtPrice = 0; //记录需要平仓的持仓委托价格
            for (int j = 0; j < m_PositionList.Count; j++)
            {
                Position curPosition = m_PositionList[j];
                if (curPosition.PositionContract.Right == ContractRight.Call && curPosition.PositionNum > 0)
                {
                    for (int i = 0; i < m_ContractList.Length/2; i++)
                    {
                        if (m_ReportList[i * 2][0].MdContract.Strike == curPosition.PositionContract.Strike)//m_Reports[i * 2]是Call
                        {
                            double curValue = m_ReportList[i * 2 + 1][0].AskPrice - m_ReportList[i * 2][0].BidPrice - curPosition.PositionContract.Strike;
                            if (curValue < closeValue1)
                            {
                                closePosition = curPosition;
                                closeValue1 = curValue;
                                closeValue2 = m_ReportList[i * 2 + 1][0].AskPrice - m_ReportList[i * 2][0].AskPrice - curPosition.PositionContract.Strike;
                                closeIndex = i;
                                closeReport = m_ReportList[i * 2][0];
                                //如果活动委托中有此持仓，通过委托价算出平仓价值
                                for (int k = 0; k < m_OrderReportList.Count; k++)
                                {
                                    if (m_OrderReportList[k].OrderContract.Strike == curPosition.PositionContract.Strike && m_OrderReportList[k].OrderContract.Right == curPosition.PositionContract.Right)
                                    {
                                        closeValue3 = m_ReportList[i * 2 + 1][0].AskPrice - m_OrderReportList[k].CurOrder.LmtPrice - curPosition.PositionContract.Strike;
                                        orderLmtPrice = m_OrderReportList[k].CurOrder.LmtPrice;
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }

            if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue1 || optValues[maxValueIndex] - m_WheeledThreshold > closeValue2 || optValues[maxValueIndex] - m_WheeledThreshold > closeValue3)//可以轮动
            {
                oneEntrust = new Entrust();
                oneEntrust.EntrustContract = closePosition.PositionContract;
                Order oneOrder = new Order();
                oneOrder.Action = OrderActionType.Sell;
                oneOrder.OrderType = "LMT";
                oneOrder.TotalQuantity = closePosition.PositionNum;
                oneOrder.Account = StrategyPool.Account;
                double sellValue = closeValue1;
                if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue1)
                {
                    oneOrder.LmtPrice = closeReport.BidPrice;
                }
                else if(optValues[maxValueIndex] - m_WheeledThreshold > closeValue2)
                {
                    oneOrder.LmtPrice = closeReport.AskPrice;
                    sellValue = closeValue2;
                }
                else if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue3)
                {
                    oneOrder.LmtPrice = orderLmtPrice;
                    sellValue = closeValue3;
                }
                oneEntrust.EntrustOrder = oneOrder;

                twoEntrust = new Entrust();
                twoEntrust.EntrustContract = m_ReportList[maxValueIndex * 2][0].MdContract;
                Order twoOrder = new Order();
                twoOrder.Action = OrderActionType.Buy;
                twoOrder.OrderType = "LMT";
                twoOrder.TotalQuantity = closePosition.PositionNum;
                twoOrder.Account = StrategyPool.Account;
                twoOrder.LmtPrice = m_ReportList[maxValueIndex * 2][0].AskPrice;               
                twoEntrust.EntrustOrder = twoOrder;

                entrustList = new List<Entrust>();
                entrustList.Add(oneEntrust);
                entrustList.Add(twoEntrust);
                Log.WriteLine(DateTime.Now.ToString() + "  卖出持仓行权价 " + closePosition.PositionContract.Strike + "   卖出价格    " + oneOrder.LmtPrice + "   卖出价值    " + sellValue + " 买入行权价  " + m_ReportList[maxValueIndex * 2][0].MdContract.Strike + "   买入价格  " + m_ReportList[maxValueIndex * 2][0].AskPrice + "   买入价值  " + optValues[maxValueIndex]);
            }
            if (entrustList != null)
            {
                return entrustList.ToArray();
            }
            return null;
        }

        /// <summary>
        /// 获取轮动买卖Put的订单,根据对价、排队价、挂单价依次判断
        /// </summary>
        /// <returns></returns>
        private Entrust[] GetPutOrders(double[] optValues, int maxValueIndex)
        {
            List<Entrust> entrustList = null;
            Entrust oneEntrust = null;
            Entrust twoEntrust = null;

            //遍历得到溢价最高的持仓
            Position closePosition = null;//卖出价值最低的持仓
            double closeValue1 = double.MaxValue;//对价平仓价值
            double closeValue2 = double.MaxValue;//排队价平仓价值
            double closeValue3 = double.MaxValue;//委托价平仓价值
            int closeIndex = -1;//低卖出价值对应的品种索引
            MdReport closeReport = null;
            double orderLmtPrice = 0; //记录需要平仓的持仓委托价格

            for (int j = 0; j < m_PositionList.Count; j++)
            {
                Position curPosition = m_PositionList[j];
                if (curPosition.PositionContract.Right == ContractRight.Put && Math.Abs(curPosition.PositionNum) > 0)
                {
                    for (int i = 0; i < m_ContractList.Length / 2; i++)
                    {
                        if (m_ReportList[i * 2 + 1][0].MdContract.Strike == curPosition.PositionContract.Strike)//m_Reports[i * 2 + 1]是Put
                        {
                            double curValue = m_ReportList[i * 2 + 1][0].AskPrice - m_ReportList[i * 2][0].BidPrice - curPosition.PositionContract.Strike;
                            if (curValue < closeValue1)
                            {
                                closePosition = curPosition;
                                closeValue1 = curValue;
                                closeValue2 = m_ReportList[i * 2 + 1][0].BidPrice - m_ReportList[i * 2][0].BidPrice - curPosition.PositionContract.Strike;
                                closeIndex = i;
                                closeReport = m_ReportList[i * 2 + 1][0];
                                //如果活动委托中有此持仓，通过委托价算出平仓价值
                                for (int k = 0; k < m_OrderReportList.Count; k++)
                                {
                                    if (m_OrderReportList[k].OrderContract.Strike == curPosition.PositionContract.Strike && m_OrderReportList[k].OrderContract.Right == curPosition.PositionContract.Right)
                                    {
                                        closeValue3 = m_OrderReportList[k].CurOrder.LmtPrice - m_ReportList[i * 2][0].BidPrice - curPosition.PositionContract.Strike;
                                        orderLmtPrice = m_OrderReportList[k].CurOrder.LmtPrice;
                                        break;
                                    }
                                }
                            }
                            break;
                        }
                    }
                }
            }

            if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue1 || optValues[maxValueIndex] - m_WheeledThreshold > closeValue2 || optValues[maxValueIndex] - m_WheeledThreshold > closeValue3)//可以轮动
            {
                oneEntrust = new Entrust();
                oneEntrust.EntrustContract = closePosition.PositionContract;
                Order oneOrder = new Order();
                oneOrder.Action = OrderActionType.Buy;
                oneOrder.OrderType = "LMT";
                oneOrder.TotalQuantity = closePosition.PositionNum;
                oneOrder.Account = StrategyPool.Account;
                double sellValue = closeValue1;
                if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue1)
                {
                    oneOrder.LmtPrice = closeReport.AskPrice; ;
                }
                else if(optValues[maxValueIndex] - m_WheeledThreshold > closeValue2)
                {
                    oneOrder.LmtPrice = closeReport.BidPrice;
                    sellValue = closeValue2;
                }
                else if (optValues[maxValueIndex] - m_WheeledThreshold > closeValue3)
                {
                    oneOrder.LmtPrice = orderLmtPrice;
                    sellValue = closeValue2;
                }
                oneEntrust.EntrustOrder = oneOrder;

                twoEntrust = new Entrust();
                twoEntrust.EntrustContract = m_ReportList[maxValueIndex * 2][0].MdContract;
                Order twoOrder = new Order();
                twoOrder.Action = OrderActionType.Sell;
                twoOrder.OrderType = "LMT";
                twoOrder.TotalQuantity = closePosition.PositionNum;
                twoOrder.Account = StrategyPool.Account;
                twoOrder.LmtPrice = m_ReportList[maxValueIndex * 2][0].BidPrice;
                twoEntrust.EntrustOrder = twoOrder;

                entrustList = new List<Entrust>();
                entrustList.Add(oneEntrust);
                entrustList.Add(twoEntrust);
                Log.WriteLine(DateTime.Now.ToString() + "  卖出持仓行权价 " + closePosition.PositionContract.Strike + "   卖出价格    " + oneOrder.LmtPrice + "   卖出价值    " + sellValue + " 买入行权价  " + m_ReportList[maxValueIndex * 2][0].MdContract.Strike + "   买入价格  " + m_ReportList[maxValueIndex * 2][0].AskPrice + "   买入价值  " + optValues[maxValueIndex]);
            }
            if (entrustList != null)
            {
                return entrustList.ToArray();
            }
            return null;
        }

        private Contract GetOptionK200Call(double strike)
        {
            Contract contract = new Contract();
            contract.Symbol = "K200";
            contract.SecType = "OPT";
            contract.Exchange = "KSE";
            contract.Currency = "KRW";
            contract.Expiry = "20160714";
            contract.Strike = strike;
            contract.Right = ContractRight.Call;
            contract.Multiplier = "500000";
            return contract;
        }

        private Contract GetOptionK200Put(double strike)
        {
            Contract contract = new Contract();
            contract.Symbol = "K200";
            contract.SecType = "OPT";
            contract.Exchange = "KSE";
            contract.Currency = "KRW";
            contract.Expiry = "20160714";
            contract.Strike = strike;
            contract.Right = ContractRight.Put;
            contract.Multiplier = "500000";
            return contract;
        }

    }
}
