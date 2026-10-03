using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace WaveSurvival.Tools.SpriteLibraryBuilder
{
    public static class SpriteLibraryExporter
    {
        public static bool Export(CategorySet set, Sprite[] sprites, string outputFolder, string libraryName)
        {
            if (!Validate(set, sprites, outputFolder, libraryName)) return false;
            var categories = BuildCategories(set, sprites);
            if (categories == null) return false;
            return CreateAndImport($"{outputFolder}/{SanitizeName(libraryName)}", categories, null);
        }

        public static bool ExportOverride(CategorySet set, Sprite[] sprites, string outputFolder, string libraryName, SpriteLibraryAsset parentLibrary)
        {
            if (parentLibrary == null)
                return Fail("Parent library is null.");

            string parentPath = AssetDatabase.GetAssetPath(parentLibrary);
            if (string.IsNullOrEmpty(parentPath))
                return Fail("Parent library has no asset path.");

            if (!Validate(set, sprites, outputFolder, libraryName)) return false;
            var categories = BuildOverrideCategories(set, sprites, parentLibrary);
            if (categories == null) return false;
            return CreateAndImport($"{outputFolder}/{SanitizeName(libraryName)}", categories, parentPath);
        }

        private static bool Validate(CategorySet set, Sprite[] sprites, string outputFolder, string libraryName)
        {
            if (set == null || sprites == null || sprites.Length == 0)
            {
                Fail("Sprites are empty.");
                return false;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                Fail("Output folder is not set.");
                return false;
            }

            if (string.IsNullOrEmpty(libraryName))
            {
                Fail("Library name is empty.");
                return false;
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
            {
                Fail($"Output folder is not a valid asset folder: {outputFolder}");
                return false;
            }

            return true;
        }

        private static List<SpriteLibraryCategory> BuildCategories(CategorySet set, Sprite[] sprites)
        {
            for (int i = 0; i < set.categories.Count; i++)
            {
                if (set.categories[i].labels.Count == 0) continue;
                if (set.HasDuplicateLabelName(i, out string dup))
                {
                    Fail($"Category '{set.categories[i].name}' has duplicate label name '{dup}'.");
                    return null;
                }
            }

            var categories = new List<SpriteLibraryCategory>();
            foreach (var category in set.categories)
            {
                string categoryName = category.name?.Trim();
                if (string.IsNullOrEmpty(categoryName) || category.labels.Count == 0)
                    continue;

                var labels = new List<SpriteLibraryLabel>();
                foreach (var label in category.labels)
                {
                    if (label.spriteIndex < 0 || label.spriteIndex >= sprites.Length) continue;
                    if (string.IsNullOrEmpty(label.name)) continue;
                    labels.Add(new SpriteLibraryLabel(label.name, sprites[label.spriteIndex]));
                }

                if (labels.Count == 0)
                {
                    Debug.LogWarning($"[SpriteLibraryBuilder] Category '{category.name}' has no valid labels; skipped.");
                    continue;
                }

                categories.Add(new SpriteLibraryCategory(categoryName, labels));
            }

            if (categories.Count == 0)
            {
                Fail("No category has at least one valid label.");
                return null;
            }

            return categories;
        }

        private static List<SpriteLibraryCategory> BuildOverrideCategories(CategorySet set, Sprite[] sprites, SpriteLibraryAsset parentLibrary)
        {
            var categories = new List<SpriteLibraryCategory>();

            foreach (var category in set.categories)
            {
                string categoryName = category.name?.Trim();
                if (string.IsNullOrEmpty(categoryName) || category.labels.Count == 0)
                    continue;

                var validChildLabels = new List<LabelDefinition>();
                foreach (var label in category.labels)
                {
                    if (label.spriteIndex < 0 || label.spriteIndex >= sprites.Length) continue;
                    validChildLabels.Add(label);
                }

                if (validChildLabels.Count == 0)
                {
                    Debug.LogWarning($"[SpriteLibraryBuilder] Category '{category.name}' has no valid labels; skipped.");
                    continue;
                }

                var parentLabels = parentLibrary.GetCategoryLabelNames(categoryName).ToArray();
                var labels = new List<SpriteLibraryLabel>();

                for (int i = 0; i < validChildLabels.Count; i++)
                {
                    string name = i < parentLabels.Length ? parentLabels[i] : validChildLabels[i].name;
                    labels.Add(new SpriteLibraryLabel(name, sprites[validChildLabels[i].spriteIndex]));
                }

                if (validChildLabels.Count < parentLabels.Length)
                {
                    Debug.LogWarning(
                        $"[SpriteLibraryBuilder] Category '{categoryName}' overrides {validChildLabels.Count}/{parentLabels.Length} labels; the rest inherit from parent.");
                }

                categories.Add(new SpriteLibraryCategory(categoryName, labels));
            }

            if (categories.Count == 0)
            {
                Fail("No category has at least one valid label.");
                return null;
            }

            return categories;
        }

        private static bool CreateAndImport(string pathWithoutExtension, List<SpriteLibraryCategory> categories, string mainLibraryPath)
        {
            string relativePath;
            try
            {
                relativePath = SpriteLibrarySourceAssetFactory.Create(pathWithoutExtension, categories, mainLibraryPath);
            }
            catch (System.Exception e)
            {
                return Fail($"Failed to create sprite library: {e.Message}");
            }

            AssetDatabase.ImportAsset(relativePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SpriteLibraryBuilder] Created sprite library: {relativePath}");
            return true;
        }

        private static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static bool Fail(string message)
        {
            Debug.LogError($"[SpriteLibraryBuilder] {message}");
            return false;
        }
    }
}