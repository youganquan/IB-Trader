using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IBApi;
using TWS.Strategy;
using System.Threading;
using System.IO;
using TextTool;
using TWS.Data;

namespace TWS
{
    /// <summary>
    /// 策略池，负责各个策略的管理与信息协调
    /// 封装TWS发送与接收数据。
    /// 通过一系列回调函数接收数据；通过EClientSocket发送数据
    /// </summary>
    public class StrategyPool : EWrapper
    {
        /// <summary>
        /// 每个策略行情ID的区间
        /// </summary>
        public const int TICK_ID_REGION = 10000;
        public static string Account = "DU...";//此处需要改成自己的账号
        /// <summary>
        /// 用于存储策略
        /// </summary>
        List<BaseStrategy> strategyList;

        EClientSocket clientSocket;
        public EClientSocket ClientSocket
        {
            get { return clientSocket; }
            set { clientSocket = value; }
        }

        /// <summary>
        /// 委托下单锁
        /// </summary>
        object reqOrderInsertLock = new object();

        /// <summary>
        /// 撤单锁
        /// </summary>
        object cancelOrderLock = new object();

        /// <summary>
        /// 编号锁
        /// </summary>
        object validIdLock = new object();

        private int validId;
        /// <summary>
        /// 请求订单唯一编号
        /// </summary>
        /// <returns></returns>
        public int GetValidId()
        {
            lock (validIdLock)
            {
                return validId++;
            }
        }

        /// <summary>
        /// 初始化策略池，参数用于初始化交易线程
        /// </summary>
        public StrategyPool()
        {
            clientSocket = new EClientSocket(this);
            clientSocket.eConnect("127.0.0.1", 7496, 0, false);

            /*************************************************************************************************************************************************/
            /* One good way of knowing if we can proceed is by monitoring the order's nextValidId reception which comes down automatically after connecting. */
            /*************************************************************************************************************************************************/
            while (validId <= 0) { }

            InitStrategy();
            clientSocket.reqPositions();
        }


        /// <summary>
        /// 初始化策略
        /// </summary>
        private void InitStrategy()
        {
            strategyList = new List<BaseStrategy>();

            string filePath = System.Windows.Forms.Application.StartupPath + @"\OptionWheeled.txt";
            //if (File.Exists(filePath))
            //{
            //    TextFile textFile = new TextFile("\t");
            //    textFile.OpenFile(filePath);
            //    for (int i = 0; i < textFile.Count; i++)
            //    {
            //        StringAnalyse tokens = textFile.GetElementAt(i);
            //        InitOptionStrategy(tokens.GetElementAt(0));
            //    }
            //}

            filePath = System.Windows.Forms.Application.StartupPath + @"\SpreadLongWheeled.txt";
            if (File.Exists(filePath))
            {
                TextFile textFile = new TextFile("\t");
                textFile.OpenFile(filePath);
                for (int i = 0; i < textFile.Count; i++)
                {
                    StringAnalyse tokens = textFile.GetElementAt(i);
                    InitSpreadLongWheeled(tokens.GetElementAt(0));
                }
            }
        }

        /// <summary>
        /// 期权轮动
        /// </summary>
        private void InitOptionStrategy(string strategyName)
        {
            BaseStrategy strategy = new OptionWheeled(strategyName, this, strategyList.Count * TICK_ID_REGION);
            strategyList.Add(strategy);
        }

        private void InitSpreadLongWheeled(string strategyName)
        {
            BaseStrategy strategy = new StrategySpreadLongWheeled(strategyName, this, strategyList.Count * TICK_ID_REGION);
            strategyList.Add(strategy);
        }

        /// <summary>
        /// 提交限价委托
        /// </summary>
        public void ReqOrderInsert(int orderId, Contract contract, Order order)
        {
            lock (reqOrderInsertLock)
            {
                clientSocket.placeOrder(orderId, contract, order);
            }
        }

        /// <summary>
        /// 撤单
        /// </summary>
        public void CancelOrder(int orderID)
        {
            lock (cancelOrderLock)
            {
                clientSocket.cancelOrder(orderID);
            }
        }

        #region 实现EWrapper接口的相关方法

        public virtual void error(Exception e)
        {
            Console.WriteLine("Exception thrown: " + e);
            throw e;
        }

        public virtual void error(string str)
        {
            Console.WriteLine("Error: " + str + "\n");
        }

        public virtual void error(int id, int errorCode, string errorMsg)
        {
            Console.WriteLine("Error. Id: " + id + ", Code: " + errorCode + ", Msg: " + errorMsg + "\n");
        }

        public virtual void connectionClosed()
        {
            Console.WriteLine("Connection closed.\n");
        }

        public virtual void currentTime(long time)
        {
            Console.WriteLine("Current Time: " + time + "\n");
        }

        public virtual void tickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            for (int i = 0; i < strategyList.Count; i++)
            {
                strategyList[i].ReceiveTickPrice(tickerId, field, price, canAutoExecute);
            }
            //Console.WriteLine("Tick Price. Ticker Id:" + tickerId + ", Field: " + field + ", Price: " + price + ", CanAutoExecute: " + canAutoExecute + "\n");
        }

        public virtual void tickSize(int tickerId, int field, int size)
        {
            for (int i = 0; i < strategyList.Count; i++)
            {
                strategyList[i].ReceiveTickSize(tickerId, field, size);
            }
            //Console.WriteLine("Tick Size. Ticker Id:" + tickerId + ", Field: " + field + ", Size: " + size + "\n");
        }

        public virtual void tickString(int tickerId, int tickType, string value)
        {
            //Console.WriteLine("Tick string. Ticker Id:" + tickerId + ", Type: " + tickType + ", Value: " + value + "\n");
        }

        public virtual void tickGeneric(int tickerId, int field, double value)
        {
            //Console.WriteLine("Tick Generic. Ticker Id:" + tickerId + ", Field: " + field + ", Value: " + value + "\n");
        }

        public virtual void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureExpiry, double dividendImpact, double dividendsToExpiry)
        {
            Console.WriteLine("TickEFP. " + tickerId + ", Type: " + tickType + ", BasisPoints: " + basisPoints + ", FormattedBasisPoints: " + formattedBasisPoints + ", ImpliedFuture: " + impliedFuture + ", HoldDays: " + holdDays + ", FutureExpiry: " + futureExpiry + ", DividendImpact: " + dividendImpact + ", DividendsToExpiry: " + dividendsToExpiry + "\n");
        }

        public virtual void tickSnapshotEnd(int tickerId)
        {
            Console.WriteLine("TickSnapshotEnd: " + tickerId + "\n");
        }

        public virtual void nextValidId(int orderId)
        {
            Console.WriteLine("Next Valid Id: " + orderId + "\n");
            validId = orderId;
        }

        public virtual void deltaNeutralValidation(int reqId, UnderComp underComp)
        {
            Console.WriteLine("DeltaNeutralValidation. " + reqId + ", ConId: " + underComp.ConId + ", Delta: " + underComp.Delta + ", Price: " + underComp.Price + "\n");
        }

        public virtual void managedAccounts(string accountsList)
        {
            Console.WriteLine("Account list: " + accountsList + "\n");
        }

        public virtual void tickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            //Console.WriteLine("TickOptionComputation. TickerId: " + tickerId + ", field: " + field + ", ImpliedVolatility: " + impliedVolatility + ", Delta: " + delta
            //    + ", OptionPrice: " + optPrice + ", pvDividend: " + pvDividend + ", Gamma: " + gamma + ", Vega: " + vega + ", Theta: " + theta + ", UnderlyingPrice: " + undPrice + "\n");
        }

        public virtual void accountSummary(int reqId, string account, string tag, string value, string currency)
        {
            Console.WriteLine("Acct Summary. ReqId: " + reqId + ", Acct: " + account + ", Tag: " + tag + ", Value: " + value + ", Currency: " + currency + "\n");
        }

        public virtual void accountSummaryEnd(int reqId)
        {
            Console.WriteLine("AccountSummaryEnd. Req Id: " + reqId + "\n");
        }

        public virtual void updateAccountValue(string key, string value, string currency, string accountName)
        {
            Console.WriteLine("UpdateAccountValue. Key: " + key + ", Value: " + value + ", Currency: " + currency + ", AccountName: " + accountName + "\n");
        }

        public virtual void updatePortfolio(Contract contract, int position, double marketPrice, double marketValue, double averageCost, double unrealisedPNL, double realisedPNL, string accountName)
        {
            Console.WriteLine("UpdatePortfolio. " + contract.Symbol + ", " + contract.SecType + " @ " + contract.Exchange
                + ": Position: " + position + ", MarketPrice: " + marketPrice + ", MarketValue: " + marketValue + ", AverageCost: " + averageCost
                + ", UnrealisedPNL: " + unrealisedPNL + ", RealisedPNL: " + realisedPNL + ", AccountName: " + accountName + "\n");
        }

        public virtual void updateAccountTime(string timestamp)
        {
            Console.WriteLine("UpdateAccountTime. Time: " + timestamp + "\n");
        }

        public virtual void accountDownloadEnd(string account)
        {
            Console.WriteLine("Account download finished: " + account + "\n");
        }

        public virtual void orderStatus(int orderId, string status, int filled, int remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld)
        {
            for (int i = 0; i < strategyList.Count; i++)
            {
                strategyList[i].ReceiveOrderStatus(orderId, status, filled, remaining, avgFillPrice, permId, parentId, lastFillPrice, clientId, whyHeld);
            }
            Console.WriteLine("OrderStatus. Id: " + orderId + ", Status: " + status + ", Filled" + filled + ", Remaiint orderId, string status, int filled, int remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld)ning: " + remaining
                + ", AvgFillPrice: " + avgFillPrice + ", PermId: " + permId + ", ParentId: " + parentId + ", LastFillPrice: " + lastFillPrice + ", ClientId: " + clientId + ", WhyHeld: " + whyHeld + "\n");
        }

        public virtual void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            for (int i = 0; i < strategyList.Count; i++)
            {
                strategyList[i].ReceiveOpenOrder(orderId, contract, order, orderState);
            }
            Console.WriteLine("OpenOrder. ID: " + orderId + ", " + contract.Symbol + ", " + contract.SecType + " @ " + contract.Exchange + ": " + order.Action + ", " + order.OrderType + " " + order.TotalQuantity + ", " + orderState.Status + "\n");

        }

        public virtual void openOrderEnd()
        {
            Console.WriteLine("OpenOrderEnd");
        }

        public virtual void contractDetails(int reqId, ContractDetails contractDetails)
        {
            Console.WriteLine("ContractDetails. ReqId: " + reqId + " - " + contractDetails.Summary.Symbol + ", " + contractDetails.Summary.SecType + ", ConId: " + contractDetails.Summary.ConId + " @ " + contractDetails.Summary.Exchange + "\n");
        }

        public virtual void contractDetailsEnd(int reqId)
        {
            Console.WriteLine("ContractDetailsEnd. " + reqId + "\n");
        }

        public virtual void execDetails(int reqId, Contract contract, Execution execution)
        {
            Console.WriteLine("ExecDetails. " + reqId + " - " + contract.Symbol + ", " + contract.SecType + ", " + contract.Currency + " - " + execution.ExecId + ", " + execution.OrderId + ", " + execution.Shares + "\n");
        }

        public virtual void execDetailsEnd(int reqId)
        {
            Console.WriteLine("ExecDetailsEnd. " + reqId + "\n");
        }

        public virtual void commissionReport(CommissionReport commissionReport)
        {
            Console.WriteLine("CommissionReport. " + commissionReport.ExecId + " - " + commissionReport.Commission + " " + commissionReport.Currency + " RPNL " + commissionReport.RealizedPNL + "\n");
        }

        public virtual void fundamentalData(int reqId, string data)
        {
            Console.WriteLine("FundamentalData. " + reqId + "" + data + "\n");
        }

        public virtual void historicalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGaps)
        {
            Console.WriteLine("HistoricalData. " + reqId + " - Date: " + date + ", Open: " + open + ", High: " + high + ", Low: " + low + ", Close: " + close + ", Volume: " + volume + ", Count: " + count + ", WAP: " + WAP + ", HasGaps: " + hasGaps + "\n");
        }

        public virtual void marketDataType(int reqId, int marketDataType)
        {
            Console.WriteLine("MarketDataType. " + reqId + ", Type: " + marketDataType + "\n");
        }

        public virtual void updateMktDepth(int tickerId, int position, int operation, int side, double price, int size)
        {
            Console.WriteLine("UpdateMarketDepth. " + tickerId + " - Position: " + position + ", Operation: " + operation + ", Side: " + side + ", Price: " + price + ", Size" + size + "\n");
        }

        public virtual void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, int size)
        {
            Console.WriteLine("UpdateMarketDepthL2. " + tickerId + " - Position: " + position + ", Operation: " + operation + ", Side: " + side + ", Price: " + price + ", Size" + size + "\n");
        }


        public virtual void updateNewsBulletin(int msgId, int msgType, String message, String origExchange)
        {
            Console.WriteLine("News Bulletins. " + msgId + " - Type: " + msgType + ", Message: " + message + ", Exchange of Origin: " + origExchange + "\n");
        }

        public virtual void position(string account, Contract contract, int pos, double avgCost)
        {
            for (int i = 0; i < strategyList.Count; i++)
            {
                strategyList[i].ReceivePosition(account, contract, pos, avgCost);
            }
            Log.WriteLine("Position. " + account + " - Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + ", Currency: " + contract.Currency + ", Position: " + pos + ", Avg cost: " + avgCost + "\n");
        }

        public virtual void positionEnd()
        {
            Console.WriteLine("PositionEnd \n");
        }

        public virtual void realtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {
            Console.WriteLine("RealTimeBars. " + reqId + " - Time: " + time + ", Open: " + open + ", High: " + high + ", Low: " + low + ", Close: " + close + ", Volume: " + volume + ", Count: " + count + ", WAP: " + WAP + "\n");
        }

        public virtual void scannerParameters(string xml)
        {
            Console.WriteLine("ScannerParameters. " + xml + "\n");
        }

        public virtual void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            Console.WriteLine("ScannerData. " + reqId + " - Rank: " + rank + ", Symbol: " + contractDetails.Summary.Symbol + ", SecType: " + contractDetails.Summary.SecType + ", Currency: " + contractDetails.Summary.Currency
                + ", Distance: " + distance + ", Benchmark: " + benchmark + ", Projection: " + projection + ", Legs String: " + legsStr + "\n");
        }

        public virtual void scannerDataEnd(int reqId)
        {
            Console.WriteLine("ScannerDataEnd. " + reqId + "\n");
        }

        public virtual void receiveFA(int faDataType, string faXmlData)
        {
            Console.WriteLine("Receing FA: " + faDataType + " - " + faXmlData + "\n");
        }

        public virtual void bondContractDetails(int requestId, ContractDetails contractDetails)
        {
            Console.WriteLine("Bond. Symbol " + contractDetails.Summary.Symbol + ", " + contractDetails.Summary);
        }

        public virtual void historicalDataEnd(int reqId, string startDate, string endDate)
        {
            Console.WriteLine("Historical data end - " + reqId + " from " + startDate + " to " + endDate);
        }

        public virtual void verifyMessageAPI(string apiData)
        {
            Console.WriteLine("verifyMessageAPI: " + apiData);
        }
        public virtual void verifyCompleted(bool isSuccessful, string errorText)
        {
            Console.WriteLine("verifyCompleted. IsSuccessfule: " + isSuccessful + " - Error: " + errorText);
        }
        public virtual void displayGroupList(int reqId, string groups)
        {
            Console.WriteLine("DisplayGroupList. Request: " + reqId + ", Groups" + groups);
        }
        public virtual void displayGroupUpdated(int reqId, string contractInfo)
        {
            Console.WriteLine("displayGroupUpdated. Request: " + reqId + ", ContractInfo: " + contractInfo);
        }

        #endregion
    }
}
