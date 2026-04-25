using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class GameTags
    {
        public static string Untagged = "Untagged";
        public static string Player = "Entity/Player";
        public static string Enemy = "Entity/Enemy";
        public static string Summoned = "Entity/Summoned";
        public static string Interactive = "Interactive";
        public static string Destructible = "Destructible";
        public static string Collectible = "Collectible";

        /// <summary>
        /// Returns true if the Component is untagged.
        /// </summary>
        public static bool IsUntagged(this Component component) =>
            component && component.CompareTag(Untagged);

        /// <summary>
        /// Returns true if the Component is an Entity.
        /// </summary>
        public static bool IsEntity(this Component component) =>
            component
            && (
                component.CompareTag(Player)
                || component.CompareTag(Enemy)
                || component.CompareTag(Summoned)
            );

        /// <summary>
        /// Returns true if the Component is a Player.
        /// </summary>
        public static bool IsPlayer(this Component component) =>
            component && component.CompareTag(Player);

        /// <summary>
        /// Returns true if the Component is an Enemy.
        /// </summary>
        public static bool IsEnemy(this Component component) =>
            component && component.CompareTag(Enemy);

        /// <summary>
        /// Returns true if the Component is a Summoned Entity.
        /// </summary>
        public static bool IsSummoned(this Component component) =>
            component && component.CompareTag(Summoned);

        /// <summary>
        /// Returns true if the Component is a Player.
        /// </summary>
        public static bool IsTarget(this Component component) =>
            component && (component.CompareTag(Enemy) || component.CompareTag(Destructible));

        /// <summary>
        /// Returns true if the Component is a Interactive.
        /// </summary>
        public static bool IsInteractive(this Component component) =>
            component && (component.CompareTag(Interactive) || component.CompareTag(Collectible));

        /// <summary>
        /// Returns true if the Component is a Collectible.
        /// </summary>
        public static bool IsCollectible(this Component component) =>
            component && component.CompareTag(Collectible);

        /// <summary>
        /// Returns true if the Component is a Destructible.
        /// </summary>
        public static bool IsDestructible(this Component component) =>
            component && component.CompareTag(Destructible);

        /// <summary>
        /// Returns true if the Component has a tag in the tag list.
        /// </summary>
        /// <param name="list">The tag list you want to check.</param>
        public static bool InTagList(this Component component, List<string> list) =>
            list != null && list.Contains(component.tag);
    }
}
