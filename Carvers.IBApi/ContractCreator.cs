using System;
using Carvers.Models;
using IBApi;

namespace Carvers.IBApi
{
    public static class ContractCreator
    {
        private static Contract GetCurrencyPairContract(CurrencyPair pair)
        {
            return new Contract
            {
                SecType = "CASH",
                Symbol = pair.TargetCurrency.ToString(),
                Exchange = "IDEALPRO",
                Currency = pair.BaseCurrency.ToString(),
                LastTradeDateOrContractMonth = string.Empty,
                PrimaryExch = string.Empty,
                IncludeExpired = false,
                Strike = 0,
                Multiplier = string.Empty,
                LocalSymbol = string.Empty
            };
        }

        private static Contract GetIndexContract(Index index)
        {
            return new Contract
            {
                SecType = "Index",
                Symbol = index.ToString(),
                Exchange = "IDEALPRO",
                Currency = "USD",
                LastTradeDateOrContractMonth = string.Empty,
                PrimaryExch = string.Empty,
                IncludeExpired = false,
                Strike = 0,
                Multiplier = string.Empty,
                LocalSymbol = string.Empty
            };
        }

        public static Contract GetContract(this Symbol symbol)
        {
            switch (symbol)
            {
                case CurrencyPair pair:
                    return GetCurrencyPairContract(pair);
                case Index index:
                    return GetIndexContract(index);
                default: 
                    throw new Exception("Unexpected symbol type");
            }
        }

        public static Symbol ToSymbol(this Contract contract)
        {
            if (contract.SecType == "CASH")
                return CurrencyPair.Get(Currency.Get(contract.Symbol), Currency.Get(contract.Currency));

            if (contract.SecType == "Index")
                return Index.Get(contract.Symbol);

            throw new Exception("Unexpected contract type");
        }
    }
}