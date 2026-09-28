# Prop Bible: initial safety-realism candidates

**Status:** Initial candidate guide, not certified compliance, an engineering approval, or a complete equipment catalog. Official OSHA pages reviewed September 28, 2026. Quotations below are verbatim excerpts; scenario applicability and complete requirements still require review.

## Baseline and evidence boundaries

The user-approved baseline is **Educatian/vr-safety-training**, local branch **revamp/v2**, inspected HEAD **a32eaed9e80aba7f9b44f850f9a6dd35c644252b**, **Unity 6000.3.25f1 / URP 17.3.0**. These values were read from the actual local project. The approved serious game is *Competent Person*. This guide does not restore the old five-site/Quest implementation.

`Tools/blender/guardrail.py` is **authored but NOT RUN**. No generated mesh, import, scene placement, dimensions, collision behavior, rendering, or runtime behavior has been verified here. Distinguish **verified source requirements**, **authored candidates**, and **PENDING validation**. Appearance, asset names, and a correctly sized mesh do not establish real-world compliance.

## 1. Exterior guardrail candidate

Source: [OSHA 1926.502, Fall protection systems criteria and practices](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.502).

- **1926.502(b)(1):** “Top edge height of top rails, or equivalent guardrail system members, shall be 42 inches (1.1 m) plus or minus 3 inches (8 cm) above the walking/working level.” This measures the **top edge**, not the tube centerline. The paragraph also addresses higher rails when conditions warrant and stilts.
- **(b)(2), (b)(2)(i):** Intermediate protection is required where there is no wall/parapet at least 21 inches high. “Midrails, when used, shall be installed at a height midway between the top edge of the guardrail system and the walking/working level.” Screens, mesh, or qualifying intermediate members can be alternatives; absence of a midrail alone is not a universal failure criterion.
- **(b)(3):** “a force of at least 200 pounds (890 N)” applies within 2 inches of the top edge, outward or downward, at any point along it. **(b)(4)** requires the top edge not to deflect below 39 inches under the specified downward test load. **(b)(5)** specifies at least 150 pounds for intermediate members.
- **(b)(6)-(9):** Review puncture/laceration and clothing-snag hazards, terminal overhangs, prohibited banding, and minimum nominal rail diameter/thickness. These are not satisfied merely by choosing a visually substantial tube.

**Authored candidate:** 2 m overall width, 0.18 m plate depth, 0.05 m tube exterior, 42-inch top edge (1.0668 m), and midpoint midrail (nominally 21 inches/0.5334 m). Top-edge and midpoint targets are source-grounded; all other geometric choices are **authored, unverified**, not OSHA-prescribed product dimensions. Strength, tube wall thickness, material specification, base stability, fasteners, and anchorage are **NOT VERIFIED**.

Variants: **clean**, **service_worn**, **missing_midrail**, **missing_midrail_worn**. Clean does not mean compliant; surface wear does not prove structural failure. Use missing_midrail as a candidate hazard only where scenario review establishes that required intermediate protection is absent, including equivalent alternatives. Before use, measure the imported geometry relative to the actual walking surface and review connections, gaps, ends, placement, and applicable edge/opening conditions. Blender execution and Unity validation remain **PENDING**.

### Toeboard and attachment details for T2.1

The same official source, **1926.502(j)(3)**, states: “Toeboards shall be a minimum of 3½ inches (9 cm) in vertical height from their top edge to the level of the walking/working surface.” Clearance is at most ¼ inch; boards must be solid or have openings at most 1 inch in greatest dimension. Paragraph **(j)(2)** requires 50 lb load resistance, which is NOT established by a mesh.

The authored candidate uses a solid board with its top edge at 0.0889 m (3½ in), 0.005 m ground clearance, and body height 0.0839 m. This explicitly measures the top from the walking surface, rather than adding the clearance to the required height. Toeboard applicability and any additional screening remain scenario-specific. Posts, clamp sleeves, bolt heads, board brackets and plates are illustrative connection geometry, not an engineered fastening design. **1926.502(d)(23)** prohibits attachment of personal fall-arrest systems to guardrail systems; this model must not be offered as a harness anchorage.

## 2. Exposed reinforcing steel and legacy caps

Source: [OSHA 1926.701(b)](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.701): “All protruding reinforcing steel, onto and into which employees could fall, shall be guarded to eliminate the hazard of impalement.”

Interpretation: [May 29, 1997 clarification, with January 15, 1997 memorandum](https://www.osha.gov/laws-regs/standardinterpretations/1997-05-29-0).

- January memorandum: “the standard mushroom style plastic rebar CAPS should not be used for protection against impalement.”
- May clarification: “There is no change in OSHA policy nor is there a ban on the general use of the small plastic rebar caps as recommended by their manufacturer.” It further states: “Rebar caps/covers are appropriate to prevent cuts, abrasions or other minor injuries when working at grade and there is no impalement hazard.”

**Required semantic/visual review:** Determine the represented product, protective function, manufacturer evidence, employee exposure, and fall path. Do not classify a particular source cap as compliant or unsafe from an image, orange color, mushroom-like silhouette, or name alone. A generic cap must not be taught as verified impalement protection.

**Preserve source compatibility until the planned T1.7 migration:** Existing source rods use the legacy name **Orange impalement cap**, and the Editor-side dresser searches that exact name. Do not rename only one side of this dependency; any future v1 removal must migrate or remove both sides and be regression-tested. Record the semantic concern in review documentation instead. Product identity, protective capability, and scenario-specific classification are **PENDING**.

## 3. Signs and tags

Source: [OSHA 1926.200](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.200).

- **(a):** Required signs/symbols “shall be visible at all times when work is being performed” and removed/covered promptly when hazards cease.
- **(b)(1):** “Danger signs shall be used only where an immediate hazard exists”. **(b)(2)** specifies predominantly red upper panel, black border outlines, and white lower panel.
- **(c)(1):** Caution signs address potential hazards or unsafe practices. **(c)(2)-(3)** specify yellow/black layout and colors. **(e)** specifies white safety-instruction signs with a green upper panel and white principal-message lettering.
- **(h)(1):** Temporary accident-prevention tags “shall not be used in place of, or as a substitute for, accident prevention signs.”
- **(g)(2):** Worker-protection traffic control devices “shall conform to Part 6 of the MUTCD”. **(b)(1), (c)(1), (h)(2), (i)** reference specific ANSI editions and additional rules.

**Candidate rule:** Choose the sign class from the scenario hazard, preserve readable contrast, and assess visibility in-headset. Do not label an authored texture ANSI-certified. **ANSI/ISEA/MUTCD applicability, incorporated-edition details, and asset-specific conformity are PENDING**; this review identifies OSHA references but does not validate external standards or ISEA requirements.

## 4. Portable ladders

Source: [OSHA 1926.1053](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.1053).

- **(a)(2):** Rungs/steps must be “parallel, level, and uniformly spaced” in use. **(a)(3)(i)** generally specifies 10-14 inches center-to-center, with exceptions in **(a)(3)(ii)-(iii)**. **(a)(4)(ii)** specifies at least 11½ inches clear between portable-ladder rails.
- **(a)(6)(ii), (a)(8):** Review slip-resistant portable-metal steps and stepladder spreader/locking device.
- **(b)(1):** For upper-landing access, side rails generally extend at least 3 feet above the landing; if ladder length prevents this, the stated rigid top securing and grasping-device alternative applies.
- **(b)(5)(i):** For non-self-supporting ladders, horizontal distance to the foot is “approximately one-quarter of the working length of the ladder”; this is not one-quarter of vertical height. **(b)(6), (b)(9)** address stable/level support or securing, and clear top/bottom areas.
- **(b)(13):** “The top or top step of a stepladder shall not be used as a step.” **(b)(15)-(16)** address inspection and defective portable ladders' identification and withdrawal from service.

**PENDING:** Exact ladder type, manufacturer dimensions/load rating, modeled measurements, condition, and placement. Geometry alone does not verify load capacity under **(a)(1)**.

## 5. Generator electrical candidate

Source: [OSHA 1926.404](https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.404).

**(b)(1)(i)** requires either the specified GFCIs or an assured equipment grounding conductor program. **(b)(1)(ii)** addresses employee-used, temporary 120-V single-phase 15/20-A receptacles and a narrowly conditioned two-wire, single-phase generator exception at not more than 5 kW, with conductors insulated from frame/grounded surfaces. Do not infer this exception from a prop's size.

**(f)(3)(i)(A)-(B)** condition the portable-generator frame grounding exception on supplied equipment/connections and bonding of noncurrent-carrying metal parts and receptacle grounding terminals. **(f)(3)(iii):** “A neutral conductor shall be bonded to the generator frame if the generator is a component of a separately derived system.” **(e)(1)(vi)(A):** “Circuit breakers shall clearly indicate whether they are in the open (off) or closed (on) position.”

**PENDING:** Generator/cart exact dimensions; generator model, rating, wiring, bonding, supply configuration, protective devices, and manuals. Do not add a grounding rod as a universal compliance cue. **Pressure gauges, operating ranges, labels, and manufacturer manuals are PENDING**. No invented specifications, certification marks, or operational readings are authorized by this guide.

## Production and learning constraints for the approved v2

3D generation: **Tripo only**, followed by Blender review. Higgsfield: layout/images/cinematics/audio, never 3D. Compliance-critical geometry: parametric Blender. Layout boards require the approval gate in Tasks T1.3a before greybox. No paid generation was performed in this change.

Export B-PROC models to `Assets/_Game/Art/Models/B-PROC/` with `SM_` prefixes. Project budgets are 40k triangles per hero and 8k per clutter prop, 2K/1K textures, and a PCVR 90-fps target; these are budgets, not measured performance. Quest standalone is out of scope.

Keep weathering comparable between hazards and look-alikes, no pre-highlighted answers, no unrelated animation in the active-hazard view cone, and at most 12 words per learner UI card. The deterministic Jobsite.Core engine owns recognition/risk/control/escalation, including the rule that stopping work never reduces CP rating. An in-game achievement is not employer designation or professional certification.
