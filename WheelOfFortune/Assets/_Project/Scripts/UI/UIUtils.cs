using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace WheelOfFortune.UI
{
    public static class UIUtils
    {
        /// <summary>Finds a child Button by name. Called from OnValidate so button references are never set by hand.</summary>
        public static Button FindButton(Component root, string buttonName)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
                if (button.name == buttonName) return button;

            Debug.LogWarning($"Button '{buttonName}' not found under '{root.name}'.", root);
            return null;
        }

        /// <summary>12500 -> "12.5K", 3000000 -> "3M".</summary>
        public static string FormatAmount(int value)
        {
            if (value >= 1_000_000) return (value / 1_000_000f).ToString("0.#", CultureInfo.InvariantCulture) + "M";
            if (value >= 10_000) return (value / 1_000f).ToString("0.#", CultureInfo.InvariantCulture) + "K";
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
