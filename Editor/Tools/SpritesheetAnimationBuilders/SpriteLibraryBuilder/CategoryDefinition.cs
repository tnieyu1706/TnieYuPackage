using System.Collections.Generic;

namespace WaveSurvival.Tools.SpriteLibraryBuilder
{
    public class CategoryDefinition
    {
        public string name;
        public readonly List<LabelDefinition> labels = new List<LabelDefinition>();

        public CategoryDefinition(string name)
        {
            this.name = name;
        }
    }
}