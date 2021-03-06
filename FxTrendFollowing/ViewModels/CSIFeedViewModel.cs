﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Carvers.Infra.Extensions;
using Carvers.Models;

namespace FxTrendFollowing.ViewModels
{
    public class CSIFeedViewModel
    {
        private readonly IEnumerable<HourlyCurrencyPairData> cpDataVms;
        private readonly ConcurrentDictionary<Currency, SingleCurrencyStrength> currencyStrengths;

        public CSIFeedViewModel(IEnumerable<HourlyCurrencyPairData> cpDataVms)
        {
            this.cpDataVms = cpDataVms;

            SingleCurrencyStrength.CurrencyStrengthStream.Subscribe(strength => AllCurrencyStrength.Add(strength));

            currencyStrengths = new ConcurrentDictionary<Currency, SingleCurrencyStrength>(
                cpDataVms
                    .Select(pair => new[] {pair.Pair.BaseCurrency, pair.Pair.TargetCurrency})
                    .SelectMany(cx => cx)
                    .Distinct()
                    .Select(cx => SingleCurrencyStrength.Unknown(cx))
                    .ToDictionary(cx => cx.Currency, cx => cx));


            cpDataVms
                .Foreach(pairVm => 
                {
                    pairVm.HourlyCsiStream
                       .Subscribe(csiRaw => {
                            AddCurrencyPairStrength(pairVm.Pair.TargetCurrency, pairVm.Pair.BaseCurrency, (csiRaw.Item1, csiRaw.Item2));
                            AddCurrencyPairStrength(pairVm.Pair.BaseCurrency, pairVm.Pair.TargetCurrency, (csiRaw.Item1, csiRaw.Item3));
                        });
                });
        }

        private void AddCurrencyPairStrength(Currency currency, Currency againstCurrency, (DateTimeOffset dt, double strength) tup)
        {
            SingleCurrencyStrength.Add(currency, againstCurrency, tup.strength, tup.dt);

        }


        public double GetExchangeRate(CurrencyPair pair)
        {
            return cpDataVms.Single(p => ReferenceEquals(p.Pair, pair))
                .LatestCandle.Ohlc.Close.Value;
        }
    }

    public class CSLogger
    {
        public CSLogger(Func<Currency, string> currencyStrengtFileGetter, string allCurrencyStrengthFile)
        {
            SingleCurrencyStrength.CurrencyStrengthStream.Subscribe(strength =>
            {
                System.IO.File.AppendAllLines(currencyStrengtFileGetter(strength.Currency),
                    new[]
                    {
                        string.Join(",", new string[] {
                            strength.TimeStamp.ToString(),
                            string.Join(",", strength.StrengthAgainstCurrency.Select(kvp => $"{kvp.Key},{kvp.Value}")),
                            $"AVG,{strength.AverageValue}"
                        })
                    });
            });


            AllCurrencyStrength.CurrencyStrengthStream.Subscribe(strength =>
            {
            System.IO.File.AppendAllLines(allCurrencyStrengthFile,
                new[]
                {
                        string.Join(",", new string[] {
                            strength.TimeStamp.ToString(),
                            string.Join(",", strength.IndividualStrengths.Select(str => $"{str.Key},{str.Value.AverageValue}")) })
                    });
            });
        }
    }
}