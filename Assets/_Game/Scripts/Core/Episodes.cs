using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // One narrated line in a cold open or epilogue. Speaker "" = on-screen caption (no name plate).
    public sealed class Line
    {
        public string Speaker { get; }
        public string Text { get; }
        public float Seconds { get; }
        public string IfFound { get; }   // epilogue: only plays if the learner reported this hazard (story follows play)
        public Line(string speaker, string text, float seconds = 4.5f, string ifFound = null) { Speaker = speaker; Text = text; Seconds = seconds; IfFound = ifFound; }
    }

    // Camera shot for an intro flythrough: moves from -> to while looking at a target (site metres).
    public sealed class CameraShot
    {
        public float[] From { get; }
        public float[] To { get; }
        public float[] LookAt { get; }
        public float Seconds { get; }
        public CameraShot(float[] from, float[] to, float[] lookAt, float seconds) { From = from; To = to; LookAt = lookAt; Seconds = seconds; }
    }

    // A topic-scoped episode (GDD §18). Each is playable on its own; the week is the story arc.
    public sealed class Episode
    {
        public int Number { get; }
        public string Title { get; }
        public string Topic { get; }
        public int DayIndex { get; }                  // 0 = Mon ... 4 = Fri (site phase)
        public string[] Standards { get; }            // 29 CFR 1926 anchors shown on the card
        public IReadOnlyList<Line> ColdOpen { get; }
        public IReadOnlyList<CameraShot> Shots { get; }
        public IReadOnlyList<Line> Epilogue { get; }
        public System.Func<IReadOnlyList<QuizItem>> ToolboxQuiz { get; }
        public System.Func<IReadOnlyList<QuizItem>> ClosingQuiz { get; }
        public bool Playable { get; }                 // false = hazards not built yet
        public float[] Zone { get; }                  // today's work zone x0,z0,x1,z1 (m); null = whole parcel

        public Episode(int number, string title, string topic, int day, string[] standards, Line[] coldOpen, CameraShot[] shots, Line[] epilogue,
            System.Func<IReadOnlyList<QuizItem>> toolbox, System.Func<IReadOnlyList<QuizItem>> closing, bool playable = true, float[] zone = null)
        {
            Zone = zone;
            Number = number; Title = title; Topic = topic; DayIndex = day; Standards = standards;
            ColdOpen = coldOpen; Shots = shots; Epilogue = epilogue; ToolboxQuiz = toolbox; ClosingQuiz = closing; Playable = playable;
        }
    }

    public static class Episodes
    {
        static float[] V(float x, float y, float z) => new[] { x, y, z };

        public static readonly IReadOnlyList<Episode> All = new[]
        {
            new Episode(1, "First Light", "Site entry · PPE · temporary power", 0,
                new[] { "1926.95", "1926.404(b)(1)", "1926.405(a)(2)", "1926.1053(b)(1)" },
                new[]
                {
                    new Line("", "Loblolly Creek Lift Station · Autauga County, Alabama · Monday, 6:40 AM"),
                    new Line("Dolores", "Morning. Hank's gone fishing for good. As of today you're the competent person on this job."),
                    new Line("Dolores", "That means you see it, you own it. Even when it's your old crew."),
                    new Line("Ray", "And it means we still pour Friday. Power's live, crews are plugging in. Don't slow me down."),
                    new Line("Dolores", "Sign in, suit up, and walk the temporary power. Anything that can bite a crew, you photograph it."),
                },
                new[]
                {
                    new CameraShot(V(-6, 14, -10), V(8, 9, -2), V(18, 0, 18), 6f),
                    new CameraShot(V(10, 1.8f, -1), V(12.5f, 1.7f, 1.2f), V(12.8f, 1.5f, 3.6f), 5f),
                    new CameraShot(V(26, 2.4f, 18), V(22, 1.6f, 20), V(20, 0.3f, 22.5f), 6f),
                },
                new[]
                {
                    new Line("Dolores", "Cords and outlets are the quiet killers. GFCI on every temporary circuit, damaged cords out of service."),
                    new Line("Ray", "Tomorrow we open the cut for the force main. Five feet and deeper. You'll want to be there."),
                },
                QuizBank.GateToolboxTalk, QuizBank.EndOfDayPower, zone: new[] { -4f, -4f, 42f, 42f }),

            new Episode(2, "The Cut", "Excavation · trenching · silica · struck-by", 1,
                new[] { "1926.652(a)(1)", "1926.651(j)(2)", "1926.651(c)(2)", "1926.1153" },
                new[]
                {
                    new Line("", "Tuesday, 7:05 AM · Force-main trench, station 2+40"),
                    new Line("Ray", "Earl's been digging since six. We're behind already."),
                    new Line("Ray", "Your buddy Marcus is down there setting pipe. Luis is cutting. Nobody needs a babysitter."),
                    new Line("Dolores", "Red clay, rained last night. Until we test it, we treat it as Type C. A cubic yard weighs as much as a car."),
                    new Line("Dolores", "Check the box, the spoil, the way out, and who's standing in that swing radius."),
                },
                new[]
                {
                    new CameraShot(V(80, 16, 14), V(74, 10, 22), V(66, 0, 34), 6f),
                    new CameraShot(V(66, 3.2f, 16), V(66, 2.0f, 21), V(66, -1.6f, 27), 5f),
                    new CameraShot(V(56, 3, 36), V(57, 2.2f, 33), V(61.5f, 1.2f, 30.5f), 6f),
                },
                new[]
                {
                    new Line("Marcus", "Two minutes, I said. ...Yeah. Okay. Thanks, I guess.", ifFound: "tue-no-protective-system"),
                    new Line("Dolores", "Trenches don't warn you. I learned that the hard way in 2009. Protective system at five feet, spoil two feet back, a ladder within 25."),
                    new Line("Ray", "Deck's formed on the pump house. Crew's up top tomorrow. Holes everywhere."),
                },
                QuizBank.ToolboxTrench, QuizBank.EndOfDayTrench, zone: new[] { -4f, -4f, 80f, 58f }),

            new Episode(3, "The Edge", "Falls · floor holes · guardrails · ladders", 2,
                new[] { "1926.501(b)(1)", "1926.502(b)", "1926.502(i)", "1926.1053(b)(1)" },
                new[]
                {
                    new Line("", "Wednesday, 9:20 AM · Pump-house deck, 14 ft above grade"),
                    new Line("Dolores", "Falls are still the number one killer in our trade. Every single year."),
                    new Line("Ray", "Rails went up yesterday. Mostly."),
                    new Line("Tasha", "Mostly. I've got my crew tied off. Yours I can't speak for, Ray."),
                    new Line("Dolores", "'Mostly' is how people end up on the slab. Walk the edge. Look down every hole."),
                },
                new[]
                {
                    new CameraShot(V(30, 18, 16), V(36, 13, 22), V(46, 4, 32), 6f),
                    new CameraShot(V(30.5f, 1.6f, 30), V(31, 4.6f, 30.5f), V(33.6f, 4.8f, 32), 5f),
                    new CameraShot(V(45.5f, 6.1f, 33), V(44.5f, 6f, 32), V(41.6f, 4.2f, 30.6f), 6f),
                },
                new[]
                {
                    new Line("Dolores", "Midrails, covers that are secured and marked, ladders three feet past the landing. Boring saves lives."),
                    new Line("Ray", "The midrail was mine. Fixed it myself. Don't make it a thing.", ifFound: "wed-missing-midrail"),
                    new Line("Ray", "Crane shows up tomorrow for the roof steel. Big pick."),
                },
                QuizBank.ToolboxFalls, QuizBank.EndOfDayFalls, zone: new[] { -4f, -4f, 60f, 48f }),

            new Episode(4, "The Pick", "Cranes · rigging · roof work", 3,
                new[] { "1926.1402", "1926.1419", "1926.1424", "1926.501(b)(10)" },
                new[] { new Line("", "Thursday · In production") }, new CameraShot[0], new Line[0],
                QuizBank.GateToolboxTalk, QuizBank.EndOfDayFalls, playable: false),

            new Episode(5, "Under the Line", "Capstone · power lines · concrete pump", 4,
                new[] { "1926.1408", "1926.416(a)(1)", "1926.702" },
                new[] { new Line("", "Friday · In production") }, new CameraShot[0], new Line[0],
                QuizBank.GateToolboxTalk, QuizBank.EndOfDayTrench, playable: false),
        };

        public static Episode Get(int number) => All.First(e => e.Number == number);
    }
}
