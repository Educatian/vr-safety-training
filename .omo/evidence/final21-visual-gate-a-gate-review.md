# Final21 Visual Gate A

- recommendation: REJECT
- originalIntent: Deliver a polished VR construction-safety training build whose fresh 60-frame capture set proves clean HUD framing, natural NPC motion/PPE, visible hands-on drag interaction, and correctly modeled safety assets.
- desiredOutcome: Every final21 frame is visually credible and unobstructed; the explicitly named prior defects are absent.
- userOutcomeReview: The set shows broad improvement and several fixes, but four explicit prior defects remain visible. The current artifact therefore does not yet satisfy the user's requested visual quality bar.

## Blockers

1. **VQ-03B-DRAG**
   - violatedCriterion: `03b full drag cue/glove/ray/cart/target visible and no HUD crop`
   - evidencePointer: `C:/tmp/vr-safety-golden-captures-final21/03b-construction-hands-on.png`
   - observation: A huge Safety Coach panel is cropped by the right and bottom frame edges and covers most of the view. The glove, ray, cart, and target are not visible as a complete drag interaction.

2. **VQ-NPC-GAIT**
   - violatedCriterion: `all NPC rest/walk/dialogue natural, no elongated feet`
   - evidencePointer: `C:/tmp/vr-safety-golden-captures-final21/06-warehouse-npc-walking-a.png`; same defect in `10-chemical-npc-walking-a.png` and `12-electrical-npc-walking-a.png`
   - observation: The forward boot is stretched into an implausibly long ribbed tan shape in multiple walking-A frames.

3. **VQ-REBAR-CAPS**
   - violatedCriterion: `rebar shows orange caps attached both ends`
   - evidencePointer: `C:/tmp/vr-safety-golden-captures-final21/asset-construction-us_capped_rebar_bundle.png`
   - observation: The capture shows only one bundle end, with bright red circular caps. It does not evidence caps attached to both ends, and an isolated orange cap-like object is visible away from the bundle.

4. **VQ-FIRE-EXTINGUISHER**
   - violatedCriterion: `extinguisher English ABC/PASS label plus gauge visible`
   - evidencePointer: `C:/tmp/vr-safety-golden-captures-final21/asset-fire-us_abc_fire_extinguisher.png`
   - observation: The visible face is a plain dark-red rectangle. No English ABC/PASS label or pressure gauge is visible.

## Verified good

- 02a mission board is not masked by an NPC.
- 02c has no prior stray-label bleed.
- Dialogue panels are readable and do not cover characters.
- Chemical/electrical PPE appears attached.
- Exit-door panic bar and NEMA front face are visible.
- Dock leveler and IBC tote are unobscured.
- Engineering/HMI panels remain readable.

## Checked artifacts

- All 60 originals inspected through 15 mapped four-frame sheets.
- Original-resolution rechecks: 02a, 03b, capped rebar, ABC extinguisher, warehouse/chemical/electrical walking-A.
- Hygiene: 60/60 PNG, 1168x692 RGB, 60 unique SHA-256 hashes.

## Exact evidence gaps

- No successful frame demonstrates the entire drag interaction without HUD cropping.
- No rebar closeup demonstrates capped geometry on both ends.
- No extinguisher closeup shows the required English label and gauge.
- Multiple gait frames show deformed/oversized footwear.

## Slop/programming perspective

Strict read-only visual gate; no production diff or test report was supplied for code-level re-review. Code-level slop/maintenance observations are N/A to this visual-only recommendation.
