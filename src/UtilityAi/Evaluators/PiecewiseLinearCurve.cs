namespace UtilityAi.Evaluators;

   /// <summary>
    /// Piecewise linear curve through arbitrary keyframes (t, v). Exact-through, easy to reason about, editor-friendly.
    /// Monotonicity is up to the provided data.
    /// </summary>
    public sealed class PiecewiseLinearCurve : ICurve
    {
        public Range Domain { get; }
        public Range Output { get; }

        /// <summary>Sorted by t ascending. t in Domain, v in Output.</summary>
        public IReadOnlyList<(double t, double v)> Keys { get; }

        private readonly double[] _t;
        private readonly double[] _v;

        /// <exception cref="ArgumentException">If fewer than 2 keys, unsorted t, or values out of range.</exception>
        public PiecewiseLinearCurve(IEnumerable<(double t, double v)> keys, Range? domain = null, Range? output = null)
        {
            if (keys is null) throw new ArgumentNullException(nameof(keys));
            var k = keys.ToArray();
            if (k.Length < 2) throw new ArgumentException("Need at least two keys.", nameof(keys));

            // If domain/output not provided, infer minimal ranges from keys and clamp outputs to [0,1].
            var tMin = k.Min(p => p.t);
            var tMax = k.Max(p => p.t);
            Domain = domain ?? new Range(tMin, tMax);

            Output = output ?? new Range(0, 1);

            Array.Sort(k, (a, b) => a.t.CompareTo(b.t));
            for (int i = 1; i < k.Length; i++)
                if (k[i - 1].t >= k[i].t)
                    throw new ArgumentException("Key times must be strictly increasing.", nameof(keys));

            // Clamp values to output range
            for (int i = 0; i < k.Length; i++)
                k[i] = (k[i].t, Output.Clamp(k[i].v));

            Keys = Array.AsReadOnly(k);
            _t = k.Select(p => p.t).ToArray();
            _v = k.Select(p => p.v).ToArray();
        }

        public double Evaluate(double x)
        {
            if (x <= _t[0]) return _v[0];
            if (x >= _t[^1]) return _v[^1];

            int hi = MathX.LowerBound(_t, x);
            int lo = hi - 1;
            double t = MathX.InverseLerp(_t[lo], _t[hi], x);
            return Output.Clamp(MathX.Lerp(_v[lo], _v[hi], t));
        }
    }