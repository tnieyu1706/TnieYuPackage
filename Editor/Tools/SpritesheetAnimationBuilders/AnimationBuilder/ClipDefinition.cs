using System.Collections.Generic;

namespace TnieYuPackage.Tools.SAB.AnimationBuilder
{
    public class ClipDefinition
    {
        public string name;
        public readonly List<int> frameIndices = new List<int>();

        public ClipDefinition(string name)
        {
            this.name = name;
        }
    }
}