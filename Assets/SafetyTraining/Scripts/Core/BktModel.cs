using System;

namespace SafetyTraining.Core
{
    /// <summary>
    /// Online Bayesian Knowledge Tracing for one learning objective. Each binary
    /// observation (criterion met / not met) updates the latent mastery posterior
    /// P(L). Parameters default to conventional priors; population-level EM over
    /// logged sessions (Tools/dashboard/bkt.py) refines them offline.
    /// </summary>
    public sealed class BktModel
    {
        readonly double pTransit;
        readonly double pSlip;
        readonly double pGuess;
        double pLearned;

        public BktModel(double pL0 = 0.30, double pT = 0.20, double pS = 0.10, double pG = 0.20)
        {
            pLearned = Clamp(pL0);
            pTransit = Clamp(pT);
            pSlip = Clamp(pS);
            pGuess = Clamp(pG);
        }

        public double Mastery => pLearned;
        public int ObservationCount { get; private set; }

        /// <summary>Updates the posterior with one observation and returns P(L).</summary>
        public double Observe(bool correct)
        {
            var pCorrect = pLearned * (1 - pSlip) + (1 - pLearned) * pGuess;
            pCorrect = Clamp(pCorrect);
            var posterior = correct
                ? pLearned * (1 - pSlip) / pCorrect
                : pLearned * pSlip / (1 - pCorrect);
            posterior = Clamp(posterior);
            pLearned = Clamp(posterior + (1 - posterior) * pTransit);
            ObservationCount++;
            return pLearned;
        }

        static double Clamp(double value) => Math.Max(0.001, Math.Min(0.999, value));
    }
}
