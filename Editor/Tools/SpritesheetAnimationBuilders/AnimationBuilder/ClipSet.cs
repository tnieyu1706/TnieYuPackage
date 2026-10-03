using System.Collections.Generic;

namespace TnieYuPackage.Tools.SAB.AnimationBuilder
{
    public class ClipSet
    {
        public readonly List<ClipDefinition> clips = new List<ClipDefinition>();
        public int selectedIndex = -1;
        private int clipNameCounter;

        public ClipDefinition SelectedClip =>
            selectedIndex >= 0 && selectedIndex < clips.Count ? clips[selectedIndex] : null;

        public int AddClip()
        {
            var clip = new ClipDefinition($"Clip_{clipNameCounter}");
            clipNameCounter++;
            clips.Add(clip);
            selectedIndex = clips.Count - 1;
            return selectedIndex;
        }

        public void RemoveClip(int index)
        {
            if (index < 0 || index >= clips.Count) return;
            clips.RemoveAt(index);
            if (clips.Count == 0)
            {
                selectedIndex = -1;
                return;
            }
            if (selectedIndex >= clips.Count)
                selectedIndex = clips.Count - 1;
        }

        public void RenameClip(int index, string newName)
        {
            if (index < 0 || index >= clips.Count) return;
            clips[index].name = newName;
        }

        public void ToggleFrame(int clipIndex, int spriteIndex)
        {
            if (clipIndex < 0 || clipIndex >= clips.Count) return;
            var clip = clips[clipIndex];
            if (clip.frameIndices.Contains(spriteIndex))
                clip.frameIndices.Remove(spriteIndex);
            else
                clip.frameIndices.Add(spriteIndex);
        }

        public bool ContainsFrame(int clipIndex, int spriteIndex)
        {
            if (clipIndex < 0 || clipIndex >= clips.Count) return false;
            return clips[clipIndex].frameIndices.Contains(spriteIndex);
        }

        public bool ContainsInAnyClip(int spriteIndex)
        {
            for (int i = 0; i < clips.Count; i++)
                if (clips[i].frameIndices.Contains(spriteIndex)) return true;
            return false;
        }

        public void MoveFrame(int clipIndex, int from, int to)
        {
            if (clipIndex < 0 || clipIndex >= clips.Count) return;
            var frames = clips[clipIndex].frameIndices;
            if (from < 0 || from >= frames.Count || to < 0 || to >= frames.Count) return;
            int value = frames[from];
            frames.RemoveAt(from);
            frames.Insert(to, value);
        }

        public int CountFramesUsedInOtherClips(int clipIndex, int spriteIndex)
        {
            int count = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                if (i == clipIndex) continue;
                if (clips[i].frameIndices.Contains(spriteIndex)) count++;
            }
            return count;
        }
    }
}