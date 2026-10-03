# Characters: *Competent Person*

The story is one week on the Loblolly Creek Lift Station, a municipal sewer pump station in Autauga County, Alabama. The city needs it online before hurricane season, and every late day costs $2,500 in liquidated damages. Everyone is fictional.

The source of truth is `Assets/_Game/Scripts/Core/Cast.cs`. The talkable NPCs (Dolores, Ray) send **backstory + voice + today's beat** to the LLM persona. OSHA facts stay separate as `facts`, so the persona can never change a regulation.

## Theme

**Production vs. protection is not a villain story.** Nobody on this site wants anyone hurt. Hazards come from schedule, habit, broken equipment and "we've done it closer than this." The learner's job is to name the hazard and the fix clearly enough that good people change course. This mirrors the core loop: report → control → stop-work.

## The learner (you)

- Four years laying pipe for this contractor. OSHA 30 finished last month.
- Promoted mid-project when the previous competent person, Hank Doss, retired.
- **Tension:** last week Marcus and Luis were your crewmates. Now they answer to you, and so do their shortcuts.
- **Arc:** from "one of the crew" to the person the crew listens to. It is measured by the story, not a cutscene: Ray starts quoting you back (EP4) and calls all-stop himself (EP5).

## Principal cast

| Character | Who | Wound / pressure | Want | Voice | Arc by episode |
|---|---|---|---|---|---|
| **Dolores Villanueva**, 56 | Corporate site safety manager (CHST), your mentor. From Mobile, a shipyard welder's daughter, flagger since 1992, bilingual EN/ES | 2009: she signed a trench inspection in the morning. It rained at noon and the wall came down on laborer Tommy Greer. She retires this year and this is her last project | To leave behind one person who sees what she sees, then step back | Calm, dry, short sentences. Asks instead of tells, speaks Spanish with Luis, never lectures twice | EP1 watches · EP2 rain morning, sharper, names 2009 in the epilogue · EP3 tests the "boring" checks · EP4 stays at the trailer on purpose · EP5 won't make the call for you |
| **Ray Tillman**, 49 | General foreman, third-generation concrete man from Prattville | Liquidated damages, and he promised his crew the bonus. Believes experience beats paperwork, because it usually has | To pour Friday **and** send everyone home | Blunt, funny, Southern. Pushes back hard, then fixes it without apologizing | EP1 thinks you were promoted too early · EP2 a stop-work on Marcus feels personal · EP3 "The midrail was mine. Fixed it myself." · EP4 backs the signal person over the operator · EP5 calls all-stop under the line |

## Crew in the hazards

| Character | Episode | Where the hazard comes from (human, not careless) |
|---|---|---|
| **Marcus Bell**, 31, pipe layer, your old partner, two kids | EP2 | "I'll be two minutes": finishing a joint in the unshored gap past the trench box. Loyalty vs. duty |
| **Luis Ortega**, 38, laborer / saw operator, Spanish first | EP2 | The water pump on the saw cart quit, so he cuts dry to keep up (silica) |
| **Earl Whitfield**, 61, excavator operator, 40 years, never hit anyone | EP2 | A walkway was cut through his swing radius. "I can't see behind me. That's why you keep them out." |
| **Tasha Greene**, 34, ironworker foreman (Birmingham) | EP3–4 | The good example: always tied off. She noticed the midrail too and is watching whether you do |
| **Kiara Wells**, 27, qualified signal person | EP4 | Youngest on the crane crew, holding the radio while the operator wants to rush |
| **Dale Pruitt**, 45, pump owner-operator, paid by the yard | EP5 | "We've done it closer than this": boom setup near the overhead line |

## How the story follows play

- **Epilogue lines have `IfFound`.** They only play if the learner reported that hazard. Marcus's grudging "Thanks, I guess" needs the trench stop, and Ray's midrail confession needs the midrail report. Missed hazards show up in the debrief instead of being narrated as successes.
- **The LLM persona includes today's beat.** Ask Ray about the trench in EP2 and he is defensive about Marcus; ask him in EP5 and he has changed.
- **Portraits** (Higgsfield) are in `Resources/Cast/<id>.jpg` and appear on the menu's CREW tab.
