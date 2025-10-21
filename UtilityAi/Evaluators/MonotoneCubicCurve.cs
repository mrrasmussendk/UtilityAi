namespace UtilityAi.Evaluators;

   /// <summary>
    /// Smooth, overshoot-safe interpolation of keyframes using Fritsch–Carlson monotone cubic (PCHIP variant).
    /// Guarantees no overshoot between keys for monotone data (v increasing or decreasing).
    /// </summary>
    public sealed class MonotoneCubicCurve : ICurve
    {
        public Range Domain { get; }
        public Range Output { get; }

        public IReadOnlyList<(double t, double v)> Keys { get; }

        private readonly double[] _t, _v, _m; // tangents

        /// <exception cref="ArgumentException">If keys invalid or fewer than 2.</exception>
        public MonotoneCubicCurve(IEnumerable<(double t, double v)> keys, Range? domain = null, Range? output = null)
        {
            if (keys is null) throw new ArgumentNullException(nameof(keys));
            var k = keys.ToArray();
            if (k.Length < 2) throw new ArgumentException("Need at least two keys.", nameof(keys));

            Array.Sort(k, (a, b) => a.t.CompareTo(b.t));
            for (int i = 1; i < k.Length; i++)
                if (k[i - 1].t >= k[i].t)
                    throw new ArgumentException("Key times must be strictly increasing.", nameof(keys));

            Domain = domain ?? new Range(k[0].t, k[^1].t);
            Output = output ?? new Range(0, 1);

            for (int i = 0; i < k.Length; i++)
                k[i] = (k[i].t, Output.Clamp(k[i].v));

            Keys = Array.AsReadOnly(k);
            _t = k.Select(p => p.t).ToArray();
            _v = k.Select(p => p.v).ToArray();
            _m = ComputeMonotoneTangents(_t, _v);
        }

        public double Evaluate(double x)
        {
            if (x <= _t[0]) return _v[0];
            if (x >= _t[^1]) return _v[^1];

            int hi = MathX.LowerBound(_t, x);
            int lo = hi - 1;

            double h = _t[hi] - _t[lo];
            double t = (x - _t[lo]) / h;

            // Cubic Hermite basis
            double t2 = t * t, t3 = t2 * t;
            double h00 = 2 * t3 - 3 * t2 + 1;
            double h10 = t3 - 2 * t2 + t;
            double h01 = -2 * t3 + 3 * t2;
            double h11 = t3 - t2;

            double y = h00 * _v[lo] + h10 * h * _m[lo] + h01 * _v[hi] + h11 * h * _m[hi];
            return Output.Clamp(y);
        }

        // Fritsch–Carlson (1980) monotone cubic interpolation
        private static double[] ComputeMonotoneTangents(double[] x, double[] y)
        {
            int n = x.Length;
            var m = new double[n];
            var d = new double[n - 1]; // slopes per segment

            for (int i = 0; i < n - 1; i++)
            {
                double h = x[i + 1] - x[i];
                d[i] = (y[i + 1] - y[i]) / h;
            }

            // Endpoints
            m[0] = d[0];
            m[n - 1] = d[^1];

            // Interior tangents: weighted harmonic mean if slopes have same sign
            for (int i = 1; i < n - 1; i++)
            {
                if (d[i - 1] * d[i] <= 0)
                {
                    m[i] = 0;
                }
                else
                {
                    double w1 = 2 * (x[i + 1] - x[i]);
                    double w2 = 2 * (x[i] - x[i - 1]);
                    m[i] = (w1 + w2) / (w1 / d[i - 1] + w2 / d[i]);
                }
            }

            // Fritsch–Carlson slope limiter to avoid overshoot
            for (int i = 0; i < n - 1; i++)
            {
                if (Math.Abs(d[i]) < 1e-12)
                {
                    m[i] = 0; m[i + 1] = 0;
                    continue;
                }

                double a = m[i] / d[i];
                double b = m[i + 1] / d[i];
                double s = a * a + b * b;
                if (s > 9.0)
                {
                    double tau = 3.0 / Math.Sqrt(s);
                    m[i] = tau * a * d[i];
                    m[i + 1] = tau * b * d[i];
                }
            }
            return m;
        }
    }
