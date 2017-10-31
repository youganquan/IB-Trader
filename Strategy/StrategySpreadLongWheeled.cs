using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TextTool;
using IBApi;
using TWS.Data;
using System.Threading;

namespace TWS.Strategy
{
    /// <summary>
    /// 跨期单边多头轮动
    /// </summary>
    public class StrategySpreadLongWheeled : BaseStrategy
    {
        /// <summary>
        /// 基准持仓数，默认为1
        /// </summary>
        int m_BasePosition = 1;

        /// <summary>
        /// 记录最小价格变动单位，默认0.2
        /// </summary>
        double minPriceChange = 0.2;

        /// <summary>
        /// 轮动价差
        /// </summary>
        double m_WheeledPriceDis = 5;

        /// <summary>
        /// 基准价差，第一合约减第二合约，通过价差与轮动价差比例控制轮动。
        /// </summary>
        double m_BasePriceDis = 32;

        public StrategySpreadLongWheeled(string strategyName, StrategyPool strategyPool, int startTickID)
            : base(strategyName, strategyPool, startTickID)
        {
        }

        protected override void RunStrategy()
        {
            #region 计算总持仓手数
            //持仓总手数
            int totalMktQty = 0;

            //各个行权价的持仓
            int[] mktQty = new int[m_ContractList.Length];
            for (int i = 0; i < m_ContractList.Length; i++)
            {
                for (int j = 0; j < m_PositionList.Count; j++)
                {
                    Position curPosition = m_PositionList[j];
                    if (curPosition.PositionContract.Expiry == m_ContractList[i].Expiry)
                    {
                        int postionNum = Math.Abs(curPosition.PositionNum);
                        mktQty[i] += postionNum;
                        totalMktQty += postionNum;
                    }
                }
            }
            #endregion

            #region 持仓异常检测,如有持仓异常，中断程序执行
            if (totalMktQty > m_BasePosition)
            {
                Log.WriteLine("持仓异常,持仓数超过最大持仓值:" + totalMktQty);
                Thread.Sleep(60000);
                return;
            }
            #endregion
            if (m_ReportList[0].Count > 0  && m_ReportList[0][0].AskPrice != 0 && m_ReportList[0][0].BidPrice != 0
                && m_ReportList[1].Count > 0 && m_ReportList[1][0].AskPrice != 0 && m_ReportList[1][0].BidPrice != 0)
            {
                Entrust[] entrustList = GetEntrustList();

                if (totalMktQty == m_BasePosition)//满仓状态下只处理平仓信号
                {
                    if (m_OrderReportList.Count > 0)
                    {
                        CancelQueueingOrder(entrustList);
                    }
                    else
                    {
                        DateTime curTime = m_ReportList[0][0].Time;
                        //在交易的最后1分钟，禁止减仓
                        if (curTime.Hour != 16 || (curTime.Hour == 16 && curTime.Minute < 29))
                        {      
                            //只平仓
                            if (entrustList != null)
                            {
                                Entrust curOrder = entrustList[0];
                                for (int i = 0; i < m_PositionList.Count; i++)
                                {
                                    if (m_PositionList[i].PositionContract.Expiry == curOrder.EntrustContract.Expiry)
                                    {
                                        ReqOrderInsert(m_StrategyPool.GetValidId(), curOrder.EntrustContract, curOrder.EntrustOrder);
                                        Log.WriteStrategySignal(m_StrategyName, DateTime.Now.ToString() + "  卖出品种  " + entrustList[0].EntrustContract.Expiry
                                        + "  卖出价格   " + entrustList[0].EntrustOrder.LmtPrice + "  买入价格  " + entrustList[1].EntrustOrder.LmtPrice);

                                        break;
                                    }
                                }   
                            }
                        }
                    }
                }
                else if (totalMktQty < m_BasePosition)//仓位不够时生成开仓信号
                {
                    if (m_OrderReportList.Count > 0)
                    {
                        CancelQueueingOrder(entrustList);
                    }
                    else
                    {
                        if (entrustList != null)
                        {
                            Entrust curOrder = entrustList[1];
                            ReqOrderInsert(m_StrategyPool.GetValidId(), curOrder.EntrustContract, curOrder.EntrustOrder);
                            Log.WriteStrategySignal(m_StrategyName, DateTime.Now.ToString() + "  卖出品种  " + entrustList[0].EntrustContract.Expiry
                                            + "  卖出价格   " + entrustList[0].EntrustOrder.LmtPrice + "  买入价格  " + entrustList[1].EntrustOrder.LmtPrice);
                        }
                                        
                    }
                }
            }
        }


        /// <summary>
        /// 生成买卖订单
        /// </summary>
        /// <returns>返回买卖信息,先卖后买</returns>
        private Entrust[] GetEntrustList()
        {
            Entrust[] entrustList = null;
            Entrust oneEntrust = null;
            Entrust twoEntrust = null;

            double curDis = m_ReportList[0][0].BidPrice - m_ReportList[1][0].AskPrice;
            if (curDis - m_BasePriceDis >= m_WheeledPriceDis)//卖一号合约买二号合约条件满足
            {
                oneEntrust = new Entrust();
                oneEntrust.EntrustContract = m_ReportList[0][0].MdContract;
                Order oneOrder = new Order();
                oneOrder.Action = OrderActionType.Sell;
                oneOrder.OrderType = "LMT";
                oneOrder.Account = StrategyPool.Account;
                oneOrder.LmtPrice = m_ReportList[0][0].BidPrice;
                oneOrder.TotalQuantity = 1;
                oneEntrust.EntrustOrder = oneOrder;

                twoEntrust = new Entrust();
                twoEntrust.EntrustContract = m_ReportList[1][0].MdContract;
                Order twoOrder = new Order();
                twoOrder.Action = OrderActionType.Buy;
                twoOrder.OrderType = "LMT";
                twoOrder.Account = StrategyPool.Account;
                twoOrder.LmtPrice = m_ReportList[1][0].AskPrice;
                twoOrder.TotalQuantity = 1;
                twoEntrust.EntrustOrder = twoOrder;

                entrustList = new Entrust[2];
                entrustList[0] = oneEntrust;
                entrustList[1] = twoEntrust;
            }
            else
            {
                curDis = m_ReportList[0][0].AskPrice - m_ReportList[1][0].BidPrice;
                if (m_BasePriceDis - curDis >= m_WheeledPriceDis)//卖二号合约买一号合约条件满足
                {
                    oneEntrust = new Entrust();
                    oneEntrust.EntrustContract = m_ReportList[1][0].MdContract;
                    Order oneOrder = new Order();
                    oneOrder.Action = OrderActionType.Sell;
                    oneOrder.OrderType = "LMT";
                    oneOrder.Account = StrategyPool.Account;
                    oneOrder.LmtPrice = m_ReportList[1][0].BidPrice;
                    oneOrder.TotalQuantity = 1;
                    oneEntrust.EntrustOrder = oneOrder;

                    twoEntrust = new Entrust();
                    twoEntrust.EntrustContract = m_ReportList[0][0].MdContract;
                    Order twoOrder = new Order();
                    twoOrder.Action = OrderActionType.Buy;
                    twoOrder.OrderType = "LMT";
                    twoOrder.Account = StrategyPool.Account;
                    twoOrder.LmtPrice = m_ReportList[0][0].AskPrice;
                    twoOrder.TotalQuantity = 1;
                    twoEntrust.EntrustOrder = twoOrder;

                    entrustList = new Entrust[2];
                    entrustList[0] = oneEntrust;
                    entrustList[1] = twoEntrust;
                }
            }

            return entrustList;
        }

        /// <summary>
        /// 从文本获取合约对应的参数,并请求行情数据
        /// </summary>
        protected override void ReqMktData()
        {
            string filePath = System.Windows.Forms.Application.StartupPath + @"\" + m_StrategyName + @"\Instrument.txt";
            if (File.Exists(filePath))
            {
                TextFile textFile = new TextFile(",");
                textFile.OpenFile(filePath);

                string[] instruments = new string[2];

                StringAnalyse tokens = textFile.GetElementAt(0);
                instruments[0] = tokens.GetElementAt(0);

                tokens = textFile.GetElementAt(1);
                instruments[1] = tokens.GetElementAt(0);

                m_ContractList = new Contract[instruments.Length];

                for (int i = 0; i < instruments.Length; i++)
                {
                    m_ContractList[i] = GetMChHk(instruments[i]);
                }

                for (int i = 0; i < m_ContractList.Length; i++)
                {
                    m_StrategyPool.ClientSocket.reqMktData(m_StartTickID + i, m_ContractList[i], "", false, new List<TagValue>());
                }
            }

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
                TextFile textFile = new TextFile(",");
                textFile.OpenFile(filePath);

                string[] instruments = new string[2];

                StringAnalyse tokens = textFile.GetElementAt(0);
                m_BasePosition = Convert.ToInt32(tokens.GetElementAt(0));

                tokens = textFile.GetElementAt(1);
                m_BasePriceDis = Convert.ToDouble(tokens.GetElementAt(0));

                tokens = textFile.GetElementAt(2);
                m_WheeledPriceDis = Convert.ToDouble(tokens.GetElementAt(0));                   
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

        private Contract GetMChHk(string expiry)
        {
            Contract contract = new Contract();
            contract.Symbol = "MCH.HK";
            contract.SecType = "FUT";
            contract.Exchange = "HKFE";
            contract.Currency = "HKD";
            contract.Expiry = expiry;
            contract.Multiplier = "10";
            return contract;
        }

        /// <summary>
        /// 根据现有的委托信号确定是否取消活动委托单
        /// </summary>
        /// <param name="entrusts"></param>
        private void CancelQueueingOrder(Entrust[] entrusts)
        {
            if (entrusts != null)
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
    }
}
