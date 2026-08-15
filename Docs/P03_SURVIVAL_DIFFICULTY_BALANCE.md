# P03 - Survival / Difficulty Balance

## Current balance before P03

- Initial Time: 30 seconds
- Maximum Time: 60 seconds
- Drain: 1 second per active second
- Normal / Fast / Perfect recovery: 3 / 5 / 8 seconds
- Grade thresholds: Perfect at 2 seconds or less; Fast at 4 seconds or less; otherwise Normal
- Tier cadence: five committed correct cycles
- Target ranges: 5-10, 8-15, 10-20; the final tier repeats indefinitely
- Fever: no direct Time modifier
- Pause: stops drain and active-duration accumulation
- Result duration: active Run time only

The previous curve strongly favored Time growth. At the grade boundaries, ignoring extra recognition/resolution time, Normal at 4+ seconds was approximately -1 second or worse, Fast at 4 seconds was +1 second, and Perfect at 2 seconds was +6 seconds. A skilled player could repeatedly refill the 60-second cap after difficulty stopped changing at ten correct answers.

## Temporary P03 playtest values

| Balance value | Before | After |
| --- | ---: | ---: |
| Initial Time | 30 | 35 |
| Maximum Time | 60 | 45 |
| Drain per active second | 1 | 1 |
| Normal recovery | 3 | 1.5 |
| Fast recovery | 5 | 2.75 |
| Perfect recovery | 8 | 4 |
| Correct cycles per tier | 5 | 6 |

Grade thresholds remain 2/4 seconds. Product rules, Fever, Combo, Board values, obstacles, and path rules are unchanged.

## Temporary difficulty curve

| Tier | Completed correct cycles on entry | Proven target range |
| ---: | ---: | ---: |
| 1 | 0 | 5-9 |
| 2 | 6 | 7-11 |
| 3 | 12 | 9-13 |
| 4 | 18 | 11-15 |
| 5 | 24 | 13-16 |

Tier 5 remains active after the maximum is reached. It never indexes beyond configuration and target recovery continues to require a current legal witness. The maximum of 16 respects the current 1-4 values and maximum four-block search path; larger configured numbers would mostly be clipped rather than provide meaningful difficulty.

## Economy estimates

Net Time per answer is `Recovery - active seconds spent`, because drain is 1.

- Beginner example: 6 seconds, Normal => -4.5 seconds. The 35-second start supports roughly 7-9 learning answers, approximately 45-60 active seconds.
- Average example: 3 seconds, Fast => -0.25 seconds. Mixed Fast/Normal play should reach several tiers before pressure produces a decline, with an initial playtest goal of roughly 90-180 seconds.
- Skilled example: 1.5 seconds, Perfect => +2.5 seconds early. The 45-second cap limits banking; later 13-16 targets should move more answers out of Perfect. The initial playtest goal is roughly 3-6 minutes, not guaranteed termination against arbitrarily fast theoretical play.

Fever can indirectly improve survival by making answers easier/faster and enabling expanded resolution, but it grants no Time multiplier, recovery bonus, or drain reduction. Combo has no direct Survival Time effect.

## Risks and human validation

- Target range is only a proxy for decision difficulty. Candidate count and recognition complexity are not scored in P03.
- A highly optimized player who continues solving maximum-tier targets under two seconds may still sustain indefinitely. Address only with playtest evidence; do not add an unapproved drain ramp.
- Verify portrait Time feedback, visible grade/recovery differences, tier transitions, maximum-tier solvability, exact Run End, pause exclusion, and clean Play Again.
- Target playtest ranges are tuning goals, not product guarantees.

## Status

Core P03 tuning and deterministic validation are implemented. Unity Play Mode balance validation remains manual.
