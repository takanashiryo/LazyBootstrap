using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace LazyBootstrap.Controls
{
    /// <summary>
    /// Shared helpers for code-behind manipulation of Avalonia controls,
    /// reused across feature views (Settings, Display, ...).
    /// </summary>
    internal static class ControlHelpers
    {
        public static double EaseInOutCubic(double progress)
        {
            return progress < 0.5d
                ? 4d * progress * progress * progress
                : 1d - Math.Pow(-2d * progress + 2d, 3d) / 2d;
        }

        public static void ReplaceComboBoxItems<T>(ComboBox comboBox, IEnumerable<T> items)
        {
            if (comboBox == null)
            {
                return;
            }

            var desiredItems = (items ?? Enumerable.Empty<T>()).Cast<object>().ToList();
            if (HasSameComboBoxItems(comboBox, desiredItems))
            {
                return;
            }

            ClearComboBoxSelection(comboBox);
            comboBox.Items.Clear();
            foreach (var item in desiredItems)
            {
                comboBox.Items.Add(item);
            }
        }

        private static bool HasSameComboBoxItems(ComboBox comboBox, IReadOnlyList<object> desiredItems)
        {
            if (comboBox.Items.Count != desiredItems.Count)
            {
                return false;
            }

            for (int i = 0; i < desiredItems.Count; i++)
            {
                var currentItem = comboBox.Items[i];
                var desiredItem = desiredItems[i];
                if (ReferenceEquals(currentItem, desiredItem) || Equals(currentItem, desiredItem))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static void ClearComboBoxSelection(ComboBox comboBox)
        {
            try
            {
                comboBox.SelectedIndex = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            try
            {
                comboBox.SelectedItem = null;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        public static void SetTextBoxTextIfNeeded(TextBox textBox, string value)
        {
            if (textBox == null)
            {
                return;
            }

            string normalizedValue = value ?? string.Empty;
            if (string.Equals(textBox.Text ?? string.Empty, normalizedValue, StringComparison.Ordinal))
            {
                return;
            }

            textBox.Text = normalizedValue;
        }
    }
}
