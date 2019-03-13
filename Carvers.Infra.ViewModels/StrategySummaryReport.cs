using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Carvers.Models;
using Carvers.Models.Events;

namespace Carvers.Infra.ViewModels
{

    public class StrategySummaryReport : ViewModel
    {
        private readonly Subject<DateTimeEvent<Price>> _profitLossStream;

        public StrategySummaryReport(IEnumerable<IStrategy> strategies)
        {
            _profitLossStream = new Subject<DateTimeEvent<Price>>();
            ddCalculator = new SimpleDrawDownCalculator();
            var closedOrders = strategies
                .Select(strat => strat.CloseddOrders)
                .Merge()
                .Subscribe(recentClosedOrder => Report(recentClosedOrder, strategies));
        }

        private void Report(IOrder recentClosedOrder, IEnumerable<IStrategy> stockData)
        {
            var orders = stockData.SelectMany(stock => stock.ClosedOrders).ToList();
            TotalTrades = orders.Count();
            ProfitableTrades = orders.Count(o => o.ProfitLoss > Price.ZeroUSD);
            LoosingTrades = orders.Count(o => o.ProfitLoss < Price.ZeroUSD);

            if (orders.Any())
                WinningPercentage = (profitableTrades * 100) / orders.Count();

            ProfitLoss = orders.ProfitLoss();
            SharpeRatio = SharpeRatioCalculator.Calculate(orders);
            MaxDDPercentage = ddCalculator.Calculate(ProfitLoss.Value);
            _profitLossStream.OnNext(new DateTimeEvent<Price>(recentClosedOrder.OrderInfo.TimeStamp, ProfitLoss));
        }

        private int totalTrades;
        public int TotalTrades
        {
            get { return totalTrades; }
            set { totalTrades = value; OnPropertyChanged(); }
        }

        private int profitableTrades;
        public int ProfitableTrades
        {
            get { return profitableTrades; }
            set { profitableTrades = value; OnPropertyChanged(); }
        }

        private int loosingTrades;
        public int LoosingTrades
        {
            get { return loosingTrades; }
            set { loosingTrades = value; OnPropertyChanged(); }
        }


        private double winningPercentage;

        public double WinningPercentage
        {
            get { return winningPercentage; }
            set { winningPercentage = value; OnPropertyChanged(); }
        }


        private Price profitLoss;

        public Price ProfitLoss
        {
            get { return profitLoss; }
            set
            {
                profitLoss = value;
                OnPropertyChanged();

            }
        }


        public double MaxDDPercentage
        {
            get => _maxDdPercentage;
            set
            {
                _maxDdPercentage = value;
                OnPropertyChanged();
            }
        }

        private double sharpeRatio;
        private double _maxDdPercentage;
        private SimpleDrawDownCalculator ddCalculator;

        public double SharpeRatio
        {
            get { return sharpeRatio; }
            set { sharpeRatio = value; OnPropertyChanged(); }
        }

        public IObservable<DateTimeEvent<Price>> ProfitLossStream => _profitLossStream;

    }

    public class StrategyInstrumentSummaryReport : ViewModel
    {
        private readonly Symbol _symbol;
        private readonly Subject<DateTimeEvent<Price>> _profitLossStream;

        public StrategyInstrumentSummaryReport(IStrategy strategy, Symbol symbol)
        {
            _symbol = symbol;
            _profitLossStream = new Subject<DateTimeEvent<Price>>();
            ddCalculator = new SimpleDrawDownCalculator();
            strategy.CloseddOrders
                .Subscribe(recentClosedOrder => Report(recentClosedOrder, strategy, symbol));
        }

        private void Report(IOrder recentClosedOrder, IStrategy strategy, Symbol symbol)
        {
            var orders = strategy.ClosedOrders.Where(o => o.OrderInfo.Symbol == symbol).ToList();
            TotalTrades = orders.Count();
            ProfitableTrades = orders.Count(o => o.ProfitLoss > Price.ZeroUSD);
            LoosingTrades = orders.Count(o => o.ProfitLoss < Price.ZeroUSD);

            if (orders.Any())
                WinningPercentage = (profitableTrades * 100) / orders.Count();

            ProfitLoss = orders.ProfitLoss();
            SharpeRatio = SharpeRatioCalculator.Calculate(orders);
            MaxDDPercentage = ddCalculator.Calculate(ProfitLoss.Value);
            _profitLossStream.OnNext(new DateTimeEvent<Price>(recentClosedOrder.OrderInfo.TimeStamp, ProfitLoss));
        }

        private int totalTrades;
        public int TotalTrades
        {
            get { return totalTrades; }
            set { totalTrades = value; OnPropertyChanged(); }
        }

        private int profitableTrades;
        public int ProfitableTrades
        {
            get { return profitableTrades; }
            set { profitableTrades = value; OnPropertyChanged(); }
        }

        private int loosingTrades;
        public int LoosingTrades
        {
            get { return loosingTrades; }
            set { loosingTrades = value; OnPropertyChanged(); }
        }


        private double winningPercentage;

        public double WinningPercentage
        {
            get { return winningPercentage; }
            set { winningPercentage = value; OnPropertyChanged(); }
        }


        private Price profitLoss;

        public Price ProfitLoss
        {
            get { return profitLoss; }
            set
            {
                profitLoss = value;
                OnPropertyChanged();

            }
        }


        public double MaxDDPercentage
        {
            get => _maxDdPercentage;
            set
            {
                _maxDdPercentage = value;
                OnPropertyChanged();
            }
        }

        private double sharpeRatio;
        private double _maxDdPercentage;
        private SimpleDrawDownCalculator ddCalculator;

        public double SharpeRatio
        {
            get { return sharpeRatio; }
            set { sharpeRatio = value; OnPropertyChanged(); }
        }

        public IObservable<DateTimeEvent<Price>> ProfitLossStream => _profitLossStream;

    }

    public class StrategyEmailReport : ViewModel
    {
        private readonly Subject<DateTimeEvent<Price>> _profitLossStream;

        public StrategyEmailReport(IEnumerable<IStrategy> strategies)
        {

            foreach(var strategy in strategies)
                {
                    strategy
                    .OpenOrders
                    .Merge(strategy.CloseddOrders)
                    .Subscribe(order => Report(order, "vsamuelmoses@gmail.com"));
                }

        }

        public static bool Report(IOrder order, string receiverEmail)
        {
            using (var client = new SmtpClient())
            {
                var msg = new MailMessage { From = new MailAddress("technologycarvers@gmail.com") };
                msg.To.Add(receiverEmail);
                msg.Subject = $"{order.OrderInfo.TimeStamp}, {order.GetType().Name}, {order.OrderInfo.Symbol}";
                msg.Body = $"{order.OrderInfo.TimeStamp}, {order.GetType().Name}, {order.OrderInfo.Symbol}";

                client.UseDefaultCredentials = true;
                client.Host = "smtp.gmail.com";
                client.Port = 587;
                client.EnableSsl = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Credentials = new NetworkCredential("technologycarvers@gmail.com", "moonnight");
                client.Timeout = 20000;
                try
                {
                    client.Send(msg);
                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
                finally
                {
                    msg.Dispose();
                }
            }
        }
    }
}