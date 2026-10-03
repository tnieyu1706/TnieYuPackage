using System.Collections.Generic;

namespace WaveSurvival.Tools.SpriteLibraryBuilder
{
    public class CategorySet
    {
        public readonly List<CategoryDefinition> categories = new List<CategoryDefinition>();
        public int selectedIndex = -1;
        private int categoryNameCounter;

        public CategoryDefinition SelectedCategory =>
            selectedIndex >= 0 && selectedIndex < categories.Count ? categories[selectedIndex] : null;

        public int AddCategory()
        {
            var category = new CategoryDefinition($"Category_{categoryNameCounter}");
            categoryNameCounter++;
            categories.Add(category);
            selectedIndex = categories.Count - 1;
            return selectedIndex;
        }

        public void Clear()
        {
            categories.Clear();
            selectedIndex = -1;
            categoryNameCounter = 0;
        }

        public int AddCategoryNamed(string name)
        {
            var category = new CategoryDefinition(name);
            categories.Add(category);
            selectedIndex = categories.Count - 1;
            return selectedIndex;
        }

        public void RemoveCategory(int index)
        {
            if (index < 0 || index >= categories.Count) return;
            categories.RemoveAt(index);
            if (categories.Count == 0)
            {
                selectedIndex = -1;
                return;
            }
            if (selectedIndex >= categories.Count)
                selectedIndex = categories.Count - 1;
        }

        public void RenameCategory(int index, string newName)
        {
            if (index < 0 || index >= categories.Count) return;
            categories[index].name = newName;
        }

        public void ToggleSprite(int categoryIndex, int spriteIndex, string spriteName)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return;
            var labels = categories[categoryIndex].labels;
            int labelIndex = FindLabelBySprite(categoryIndex, spriteIndex);
            if (labelIndex >= 0)
                labels.RemoveAt(labelIndex);
            else
                labels.Add(new LabelDefinition(spriteName, spriteIndex));
        }

        public int FindLabelBySprite(int categoryIndex, int spriteIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return -1;
            var labels = categories[categoryIndex].labels;
            for (int i = 0; i < labels.Count; i++)
                if (labels[i].spriteIndex == spriteIndex) return i;
            return -1;
        }

        public bool ContainsSpriteInCategory(int categoryIndex, int spriteIndex)
        {
            return FindLabelBySprite(categoryIndex, spriteIndex) >= 0;
        }

        public bool ContainsSpriteInAnyCategory(int spriteIndex)
        {
            for (int i = 0; i < categories.Count; i++)
                if (ContainsSpriteInCategory(i, spriteIndex)) return true;
            return false;
        }

        public void RenameLabel(int categoryIndex, int labelIndex, string newName)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return;
            var labels = categories[categoryIndex].labels;
            if (labelIndex < 0 || labelIndex >= labels.Count) return;
            labels[labelIndex].name = newName;
        }

        public void RemoveLabel(int categoryIndex, int labelIndex)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return;
            var labels = categories[categoryIndex].labels;
            if (labelIndex < 0 || labelIndex >= labels.Count) return;
            labels.RemoveAt(labelIndex);
        }

        public void MoveLabel(int categoryIndex, int from, int to)
        {
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return;
            var labels = categories[categoryIndex].labels;
            if (from < 0 || from >= labels.Count || to < 0 || to >= labels.Count) return;
            var value = labels[from];
            labels.RemoveAt(from);
            labels.Insert(to, value);
        }

        public bool HasDuplicateLabelName(int categoryIndex, out string duplicateName)
        {
            duplicateName = null;
            if (categoryIndex < 0 || categoryIndex >= categories.Count) return false;
            var labels = categories[categoryIndex].labels;
            var seen = new HashSet<string>();
            foreach (var label in labels)
            {
                if (string.IsNullOrEmpty(label.name)) continue;
                if (!seen.Add(label.name))
                {
                    duplicateName = label.name;
                    return true;
                }
            }
            return false;
        }
    }
}