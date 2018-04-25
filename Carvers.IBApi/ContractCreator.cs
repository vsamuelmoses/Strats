using Carvers.Models;
using IBApi;

namespace Carvers.IBApi
{
    public static class ContractCreator
    {
        public static Contract GetCurrencyPairContract(CurrencyPair pair)
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


    }
}