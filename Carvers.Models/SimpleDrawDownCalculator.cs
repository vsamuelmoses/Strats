namespace Carvers.Models
{
    public class SimpleDrawDownCalculator
    {
        public double Peak { get; set; }
        public double Trough { get; set; }
        public double MaxDDPercentage { get; set; }

        public SimpleDrawDownCalculator()
        {
            Peak = 0;
            Trough = 0;
            MaxDDPercentage = 0;
        }

        public double Calculate(double newValue)
        {
            if (newValue > Peak)
            {
                Peak = newValue;
                Trough = Peak;
            }
            else if (newValue < Trough)
            {
                Trough = newValue;

                var tmpDrawDown = ((Peak - Trough) * 100) / Peak;

                if (Trough < 0)
                {
                    tmpDrawDown = ((Peak + Trough) * 100) / Peak;
                }

                if (tmpDrawDown > 100)
                {
                    var breakpoint = 1;
                }

                if (tmpDrawDown > MaxDDPercentage)
                    MaxDDPercentage = tmpDrawDown;
            }

            return MaxDDPercentage;
        }
    }
}