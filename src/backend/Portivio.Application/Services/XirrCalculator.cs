namespace Portivio.Application.Services
{
    public static class XirrCalculator
    {
        public static decimal Calculate(
            List<(DateTime date, decimal amount)> cashFlows,
            decimal guess = 0.1m,
            int maxIterations = 100,
            decimal tolerance = 0.000001m)
        {
            if (cashFlows.Count == 0)
                return 0m;

            var hasPositive = cashFlows.Any(cf => cf.amount > 0);
            var hasNegative = cashFlows.Any(cf => cf.amount < 0);
            if (!hasPositive || !hasNegative)
                return 0m;

            var firstDate = cashFlows.Min(cf => cf.date);

            double rate = (double)guess;
            for (int i = 0; i < maxIterations; i++)
            {
                double npv = 0;
                double npvDerivative = 0;

                foreach (var (date, amount) in cashFlows)
                {
                    double years = (date - firstDate).TotalDays / 365.0;
                    double factor = Math.Pow(1 + rate, years);
                    double cf = (double)amount;

                    npv += cf / factor;
                    if (Math.Abs(factor * (1 + rate)) > 1e-10)
                        npvDerivative -= years * cf / (factor * (1 + rate));
                }

                if (Math.Abs(npvDerivative) < 1e-10)
                    return 0m;

                double newRate = rate - npv / npvDerivative;

                if (Math.Abs(newRate - rate) < (double)tolerance)
                    return (decimal)Math.Round(newRate, 6);

                rate = newRate;

                if (rate <= -1)
                    return 0m;
            }

            return 0m;
        }
    }
}
