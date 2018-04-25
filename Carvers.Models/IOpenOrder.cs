namespace Carvers.Models
{
    public interface IOpenOrder<T> : IOrder
        where T : IClosedOrder
    {
        T Close(IOrderInfo info);
    }
}