using System.Text.RegularExpressions;
using UnityEngine;

namespace PLAYERTWO.ARPGProject
{
    public static class StringExtensions
    {
        /// <summary>
        /// Returns a given string in <b>Title Case</b>.
        /// </summary>
        public static string ToTitleCase(this string input)
        {
            var result = Regex.Replace(input, @"([a-z])([A-Z])", "$1 $2");
            result = Regex.Replace(result, @"([A-Z])([A-Z][a-z])", "$1 $2");
            result = char.ToUpper(result[0]) + result[1..];
            return result;
        }

        /// <summary>
        /// Returns a given string with an color assigned using rich text formatting.
        /// This is useful for displaying colored text in Unity UI.
        /// </summary>
        /// <param name="color">The color you want to set.</param>
        public static string WithColor(this string input, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGB(color);
            return $"<color=#{hex}>{input}</color>";
        }
    }
}
