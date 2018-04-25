namespace Carvers.Models
{
    public class Range<T>
    {
        public T From { get; }
        public T To { get; }

        public Range(T from, T to)
        {
            From = @from;
            To = to;
        }

        public override string ToString()
        {
            return $"Range({From},{To})";
        }


        public static Range<Tin> Create<Tin>(Tin from, Tin to)
            => new Range<Tin>(from, to);
    }
}