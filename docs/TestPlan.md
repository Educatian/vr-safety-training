# Test Plan — v2

## Automated
- **EditMode (Core):**
  - `ZoneSession` scoring (+100 correct classify, +20 per control, −25 first look-alike, repeats 0).
  - Control gating: no control before a correct classify, and no later step before the current one.
  - Stop-work is required where authored.
  - Every `HazardDefinition` has a CFR reference and an explanation, and each zone has ≥ 3 hazards and ≥ 2 look-alikes.
- **EditMode (Scene):**
  - Every `HazardTarget` in `Jobsite.unity` resolves to a definition.
  - No missing materials (pink) and no missing scripts.
  - Every control destination lies on a walkable surface.
- **PlayMode smoke:** scripted desktop run completes all zones with no console errors.

## Measured
- 90 fps average on the PCVR target at 2016×2240 per eye, and 60 fps on a desktop 1080p fallback on an integrated-class GPU.
- Visible triangles ≤ 2.5M at worst viewpoints: the trench edge looking at the equipment yard, and the roof overview.

## Visual (critic)
- Capture 12 fixed viewpoints per milestone.
- The critic judges against GDD §4:
  - no untextured primitive in view,
  - hazards not distinguishable by art quality,
  - signage legible at 3 m.

## SME
- An OSHA-competent reviewer (Anisha Deria or a lab partner) walks through every `HazardDefinition` before any study deployment.
