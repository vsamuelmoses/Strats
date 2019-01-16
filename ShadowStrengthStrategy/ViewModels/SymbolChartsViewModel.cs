using Carvers.Charting.MultiPane;
using Carvers.Infra.ViewModels;
using Carvers.Models;

namespace ShadowStrengthStrategy.ViewModels
{
    public class SymbolChartsViewModel : ViewModel
    {
        private string _summary;

        public SymbolChartsViewModel(Symbol instrument, CreateMultiPaneStockChartsViewModel chart, StrategyInstrumentSummaryReport summaryReport)
        {
            Instrument = instrument;
            Chart = chart;
            SummaryReport = summaryReport;
        }

        public Symbol Instrument { get; }
        public CreateMultiPaneStockChartsViewModel Chart { get; private set; }
        public StrategyInstrumentSummaryReport SummaryReport { get; }

        public string Summary
        {
            get => _summary;
            set
            {
                _summary = value;
                OnPropertyChanged();
            }
        }
    }
}