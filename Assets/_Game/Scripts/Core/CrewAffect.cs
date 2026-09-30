using System;
using System.Collections.Generic;

namespace Jobsite.Core
{
    // Bounded, continuous affect for the people on site (quality review 2026-09-30, area 6). Two latent variables per
    // person: Trust in the competent person [-1, 1] and Stress [0, 1]. Every event moves them by a bounded step with
    // saturation (the closer to a bound, the smaller the step), and both relax over time toward a baseline. The integer
    // DaySession.CrewTrust stays as the gameplay counter; this state drives presentation (faces, gestures, tablet bar).
    // Coefficients are initial tuning values pending SME review (docs/DesignUpgrade_2026-09-30.md §6).
    public sealed class CrewAffect
    {
        public const string Crew = "crew", Foreman = "ray";
        public const float MaxStep = 0.35f;            // no single event moves a variable further than this
        public const float TrustRelaxPerSecond = 0.002f, StressRelaxPerSecond = 0.015f;

        public sealed class Person
        {
            public float Trust;      // -1 distrust .. 1 full trust
            public float Stress;     // 0 calm .. 1 overloaded
            public float TrustBaseline;
        }

        private readonly Dictionary<string, Person> people = new Dictionary<string, Person>
        {
            [Crew] = new Person { Trust = 0f },
            [Foreman] = new Person { Trust = -0.1f, TrustBaseline = -0.1f, Stress = 0.2f },   // Ray starts wary of the new CP
        };

        public Person Get(string id) => people.TryGetValue(id, out var p) ? p : people[id] = new Person();
        public float Trust(string id) => Get(id).Trust;
        public float Stress(string id) => Get(id).Stress;

        // Bounded, saturating step: a +d near +1 (or -d near -1) moves less.
        public void Nudge(string id, float dTrust, float dStress)
        {
            var p = Get(id);
            dTrust = Clamp(dTrust, -MaxStep, MaxStep); dStress = Clamp(dStress, -MaxStep, MaxStep);
            // Step = d x distance to the bound it moves toward, capped at |d|: full step near the middle, ~0 at the bound.
            var tStep = dTrust * (dTrust > 0 ? 1f - p.Trust : 1f + p.Trust);
            p.Trust = Clamp(p.Trust + Clamp(tStep, -Math.Abs(dTrust), Math.Abs(dTrust)), -1f, 1f);
            p.Stress = Clamp(p.Stress + (dStress > 0 ? dStress * (1f - p.Stress) : dStress), 0f, 1f);
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0f) return;
            foreach (var p in people.Values)
            {
                p.Trust = Toward(p.Trust, p.TrustBaseline, TrustRelaxPerSecond * seconds);
                p.Stress = Toward(p.Stress, 0f, StressRelaxPerSecond * seconds);
            }
        }

        // Presentation band for faces/gestures: what a viewer should read on this person right now.
        public enum Band { Calm, Warm, Tense, Hostile }
        public Band BandOf(string id)
        {
            var p = Get(id);
            if (p.Stress > 0.6f && p.Trust < 0f) return Band.Hostile;
            if (p.Stress > 0.45f) return Band.Tense;
            return p.Trust > 0.35f ? Band.Warm : Band.Calm;
        }

        static float Toward(float v, float target, float step) => v < target ? Math.Min(target, v + step) : Math.Max(target, v - step);
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
