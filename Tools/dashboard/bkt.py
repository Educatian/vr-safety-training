"""Bayesian Knowledge Tracing (classic 4-parameter BKT) fitted by a simplified
EM over per-session binary observation sequences, one model per learning
objective.

Observations come from `assessment_evidence` events (outcome met/not_met per
objectiveId, ordered within a session). Sessions are the unit of sequence;
parameters are population-level. This is the offline overlay recommended in the
audit: it consumes existing telemetry unchanged.
"""
from __future__ import annotations

CLAMP = (0.001, 0.999)


def _clamp(value: float) -> float:
    return max(CLAMP[0], min(CLAMP[1], value))


def _forward(seq, p_l0, p_t, p_s, p_g):
    """Per-step (prior P(L), posterior P(L|obs)) plus final P(L) after updates."""
    p_l = p_l0
    states = []
    for obs in seq:
        p_correct = _clamp(p_l * (1 - p_s) + (1 - p_l) * p_g)
        posterior = _clamp(p_l * (1 - p_s) / p_correct if obs
                           else p_l * p_s / (1 - p_correct))
        states.append((p_l, posterior))
        p_l = posterior + (1 - posterior) * p_t
    return states, p_l


def fit_bkt(sequences, iterations=30):
    """Simplified EM fit. sequences: list of lists of 0/1.
    Returns (pL0, pT, pS, pG)."""
    p_l0, p_t, p_s, p_g = 0.3, 0.2, 0.1, 0.2
    for _ in range(iterations):
        n_seq = 0
        l0_expect = 0.0
        slip_when_known = correct_when_known = 0.0
        guess_when_unknown = incorrect_when_unknown = 0.0
        trans_num = trans_den = 0.0
        for seq in sequences:
            if not seq:
                continue
            n_seq += 1
            states, _final = _forward(seq, p_l0, p_t, p_s, p_g)
            l0_expect += states[0][1]
            for index, obs in enumerate(seq):
                prior, posterior = states[index]
                if obs:
                    correct_when_known += prior
                    guess_when_unknown += 1 - prior
                else:
                    slip_when_known += prior
                    incorrect_when_unknown += 1 - prior
                if index + 1 < len(seq):
                    next_prior = states[index + 1][0]
                    trans_den += 1 - posterior
                    trans_num += max(0.0, next_prior - posterior)
        if n_seq == 0:
            break
        p_l0 = _clamp(l0_expect / n_seq)
        known_total = correct_when_known + slip_when_known
        if known_total > 0:
            p_s = _clamp(slip_when_known / known_total)
        unknown_total = guess_when_unknown + incorrect_when_unknown
        if unknown_total > 0:
            p_g = _clamp(guess_when_unknown / unknown_total)
        if trans_den > 0:
            p_t = _clamp(trans_num / trans_den)
    return p_l0, p_t, p_s, p_g


def mastery_summary(sequences, params):
    """Mean final P(L) across sequences under fitted params."""
    p_l0, p_t, p_s, p_g = params
    finals = []
    for seq in sequences:
        if not seq:
            continue
        _states, final = _forward(seq, p_l0, p_t, p_s, p_g)
        finals.append(final)
    return sum(finals) / len(finals) if finals else None
