namespace Carvers.Models.Indicators
{
    public interface IIndicator
    {
        string Description { get; }
    }

    public abstract class CandleBasedIndicator : IIndicator
    {
        protected CandleBasedIndicator(string description)
        {
            Description = description;
        }
        public string Description { get; }
    }
}
