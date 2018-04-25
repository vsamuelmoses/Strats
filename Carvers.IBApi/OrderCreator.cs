using System;
using System.Collections.Generic;
using IBApi;

namespace Carvers.IBApi
{
    public static class OrderCreator
    {
        public static Order GetOrder(int id, string action, string quantityText, string orderType, string limitPrice,string tif)
        {
            return GetOrder(id, action, orderType, limitPrice, quantityText, "", "", tif, "", "", "");
        }

        public static Order GetOrder(int orderId, string actionText, string orderTypeText, string lmtPriceText, string quantityText, string accountText, string modelCodeText,
            string timeInForceText, string auxPriceText, string displaySizeText, string cashQtyText)
        {
            Order order = new Order();
            order.OrderId = orderId;
            order.Action = actionText;
            order.OrderType = orderTypeText;
            if (!lmtPriceText.Equals(""))
                order.LmtPrice = Double.Parse(lmtPriceText);
            if (!quantityText.Equals(""))
                order.TotalQuantity = Double.Parse(quantityText);
            order.Account = accountText;
            order.ModelCode = modelCodeText;
            order.Tif = timeInForceText;
            if (!auxPriceText.Equals(""))
                order.AuxPrice = Double.Parse(auxPriceText);
            if (!displaySizeText.Equals(""))
                order.DisplaySize = Int32.Parse(displaySizeText);
            if (!cashQtyText.Equals(""))
                order.CashQty = Double.Parse(cashQtyText);

            FillExtendedOrderAttributes(order, Rule80A.None, TriggerMethod.Default, OCAType.None, HedgeType.None, transmit:true);
            FillAdvisorAttributes(order, FaMethod.None);
            FillVolatilityAttributes(order, VolatilityType.None, ReferencePriceType.None, deltaNeutralOrderType:"None");
            FillScaleAttributes(order);
            FillAlgoAttributes(order, strageyText:"None");
            FillPegToBench(order, isPeggedChangeDecrease:false);
            FillAdjustedStops(order);
            FillConditions(order);

            return order;
        }

        private static void  FillConditions(Order order)
        {
            //order.Conditions = orderBindingSource.DataSource as List<OrderCondition>;
            //order.ConditionsIgnoreRth = ignoreRth.Checked;
            //order.ConditionsCancelOrder = cancelOrder.SelectedIndex == 1;
        }

        private static void  FillAdjustedStops(Order order)
        {
            //if (cbAdjustedOrderType.SelectedIndex > 0)
            //    order.AdjustedOrderType = cbAdjustedOrderTypeText;

            //if (!string.IsNullOrWhiteSpace(tbTriggerPriceText))
            //    order.TriggerPrice = double.Parse(tbTriggerPriceText;

            //if (!string.IsNullOrWhiteSpace(tbAdjustedStopPriceText))
            //    order.AdjustedStopPrice = double.Parse(tbAdjustedStopPriceText;

            //if (!string.IsNullOrWhiteSpace(tbAdjustedStopLimitPriceText))
            //    order.AdjustedStopLimitPrice = double.Parse(tbAdjustedStopLimitPriceText;

            //if (!string.IsNullOrWhiteSpace(tbAdjustedTrailingAmntText))
            //    order.AdjustedTrailingAmount = double.Parse(tbAdjustedTrailingAmntText;

            //order.AdjustableTrailingUnit = (cbAdjustedTrailingAmntUnit.SelectedItem as TrailingAmountUnit).Val;
            order.LmtPriceOffset = order.LmtPrice - order.AuxPrice;
        }

        private static void FillPegToBench(Order order, bool isPeggedChangeDecrease)
        {
            //if (!string.IsNullOrWhiteSpace(tbStartingPriceText))
            //    order.StartingPrice = double.Parse(tbStartingPriceText;

            //if (!string.IsNullOrWhiteSpace(tbStartingReferencePriceText))
            //    order.StockRefPrice = double.Parse(tbStartingReferencePriceText;

            //if (!string.IsNullOrWhiteSpace(tbPeggedChangeAmountText))
            //    order.PeggedChangeAmount = double.Parse(tbPeggedChangeAmountText;

            //if (!string.IsNullOrWhiteSpace(tbReferenceChangeAmountText))
            //    order.ReferenceChangeAmount = double.Parse(tbReferenceChangeAmountText;

            //if (!string.IsNullOrWhiteSpace(pgdStockRangeLowerText))
            //    order.StockRangeLower = double.Parse(pgdStockRangeLowerText;

            //if (!string.IsNullOrWhiteSpace(pgdStockRangeUpperText))
            //    order.StockRangeUpper = double.Parse(pgdStockRangeUpperText;

            order.IsPeggedChangeAmountDecrease = isPeggedChangeDecrease;

            //if (contractSearchControl1.Contract != null)
            //{
            //    order.ReferenceContractId = contractSearchControl1.Contract.ConId;
            //    order.ReferenceExchange = contractSearchControl1.Contract.Exchange;
            //}
        }

        private static void FillExtendedOrderAttributes(Order order, IBType rule80A, IBType triggerMethod, IBType ocaType, IBType hedgeType, bool transmit)
        {
            FillExtendedOrderAttributes(order, "", "", "", "", (string)rule80A.Value,
                (int)triggerMethod.Value, "", "", "", "", "", "", (int)ocaType.Value, (string)hedgeType.Value,
                "", false, false, false, false, false, false, false, false, false, false, transmit, "", "", "", "");
        }

        private static void FillExtendedOrderAttributes(Order order,
            string orderReferenceText, string minQtyText, string goodAfterText, string goodUntilText, string rule80A, int triggerMethod, string percentOffsetText,
            string trailStopPriceText, string discretionaryAmountText, string nbboPriceCapText, string trailingPercentText, string ocaGroupText, int ocaType, string hedgeTypeText, 
            string hedgeParamText, bool notHeld, bool block, bool sweepToFill, bool outsideRTH, bool hidden, bool allOrNone, bool overrideConstraints,  
            bool eTrade, bool firmQuote, bool optOutSmart, bool transmit,
            string mifid2DecisionAlgoText, string mifid2DecisionMakerText, string mifid2ExecutionAlgoText, string mifid2ExecutionTraderText)
        {
            order.OrderRef = orderReferenceText;
            if (!minQtyText.Equals(""))
                order.MinQty = Int32.Parse(minQtyText);
            order.GoodAfterTime = goodAfterText;
            order.GoodTillDate = goodUntilText;
            order.Rule80A = rule80A;
            order.TriggerMethod = triggerMethod;

            if (!percentOffsetText.Equals(""))
                order.PercentOffset = Double.Parse(percentOffsetText);
            if (!trailStopPriceText.Equals(""))
                order.TrailStopPrice = Double.Parse(trailStopPriceText);
            if (!trailingPercentText.Equals(""))
                order.TrailingPercent = Double.Parse(trailingPercentText);
            if (!discretionaryAmountText.Equals(""))
                order.DiscretionaryAmt = Int32.Parse(discretionaryAmountText);
            if (!nbboPriceCapText.Equals(""))
                order.NbboPriceCap = Double.Parse(nbboPriceCapText);

            order.OcaGroup = ocaGroupText;
            order.OcaType = ocaType;
            order.HedgeType = hedgeTypeText;
            order.HedgeParam = hedgeParamText;

            order.NotHeld = notHeld;
            order.BlockOrder = block;
            order.SweepToFill = sweepToFill;
            order.Hidden = hidden;
            order.OutsideRth = outsideRTH;
            order.AllOrNone = allOrNone;
            order.OverridePercentageConstraints = overrideConstraints;
            order.ETradeOnly = eTrade;
            order.FirmQuoteOnly = firmQuote;
            order.OptOutSmartRouting = optOutSmart;
            order.Transmit = transmit;
            order.Tier = new SoftDollarTier("", "", "");
            order.Mifid2DecisionMaker = mifid2DecisionMakerText;
            order.Mifid2DecisionAlgo = mifid2DecisionAlgoText;
            order.Mifid2ExecutionTrader = mifid2ExecutionTraderText;
            order.Mifid2ExecutionAlgo = mifid2ExecutionAlgoText;
        }

        private static void FillVolatilityAttributes(Order order, IBType volatilityType, IBType optionReferencePrice, string deltaNeutralOrderType)
        {
            FillVolatilityAttributes(order, "", (int)volatilityType.Value, false, (int)optionReferencePrice.Value, "", deltaNeutralOrderType, "", "", "");
        }
    
        private static void FillVolatilityAttributes(Order order,
        string volatilityText, int volatilityType, bool continuousUpdate, int optionReferencePrice, string deltaNeutralAuxPriceText, string deltaNeutralOrderTypeText, string deltaNeutralConIdText, 
            string stockRangeLowerText, string stockRangeUpperText)
        {
            if (!volatilityText.Equals(""))
                order.Volatility = Double.Parse(volatilityText);
            order.VolatilityType = volatilityType;
            if (continuousUpdate)
                order.ContinuousUpdate = 1;
            else
                order.ContinuousUpdate = 0;
            order.ReferencePriceType = optionReferencePrice;

            if (!deltaNeutralOrderTypeText.Equals("None"))
                order.DeltaNeutralOrderType = deltaNeutralOrderTypeText;

            if (!deltaNeutralAuxPriceText.Equals(""))
                order.DeltaNeutralAuxPrice = Double.Parse(deltaNeutralAuxPriceText);
            if (!deltaNeutralConIdText.Equals(""))
                order.DeltaNeutralConId = Int32.Parse(deltaNeutralConIdText);
            if (!stockRangeLowerText.Equals(""))
                order.StockRangeLower = Double.Parse(stockRangeLowerText);
            if (!stockRangeUpperText.Equals(""))
                order.StockRangeUpper = Double.Parse(stockRangeUpperText);
        }

        private static void FillAdvisorAttributes(Order order, IBType faMethod)
        {
            FillAdvisorAttributes(order, "", "", (string)faMethod.Value, "");
        }

        private static void FillAdvisorAttributes(Order order,
            string faGroupText, string faPercentageText, string faMethod, string faProfileText)
        {
            order.FaGroup = faGroupText;
            order.FaPercentage = faPercentageText;
            order.FaMethod = faMethod;
            order.FaProfile = faProfileText;
        }

        private static void FillScaleAttributes(Order order)
        {
            FillScaleAttributes(order, "", "", "", "", "", "", "", "", false, randomiseSize: false);
        }

        private static void FillScaleAttributes(Order order,
            string initialFillQuantityText, string subsequentLevelSizeText,string priceAdjustIntervalText, string priceAdjustValueText, string profitOffsetText, string initialLevelSizeText, string initialPositionText, 
            string priceIncrementText, bool autoReset, bool randomiseSize)
        {
            if (!initialLevelSizeText.Equals(""))
                order.ScaleInitLevelSize = Int32.Parse(initialLevelSizeText);
            if (!subsequentLevelSizeText.Equals(""))
                order.ScaleSubsLevelSize = Int32.Parse(subsequentLevelSizeText); 
            if (!priceIncrementText.Equals(""))
                order.ScalePriceIncrement = Double.Parse(priceIncrementText);
            if (!priceAdjustValueText.Equals(""))
                order.ScalePriceAdjustValue = Double.Parse(priceAdjustValueText);
            if (!priceAdjustIntervalText.Equals(""))
                order.ScalePriceAdjustInterval = Int32.Parse(priceAdjustIntervalText);
            if (!profitOffsetText.Equals(""))
                order.ScaleProfitOffset = Double.Parse(profitOffsetText);
            if (!initialPositionText.Equals(""))
                order.ScaleInitPosition = Int32.Parse(initialPositionText);
            if (!initialFillQuantityText.Equals(""))
                order.ScaleInitFillQty = Int32.Parse(initialFillQuantityText);

            order.ScaleAutoReset = autoReset;
            order.ScaleRandomPercent = randomiseSize;
        }

        private static void FillAlgoAttributes(Order order, string strageyText)
        {
            FillAlgoAttributes(order, strageyText, "", "", "", "", "", "", "", "", "", "", "", "", "");
        }

        private static void FillAlgoAttributes(Order order,
            string algoStrategyText, string startTimeText, string endTimeText, string maxPctVolText, string noTakeLiqText, 
            string getDoneText, string noTradeAheadText, string useOddLotsText, string allowPastEndTimeText,
            string strategyTypeText, string riskAversionText, string forceCompletionText,  string displaySizeAlgoText, string pctVolText)
        {
            if (algoStrategyText.Equals("") || algoStrategyText.Equals("None"))
                return;
            List<TagValue> algoParams = new List<TagValue>();
            algoParams.Add(new TagValue("startTime", startTimeText));
            algoParams.Add(new TagValue("endTime", endTimeText));

            order.AlgoStrategy = algoStrategyText;

            /*Vwap Twap ArrivalPx DarkIce PctVol*/
            if (order.AlgoStrategy.Equals("VWap"))
            {
                algoParams.Add(new TagValue("maxPctVol", maxPctVolText));
                algoParams.Add(new TagValue("noTakeLiq", noTakeLiqText));
                algoParams.Add(new TagValue("getDone", getDoneText));
                algoParams.Add(new TagValue("noTradeAhead", noTradeAheadText));
                algoParams.Add(new TagValue("useOddLots", useOddLotsText));
                algoParams.Add(new TagValue("allowPastEndTime", allowPastEndTimeText));
            }

            if (order.AlgoStrategy.Equals("Twap"))
            {
                algoParams.Add(new TagValue("strategyType", strategyTypeText));
                algoParams.Add(new TagValue("allowPastEndTime", allowPastEndTimeText));
            }

            if (order.AlgoStrategy.Equals("ArrivalPx"))
            {
                algoParams.Add(new TagValue("allowPastEndTime", allowPastEndTimeText));
                algoParams.Add(new TagValue("maxPctVol", maxPctVolText));
                algoParams.Add(new TagValue("riskAversion", riskAversionText));
                algoParams.Add(new TagValue("forceCompletion", forceCompletionText));
            }

            if (order.AlgoStrategy.Equals("DarkIce"))
            {
                algoParams.Add(new TagValue("allowPastEndTime", allowPastEndTimeText));
                algoParams.Add(new TagValue("displaySize", displaySizeAlgoText));
            }

            if (order.AlgoStrategy.Equals("PctVol"))
            {
                algoParams.Add(new TagValue("pctVol", pctVolText));
                algoParams.Add(new TagValue("noTakeLiq", noTakeLiqText));
            }
            order.AlgoParams = algoParams;
        }

        class IBType
        {
            private string name;
            private object value;

            public IBType(string name, object value)
            {
                this.name = name;
                this.value = value;
            }

            public string Name
            {
                get { return name; }
            }

            public object Value
            {
                get { return this.value; }
            }

            public override string ToString()
            {
                return name;
            }
        }

        class TriggerMethod
        {
            public static object[] GetAll()
            {
                return new object[] { Default, DoubleBidAsk, Last, DoubleLast, BidAsk, LastBidOrAsk, Midpoint };
            }
            public static IBType Default = new IBType("Default", 0);
            public static IBType DoubleBidAsk = new IBType("DoubleBidAsk", 1);
            public static IBType Last = new IBType("Last", 2);
            public static IBType DoubleLast = new IBType("DoubleLast", 3);
            public static IBType BidAsk = new IBType("BidAsk", 4);
            public static IBType LastBidOrAsk = new IBType("LastBidOrAsk", 5);
            public static IBType Midpoint = new IBType("Midpoint", 6);
        }

        class Rule80A
        {
            public static object[] GetAll()
            {
                return new object[] { None, IndivArb, IndivBigNonArb, IndivSmallNonArb, INST_ARB, InstBigNonArb, InstSmallNonArb };
            }
            public static IBType None = new IBType("None", "");
            public static IBType IndivArb = new IBType("IndivArb", "J");
            public static IBType IndivBigNonArb = new IBType("IndivBigNonArb", "K");
            public static IBType IndivSmallNonArb = new IBType("IndivSmallNonArb", "I");
            public static IBType INST_ARB = new IBType("INST_ARB", "U");
            public static IBType InstBigNonArb = new IBType("InstBigNonArb", "Y");
            public static IBType InstSmallNonArb = new IBType("InstSmallNonArb", "A");
        }

        class OCAType
        {
            public static object[] GetAll()
            {
                return new object[] { None, CancelWithBlocking, ReduceWithBlocking, ReduceWithoutBlocking };
            }
            public static IBType None = new IBType("None", 0);
            public static IBType CancelWithBlocking = new IBType("CancelWithBlocking", 1);
            public static IBType ReduceWithBlocking = new IBType("ReduceWithBlocking", 2);
            public static IBType ReduceWithoutBlocking = new IBType("ReduceWithoutBlocking", 3);
        }

        class HedgeType
        {
            public static object[] GetAll()
            {
                return new object[] { None, Delta, Beta, Fx, Pair };
            }
            public static IBType None = new IBType("None", "");
            public static IBType Delta = new IBType("Delta", "D");
            public static IBType Beta = new IBType("Beta", "B");
            public static IBType Fx = new IBType("Fx", "F");
            public static IBType Pair = new IBType("Pair", "P");
        }

        class VolatilityType
        {
            public static object[] GetAll()
            {
                return new object[] { None, Daily, Annual };
            }
            public static IBType None = new IBType("None", 0);
            public static IBType Daily = new IBType("Daily", 1);
            public static IBType Annual = new IBType("Annual", 1);
        }

        class ReferencePriceType
        {
            public static object[] GetAll()
            {
                return new object[] { None, Midpoint, BidOrAsk };
            }
            public static IBType None = new IBType("None", 0);
            public static IBType Midpoint = new IBType("Midpoint", 1);
            public static IBType BidOrAsk = new IBType("BidOrAsk", 2);
        }

        class FaMethod
        {
            public static object[] GetAll()
            {
                return new object[] { None, EqualQuantity, AvailableEquity, NetLiq, PctChange };
            }
            public static IBType None = new IBType("None", "");
            public static IBType EqualQuantity = new IBType("EqualQuantity", "EqualQuantity");
            public static IBType AvailableEquity = new IBType("AvailableEquity", "AvailableEquity");
            public static IBType NetLiq = new IBType("NetLiq", "NetLiq");
            public static IBType PctChange = new IBType("PctChange", "PctChange");
        }

        class ContractRight
        {
            public static object[] GetAll()
            {
                return new object[] { None, Put, Call };
            }

            public static IBType None = new IBType("None", "");
            public static IBType Put = new IBType("Put", "P");
            public static IBType Call = new IBType("Call", "C");
        }

        class FundamentalsReport
        {
            public static object[] GetAll()
            {
                return new object[] { ReportSnapshot, FinancialSummary, FinStatements, RESC };
            }
            public static IBType ReportSnapshot = new IBType("Company overview", "ReportSnapshot");
            public static IBType FinancialSummary = new IBType("Financial summary", "ReportsFinSummary");
            public static IBType FinStatements = new IBType("Financial statements", "ReportsFinStatements");
            public static IBType RESC = new IBType("Analyst estimates", "RESC");
        }

        class FinancialAdvisorDataType
        {
            public static object[] GetAll()
            {
                return new object[] { Groups, Profiles, Aliases };
            }

            public static IBType Groups = new IBType("Groups", 1);
            public static IBType Profiles = new IBType("Profiles", 2);
            public static IBType Aliases = new IBType("Alias", 3);
        }

        class AllocationGroupMethod
        {
            public static IBType EqualQuantity = new IBType("Equal quantity", "EqualQuantity");
            public static IBType AvailableEquity = new IBType("Available equity", "AvailableEquity");
            public static IBType NetLiquidity = new IBType("Net liquidity", "NetLiq");
            public static IBType PercentChange = new IBType("Percent change", "PctChange");
        }

        

        class MarketDataType
        {
            public static object[] GetAll()
            {
                return new object[] { Real_Time, Frozen, Delayed, Delayed_Frozen };
            }

            public static IBType get(int marketDataType)
            {
                IBType ret = Real_Time;
                foreach (object ibType in MarketDataType.GetAll())
                {
                    if ((int)((IBType)ibType).Value == marketDataType)
                    {
                        ret = (IBType)ibType;
                    }
                }
                return ret;
            }

            public static IBType Real_Time = new IBType("Real-Time", 1);
            public static IBType Frozen = new IBType("Frozen", 2);
            public static IBType Delayed = new IBType("Delayed", 3);
            public static IBType Delayed_Frozen = new IBType("Delayed-Frozen", 4);
        }
    }
}