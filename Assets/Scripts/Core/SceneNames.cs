using System.Collections.Generic;

namespace ColorfulSort.Core
{
    /// <summary>
    /// The three scenes, named once. A scene name is structure rather than content, so
    /// it stays in code — but in exactly one file, because the Editor bootstrapper fills
    /// Build Settings from <see cref="BuildOrder"/>: the list the player boots into and
    /// the names <see cref="SceneFlowService"/> asks for cannot drift apart.
    /// </summary>
    public static class SceneNames
    {
        /// <summary>Persistent services, loaded single and never unloaded. First in Build Settings.</summary>
        public const string Boot = "Boot";

        /// <summary>Main menu, loaded additively over Boot.</summary>
        public const string Menu = "Menu";

        /// <summary>The playable board, loaded additively over Boot.</summary>
        public const string Game = "Game";

        private static readonly string[] Order = { Boot, Menu, Game };

        /// <summary>Build Settings order, Boot first — it is the scene a build starts in.</summary>
        public static IReadOnlyList<string> BuildOrder => Order;
    }
}
