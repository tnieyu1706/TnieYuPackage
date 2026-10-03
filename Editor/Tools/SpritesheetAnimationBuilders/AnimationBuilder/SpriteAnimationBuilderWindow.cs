using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using AnimatorController = UnityEditor.Animations.AnimatorController;

namespace TnieYuPackage.Tools.SAB.AnimationBuilder
{
    public class SpriteAnimationBuilderWindow : EditorWindow
    {
        private const string MenuPath = "Tools/TnieYu/Animation/Sprite Animation Builder";

        public enum ExportMode
        {
            CreateNewController,
            OverrideController
        }

        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private DefaultAsset outputFolder;
        [SerializeField] private string controllerName = "Character";
        [SerializeField] private int sampleRate = 10;
        [SerializeField] private bool loop = true;
        [SerializeField] private ExportMode exportMode = ExportMode.CreateNewController;
        [SerializeField] private AnimatorController baseController;

        private Sprite[] sprites;
        private readonly ClipSet clipSet = new ClipSet();

        private ReorderableList frameList;
        private int frameListClipIndex = -1;
        private Vector2 clipScroll;

        private GUIStyle indexStyle;
        private GUIStyle selectedRowStyle;
        private readonly Color selectedFill = new Color(0f, 0.8f, 1f, 0.35f);
        private readonly Color otherFill = new Color(1f, 0.9f, 0f, 0.18f);
        private readonly Color selectedBorder = new Color(0f, 0.9f, 1f, 1f);
        private readonly Color otherBorder = new Color(1f, 0.8f, 0f, 0.8f);
        private readonly Color defaultBorder = new Color(1f, 1f, 1f, 0.45f);

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<SpriteAnimationBuilderWindow>("Sprite Animation Builder");
            window.minSize = new Vector2(760, 520);
        }

        private void OnEnable()
        {
            indexStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            selectedRowStyle = new GUIStyle("SelectionRect");
        }

        private void OnGUI()
        {
            DrawSettingsBar();
            GUILayout.Space(6);

            if (sourceTexture == null)
            {
                DrawDropArea();
                return;
            }
            if (sprites == null || sprites.Length == 0)
            {
                EditorGUILayout.HelpBox("Texture chưa được slice (SpriteImportMode.Multiple).", MessageType.Warning);
                return;
            }

            GUILayout.BeginHorizontal();
            Rect gridRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.Width(position.width * 0.8f), GUILayout.ExpandHeight(true));
            DrawSpriteGrid(gridRect);
            GUILayout.BeginVertical();
            DrawClipPanel();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawSettingsBar()
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Spritesheet", sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
                LoadSprites();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output", outputFolder, typeof(DefaultAsset), false);
            controllerName = EditorGUILayout.TextField("Controller", controllerName);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.3f));
            sampleRate = EditorGUILayout.IntField("Sample Rate", Mathf.Max(1, sampleRate));
            loop = EditorGUILayout.Toggle("Loop", loop);
            exportMode = (ExportMode)EditorGUILayout.EnumPopup("Mode", exportMode);
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Export", GUILayout.Width(80), GUILayout.Height(36)))
                Export();
            EditorGUILayout.EndHorizontal();

            if (exportMode == ExportMode.OverrideController)
            {
                baseController = (AnimatorController)EditorGUILayout.ObjectField(
                    "Base Controller", baseController, typeof(AnimatorController), false);
            }
        }

        private void DrawDropArea()
        {
            EditorGUILayout.LabelField("Spritesheet", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            sourceTexture = (Texture2D)EditorGUILayout.ObjectField(sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
                LoadSprites();

            Rect dropRect = GUILayoutUtility.GetRect(0f, 60f, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "Kéo & thả spritesheet vào đây", EditorStyles.helpBox);
            HandleDragAndDrop(dropRect);
        }

        private void HandleDragAndDrop(Rect dropRect)
        {
            Event e = Event.current;
            if (!dropRect.Contains(e.mousePosition)) return;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is Texture2D tex)
                    {
                        sourceTexture = tex;
                        LoadSprites();
                        Repaint();
                        break;
                    }
                }
            }
            e.Use();
        }

        private void LoadSprites()
        {
            sprites = null;
            if (sourceTexture == null) return;

            string path = AssetDatabase.GetAssetPath(sourceTexture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
            {
                Debug.LogWarning($"{sourceTexture.name} chưa được slice (SpriteImportMode.Multiple).");
                return;
            }

            sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().ToArray();
            if (clipSet.clips.Count == 0)
                clipSet.AddClip();
        }

        private void DrawSpriteGrid(Rect gridRect)
        {
            GUI.Box(gridRect, GUIContent.none);

            Rect texRect = SpriteOverlayMath.FitTextureRect(sourceTexture.width, sourceTexture.height, gridRect);
            GUI.DrawTexture(texRect, sourceTexture);

            for (int i = 0; i < sprites.Length; i++)
            {
                Rect screenRect = SpriteOverlayMath.SpriteToScreenRect(
                    sprites[i].rect, sourceTexture.width, sourceTexture.height, texRect);
                if (screenRect.width < 2f || screenRect.height < 2f) continue;

                bool inSelected = clipSet.selectedIndex >= 0 && clipSet.SelectedClip.frameIndices.Contains(i);
                bool inOther = !inSelected && clipSet.ContainsInAnyClip(i);

                if (inSelected)
                    EditorGUI.DrawRect(screenRect, selectedFill);
                else if (inOther)
                    EditorGUI.DrawRect(screenRect, otherFill);

                Color border = inSelected ? selectedBorder : (inOther ? otherBorder : defaultBorder);
                DrawBorder(screenRect, border);
                DrawIndexLabel(screenRect, i);
            }

            HandleGridClick(gridRect, texRect);
        }

        private void HandleGridClick(Rect gridRect, Rect texRect)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (!gridRect.Contains(e.mousePosition)) return;

            Vector2 texPoint = SpriteOverlayMath.ScreenToTexturePoint(
                e.mousePosition, texRect, sourceTexture.width, sourceTexture.height);

            int hit = SpriteOverlayMath.HitTestRect(texPoint, sprites.Select(s => s.rect).ToArray());
            if (hit >= 0 && clipSet.selectedIndex >= 0)
            {
                clipSet.ToggleFrame(clipSet.selectedIndex, hit);
                e.Use();
                Repaint();
            }
        }

        private void DrawBorder(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private void DrawIndexLabel(Rect rect, int index)
        {
            if (rect.width < 14f || rect.height < 14f) return;
            indexStyle.fontSize = Mathf.Clamp((int)(rect.width * 0.4f), 8, 20);
            indexStyle.normal.textColor = Color.black;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), index.ToString(), indexStyle);
            indexStyle.normal.textColor = Color.white;
            GUI.Label(rect, index.ToString(), indexStyle);
        }

        private void DrawClipPanel()
        {
            GUILayout.Label("Clips", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.Width(28)))
                clipSet.AddClip();
            if (GUILayout.Button("-", GUILayout.Width(28)) && clipSet.selectedIndex >= 0)
                clipSet.RemoveClip(clipSet.selectedIndex);
            GUILayout.EndHorizontal();

            clipScroll = GUILayout.BeginScrollView(clipScroll);
            for (int i = 0; i < clipSet.clips.Count; i++)
                DrawClipEntry(i);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Frames", EditorStyles.boldLabel);
            DrawFrameList();
        }

        private void DrawClipEntry(int index)
        {
            var clip = clipSet.clips[index];
            bool selected = clipSet.selectedIndex == index;

            GUILayout.BeginHorizontal(selected ? selectedRowStyle : GUIStyle.none);
            if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(18)))
            {
                clipSet.selectedIndex = index;
                frameList = null;
            }
            string newName = EditorGUILayout.TextField(clip.name);
            if (newName != clip.name)
                clipSet.RenameClip(index, newName);
            GUILayout.EndHorizontal();

            DrawMinimap(clip);
        }

        private void DrawMinimap(ClipDefinition clip)
        {
            if (clip.frameIndices.Count == 0) return;

            Rect rect = GUILayoutUtility.GetRect(0f, 22f);
            float x = rect.x;
            const float size = 20f;
            foreach (int idx in clip.frameIndices)
            {
                if (idx < 0 || idx >= sprites.Length) continue;
                Sprite s = sprites[idx];
                Rect thumbRect = new Rect(x, rect.y, size, size);
                Rect texCoords = new Rect(
                    s.rect.x / sourceTexture.width,
                    s.rect.y / sourceTexture.height,
                    s.rect.width / sourceTexture.width,
                    s.rect.height / sourceTexture.height);
                GUI.DrawTextureWithTexCoords(thumbRect, sourceTexture, texCoords);
                x += size + 2f;
                if (x > rect.xMax - size) break;
            }
        }

        private void DrawFrameList()
        {
            var clip = clipSet.SelectedClip;
            if (clip == null)
            {
                GUILayout.Label("Chọn một clip để xem frames.");
                return;
            }

            if (frameList == null || frameListClipIndex != clipSet.selectedIndex)
            {
                BuildFrameList(clip);
                frameListClipIndex = clipSet.selectedIndex;
            }

            frameList.DoLayoutList();
        }

        private void BuildFrameList(ClipDefinition clip)
        {
            frameList = new ReorderableList(clip.frameIndices, typeof(int), true, false, false, true);
            frameList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Frames");
            frameList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                int spriteIndex = clip.frameIndices[index];
                Sprite s = spriteIndex >= 0 && spriteIndex < sprites.Length ? sprites[spriteIndex] : null;

                Rect thumbRect = new Rect(rect.x, rect.y, rect.height, rect.height);
                if (s != null)
                {
                    Rect texCoords = new Rect(
                        s.rect.x / sourceTexture.width,
                        s.rect.y / sourceTexture.height,
                        s.rect.width / sourceTexture.width,
                        s.rect.height / sourceTexture.height);
                    GUI.DrawTextureWithTexCoords(thumbRect, sourceTexture, texCoords);
                }

                string label = s != null ? $"{spriteIndex} — {s.name}" : $"#{spriteIndex}";
                EditorGUI.LabelField(new Rect(rect.x + rect.height + 4f, rect.y, rect.width - rect.height - 4f, rect.height), label);
            };
        }

        private void Export()
        {
            if (sourceTexture == null || sprites == null || sprites.Length == 0)
            {
                EditorUtility.DisplayDialog("Export", "Chưa load spritesheet hợp lệ.", "OK");
                return;
            }
            if (outputFolder == null)
            {
                EditorUtility.DisplayDialog("Export", "Chưa chọn Output folder.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                EditorUtility.DisplayDialog("Export", "Thiếu tên controller.", "OK");
                return;
            }
            if (exportMode == ExportMode.OverrideController && baseController == null)
            {
                EditorUtility.DisplayDialog("Export", "Chọn Base Controller để override.", "OK");
                return;
            }

            string folder = AssetDatabase.GetAssetPath(outputFolder);
            bool ok = exportMode == ExportMode.OverrideController
                ? AnimationClipExporter.ExportOverride(clipSet, sprites, folder, controllerName, sampleRate, loop, baseController)
                : AnimationClipExporter.Export(clipSet, sprites, folder, controllerName, sampleRate, loop);
            EditorUtility.DisplayDialog("Export", ok ? "Export thành công." : "Export thất bại. Xem Console.", "OK");
        }
    }
}