namespace Carvers.Models.Extensions
{
    public static class StringEntensions
    {
        public static Symbol AsSymbol(this string symbol)
        {
            return new Symbol(symbol);
        }
    }
}