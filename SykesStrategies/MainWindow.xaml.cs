using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Carvers.Infra;
using Carvers.Infra.Extensions;
using Carvers.Models;
using Carvers.Models.Extensions;
using SykesStrategies.Annotations;
using SykesStrategies.Data;
using SykesStrategies.Utilities;
using SykesStrategies.ViewModels;

namespace SykesStrategies
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string status;

        public MainWindow()
        {
            InitializeComponent();
            Strategy = new StrategyRunner();
            DataContext = this;
        }

        private void OnDownloadNYSEClicked(object sender, RoutedEventArgs e)
        {
            CsvReader
                .ReadFile(new FileInfo(Paths.NYSESymbols), values => new Symbol(values[0]), skip:1)
                .Select(symbol => Tuple.Create(symbol, DownloadQuoteMedia(symbol)))
                .Select(tuple => Tuple.Create(tuple.Item1, tuple.Item2.ToObservable()))
                .Foreach(tuple => tuple.Item2.Subscribe(async data =>
                {
                    if (!string.IsNullOrEmpty(data))
                        await WriteFile(data, $"{Paths.NYSEUniverse}\\{tuple.Item1}.csv");
                }));
        }

        private void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            //CsvReader
            //    .ReadFile(new FileInfo("Data\\NasdaqCompanyList.csv"), values => new Symbol(values[0]))
            //    .Select(symbol => Tuple.Create(symbol, DownloadGoogle(symbol)))
            //    .Select(tuple => Tuple.Create(tuple.Item1, tuple.Item2.ToObservable()))
            //    .Foreach(tuple => tuple.Item2.Subscribe(async data =>
            //    {
            //        if (!string.IsNullOrEmpty(data))
            //            await WriteFile(data, $"Data\\NasdaqData\\{tuple.Item1}.csv");
            //    }));


            CsvReader
                .ReadFile(new FileInfo("Data\\NasdaqCompanyList.csv"), (string[] values) => new Symbol(values[0]), skip:1)
                .Take(5)
                .Select(symbol => Tuple.Create(symbol, DownloadQuoteMedia(symbol)))
                .Select((Tuple<Symbol, Task<string>> tuple) => Tuple.Create(tuple.Item1, tuple.Item2.ToObservable()))
                .Foreach((Tuple<Symbol, IObservable<string>> tuple) => tuple.Item2.Subscribe(async data =>
                {
                    if (!string.IsNullOrEmpty(data))
                        await WriteFile(data, $"{Paths.NasdaqUniverse}\\{tuple.Item1}.csv");
                }));
        }

        private static async Task<string> DownloadGoogle(Symbol symbol)
        {
            try
            {
                var data = await Task<string>.Factory.StartNew(() =>
                {
                    Console.WriteLine($"Downloading data for {symbol}");
                    System.Net.WebClient wc = new System.Net.WebClient();
                    return wc.DownloadString("https://www.google.com/finance/historical?output=csv&q=" + symbol);
                });

                Console.WriteLine($"Downloaded data for {symbol}");
                return data;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static async Task<string> DownloadQuoteMedia(Symbol symbol)
        {
            try
            {
                var data = await Task<string>.Factory.StartNew(() =>
                {
                    var today = DateTime.Now;
                    var webaddress =
                        "https://app.quotemedia.com/quotetools/getHistoryDownload.csv?&webmasterId=501&" +
                        $"startDay=01&startMonth=01&startYear=2000&endDay={today.Day}&endMonth={today.Month}&endYear={today.Year}&isRanged=false&symbol={symbol}";

                    Console.WriteLine($"Downloading data for {symbol}");
                    System.Net.WebClient wc = new System.Net.WebClient();
                    return wc.DownloadString(webaddress);
                });

                Console.WriteLine($"Downloaded data for {symbol}");
                return data;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static async Task WriteFile(string content, string path)
        {
            await Task.Factory.StartNew(() =>
            {
                File.WriteAllText(path, content);
            });
        }


        private void OnReadClicked(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() =>
                Directory
                .EnumerateFiles(@"..\..\..\Data\historic")
                .Select(file => new FileInfo(file))
                .Select(file => CsvReader.ReadFileAsync(file, CandleCreator.GoogleFormat, skip:1)
                                .ContinueWith(task =>
                                {
                                    Status = $"Read data for: {file.Name}: {task.Result.Count()}";
                                    return new StockData(Path.GetFileNameWithoutExtension(file.Name).AsSymbol(), task.Result);})))
                .ContinueWith(t =>
                {
                    var text =
                    string.Join(Environment.NewLine,
                        t.Result
                        .Select(stockTask => $"{stockTask.Result.Symbol},{stockTask.Result.AverageClosePrice(30)}")
                        .Where(str => !string.IsNullOrEmpty(str)));
                    File.WriteAllText(@"..\..\..\Data\Universe.csv", text);
                    Status = "Completed reading all files";
                });
        }

        private void OnReadSykesUniverse(object sender, RoutedEventArgs e)
        {
            Strategy.Start();
        }


        public StrategyRunner Strategy { get; private set; }

        public string Status
        {
            get { return status; }
            set
            {
                status = value;
                OnPropertyChanged();
            }
        }

        public SykesNasdaqUniverse SykesNasdaqUniverse { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
