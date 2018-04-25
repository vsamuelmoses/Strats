namespace Carvers.Models
{
    public interface IClosedOrder : IOrder
    {
        Price ProfitLoss { get; }
    }
}