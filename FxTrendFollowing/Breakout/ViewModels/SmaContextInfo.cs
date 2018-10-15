using Carvers.Models.Indicators;

namespace FxTrendFollowing.Breakout.ViewModels
{
    internal class SmaContextInfo : IContextInfo
    {
        public double Sma3600Current { get; }
        public double Sma100Current { get; }

        public SmaContextInfo(double sma3600Current, double sma100Current)
        {
            Sma3600Current = sma3600Current;
            Sma100Current = sma100Current;
        }
    }
}