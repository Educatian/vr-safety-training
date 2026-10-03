namespace Jobsite.Core
{
    // Development credits (menu > CREDITS, and after the Friday capstone). Edit the names and roles here; the logos
    // are Resources/Credits/addie_lab.png and ua_coe.png (flattened on white for the logo card).
    public static class Credits
    {
        public sealed class Section
        {
            public readonly string Title; public readonly string[] Lines;
            public Section(string title, params string[] lines) { Title = title; Lines = lines; }
        }

        public const string Studio = "AdDIE Lab · Adaptive Design of Immersive E-Learning";
        public const string Home = "College of Education, The University of Alabama";

        public static readonly Section[] Sections =
        {
            new Section("CREATED BY",
                "Jewoong Moon",
                "Project lead · game and learning design · development",
                "",
                Studio,
                Home,
                "",
                "AI development partner: Claude (Anthropic)"),
            new Section("LEARNING DESIGN",
                "Evidence-centered design: every scored action is evidence for one of 14 competent-person competencies, each anchored to 29 CFR 1926",
                "",
                "Built for construction safety and construction management courses",
                "",
                "Play data leaves the device only with the learner's opt-in consent"),
            new Section("ASSETS AND TOOLS",
                "Unity 6 (URP) · WebGL",
                "Crew avatars: Microsoft Rocketbox (MIT license), Gonzalez-Franco et al., 2020",
                "Textures, sky and trees: Poly Haven (CC0)",
                "Vehicles and props: Tripo AI",
                "Tablet and icon art: Higgsfield",
                "Font: Barlow Condensed (SIL Open Font License 1.1)"),
        };

        public const string Footer = "Training simulation. It does not issue an OSHA 10/30 card or a competent-person designation.";
    }
}
