namespace UtilityAi.Evaluators;


    /// <summary>
    /// Logistic (sigmoid) curve scaled to Output range. Great for thresholds:
    /// y = yMin + (yMax - yMin) / (1 + exp(-k * (x - x0))).
    /// Positive k: increasing S-curve. Negative k: decreasing.
    /// </summary>
    public sealed class LogisticCurve : ICurve
    {
        public Range Domain { get; }
        public Range Output { get; }

        public double K { get; }   // steepness
        public double X0 { get; }  // inflection point

        public LogisticCurve(Range domain, Range? output = null, double k = 1.0, double x0 = 0.0)
        {
            if (Math.Abs(domain.Size) < 1e-12) throw new ArgumentException("Domain length must be > 0.", nameof(domain));
            Domain = domain;
            Output = output ?? new Range(0, 1);
            K = k;
            X0 = x0;
        }

        /// <summary>
        /// Fit (k, x0) from two anchors y(x1)=y1 and y(x2)=y2 using Output scaling.
        /// y1,y2 MUST be strictly inside Output (not equal to bounds).
        /// </summary>
        public static LogisticCurve FitFromAnchors((double x, double y) a, (double x, double y) b, Range domain, Range? output = null)
        {
            var outR = output ?? new Range(0, 1);

            // normalize y into (0,1)
            double z1 = (a.y - outR.Min) / outR.Size;
            double z2 = (b.y - outR.Min) / outR.Size;

            if (!(z1 > 0 && z1 < 1 && z2 > 0 && z2 < 1))
                throw new ArgumentException("Anchor y must lie strictly within Output range.");

            if (Math.Abs(a.x - b.x) < 1e-12)
                throw new ArgumentException("Anchor x must be distinct.");

            // For z = 1/(1 + exp(-k*(x - x0)))  => ln(z/(1-z)) = k(x - x0)
            double L1 = Math.Log(z1 / (1 - z1));
            double L2 = Math.Log(z2 / (1 - z2));

            double k = (L1 - L2) / (a.x - b.x);
            double x0 = a.x - L1 / k;

            return new LogisticCurve(domain, outR, k, x0);
        }

        public double Evaluate(double x)
        {
            x = Domain.Clamp(x);
            double z = 1.0 / (1.0 + Math.Exp(-K * (x - X0)));
            double y = Output.Denormalize(z);
            return Output.Clamp(y);
        }
    }