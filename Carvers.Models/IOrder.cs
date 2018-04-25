namespace Carvers.Models
{
    public interface IOrder
    {
        IOrderInfo OrderInfo { get; }
        string ToCsv();
    }
}