using System;
using Carvers.Models;
using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    internal class SmaContextInfo : IContextInfo
    {
        public double Sma3600Current { get; }
        public double Sma100Current { get; }
        public (IOrder, double) StopLossLimit { get; }

        public SmaContextInfo(double sma3600Current, 
            double sma100Current,
            (IOrder, double) stopLossLimit)
        {
            Sma3600Current = sma3600Current;
            Sma100Current = sma100Current;
            StopLossLimit = stopLossLimit;
        }

        public SmaContextInfo(double sma3600Current,
            double sma100Current)
        {
            Sma3600Current = sma3600Current;
            Sma100Current = sma100Current;
        }
    }
}