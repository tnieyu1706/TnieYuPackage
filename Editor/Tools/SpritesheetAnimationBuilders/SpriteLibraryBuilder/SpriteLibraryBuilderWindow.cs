using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace WaveSurvival.Tools.SpriteLibraryBuilder
{
    public class SpriteLibraryBuilderWindow : EditorWindow
    {
        private const string MenuPath = "Tools/TnieYu/Animation/Sprite Library Builder";

        public enum ExportMode
        {
            CreateNew,
            Override
        }

        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private DefaultAsset outputFolder;
        [SerializeField] private string libraryName = "Character";
        [SerializeField] private ExportMode exportMode = ExportMode.CreateNew;
        [SerializeField] private SpriteLibraryAsset parentLibrary;

        private Sprite[] sprites;
        private readonly CategorySet categorySet = new CategorySet();

        private Vector2 categoryScroll;
        private Texture2D parentTexture;

        private ReorderableList labelList;
        private int labelListCategoryIndex = -1;

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
            var window = GetWindow<SpriteLibraryBuilderWindow>("Sprite Library Builder");
            window.minSize = new Vector2(900, 520);
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

            if (exportMode == ExportMode.Override && parentLibrary != null)
                LoadParentCategories();
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

            if (exportMode == ExportMode.Override)
            {
                GUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));
                GUILayout.Label("Parent", EditorStyles.boldLabel);
                Rect parentRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawParentSheet(parentRect);
                GUILayout.EndVertical();
            }

            float gridWidth = exportMode == ExportMode.Override ? position.width * 0.4f : position.width * 0.8f;
            Rect gridRect = GUILayoutUtility.GetRect(0f, 0f, GUILayout.Width(gridWidth), GUILayout.ExpandHeight(true));
            DrawSpriteGrid(gridRect);

            GUILayout.BeginVertical();
            DrawCategoryPanel();
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
            libraryName = EditorGUILayout.TextField("Library", libraryName);
            EditorGUI.BeginChangeCheck();
            exportMode = (ExportMode)EditorGUILayout.EnumPopup("Mode", exportMode);
            if (EditorGUI.EndChangeCheck() && exportMode == ExportMode.Override && parentLibrary != null)
                LoadParentCategories();
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("Export", GUILayout.Width(80), GUILayout.Height(36)))
                Export();
            EditorGUILayout.EndHorizontal();

            if (exportMode == ExportMode.Override)
            {
                EditorGUI.BeginChangeCheck();
                parentLibrary = (SpriteLibraryAsset)EditorGUILayout.ObjectField(
                    "Parent Library", parentLibrary, typeof(SpriteLibraryAsset), false);
                if (EditorGUI.EndChangeCheck())
                    LoadParentCategories();
            }
        }

        private void LoadParentCategories()
        {
            parentTexture = null;
            labelList = null;
            if (parentLibrary == null) return;

            categorySet.Clear();
            foreach (string categoryName in parentLibrary.GetCategoryNames())
                categorySet.AddCategoryNamed(categoryName);
            categorySet.selectedIndex = categorySet.categories.Count > 0 ? 0 : -1;
        }

        private void RefreshParentTexture()
        {
            parentTexture = null;
            if (parentLibrary == null) return;
            foreach (string categoryName in parentLibrary.GetCategoryNames())
            {
                foreach (string labelName in parentLibrary.GetCategoryLabelNames(categoryName))
                {
                    Sprite sprite = parentLibrary.GetSprite(categoryName, labelName);
                    if (sprite != null && sprite.texture != null)
                    {
                        parentTexture = sprite.texture;
                        return;
                    }
                }
            }
        }

        private void DrawParentSheet(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);

            if (parentLibrary == null)
            {
                EditorGUI.HelpBox(rect, "Chọn parent library để override.", MessageType.Info);
                return;
            }

            if (parentTexture == null)
            {
                RefreshParentTexture();
                if (parentTexture == null)
                {
                    EditorGUI.HelpBox(rect, "Parent library không có sprite.", MessageType.Warning);
                    return;
                }
            }

            Rect texRect = SpriteOverlayMath.FitTextureRect(parentTexture.width, parentTexture.height, rect);
            GUI.DrawTexture(texRect, parentTexture);

            var category = categorySet.SelectedCategory;
            if (category == null) return;
            string categoryName = category.name?.Trim();
            if (string.IsNullOrEmpty(categoryName)) return;

            var labelNames = parentLibrary.GetCategoryLabelNames(categoryName).ToArray();
            for (int i = 0; i < labelNames.Length; i++)
            {
                Sprite sprite = parentLibrary.GetSprite(categoryName, labelNames[i]);
                if (sprite == null || sprite.texture != parentTexture) continue;
                Rect screenRect = SpriteOverlayMath.SpriteToScreenRect(
                    sprite.rect, parentTexture.width, parentTexture.height, texRect);
                EditorGUI.DrawRect(screenRect, selectedFill);
                DrawBorder(screenRect, selectedBorder);
                DrawIndexLabel(screenRect, i);
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
            if (categorySet.categories.Count == 0)
                categorySet.AddCategory();
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

                bool inSelected = categorySet.selectedIndex >= 0 && categorySet.ContainsSpriteInCategory(categorySet.selectedIndex, i);
                bool inOther = !inSelected && categorySet.ContainsSpriteInAnyCategory(i);

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
            if (hit < 0 || categorySet.selectedIndex < 0) return;

            categorySet.ToggleSprite(categorySet.selectedIndex, hit, sprites[hit].name);
            labelList = null;
            e.Use();
            Repaint();
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

        private void DrawCategoryPanel()
        {
            GUILayout.Label("Categories", EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+", GUILayout.Width(28)))
                categorySet.AddCategory();
            if (GUILayout.Button("-", GUILayout.Width(28)) && categorySet.selectedIndex >= 0)
                categorySet.RemoveCategory(categorySet.selectedIndex);
            GUILayout.EndHorizontal();

            categoryScroll = GUILayout.BeginScrollView(categoryScroll, GUILayout.ExpandHeight(false));
            for (int i = 0; i < categorySet.categories.Count; i++)
                DrawCategoryEntry(i);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("Labels", EditorStyles.boldLabel);
            DrawLabelList();
        }

        private void DrawCategoryEntry(int index)
        {
            var category = categorySet.categories[index];
            bool selected = categorySet.selectedIndex == index;

            GUILayout.BeginHorizontal(selected ? selectedRowStyle : GUIStyle.none);
            if (GUILayout.Button(selected ? "●" : "○", GUILayout.Width(18)))
            {
                categorySet.selectedIndex = index;
                labelList = null;
            }
            string newName = EditorGUILayout.TextField(category.name);
            if (newName != category.name)
                categorySet.RenameCategory(index, newName);
            GUILayout.EndHorizontal();

            DrawMinimap(category);
        }

        private void DrawMinimap(CategoryDefinition category)
        {
            if (category.labels.Count == 0) return;

            Rect rect = GUILayoutUtility.GetRect(0f, 22f);
            float x = rect.x;
            const float size = 20f;
            foreach (var label in category.labels)
            {
                if (label.spriteIndex < 0 || label.spriteIndex >= sprites.Length) continue;
                Sprite s = sprites[label.spriteIndex];
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

        private void DrawLabelList()
        {
            var category = categorySet.SelectedCategory;
            if (category == null)
            {
                GUILayout.Label("Chọn một category để xem labels.");
                return;
            }

            if (labelList == null || labelListCategoryIndex != categorySet.selectedIndex)
            {
                BuildLabelList(category);
                labelListCategoryIndex = categorySet.selectedIndex;
            }

            labelList.DoLayoutList();
        }

        private void BuildLabelList(CategoryDefinition category)
        {
            labelList = new ReorderableList(category.labels, typeof(LabelDefinition), true, true, false, true);
            labelList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Labels");
            labelList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var label = category.labels[index];
                bool spriteValid = label.spriteIndex >= 0 && label.spriteIndex < sprites.Length;

                Rect thumbRect = new Rect(rect.x, rect.y, rect.height, rect.height);
                if (spriteValid)
                {
                    Sprite s = sprites[label.spriteIndex];
                    Rect texCoords = new Rect(
                        s.rect.x / sourceTexture.width,
                        s.rect.y / sourceTexture.height,
                        s.rect.width / sourceTexture.width,
                        s.rect.height / sourceTexture.height);
                    GUI.DrawTextureWithTexCoords(thumbRect, sourceTexture, texCoords);
                }

                Rect nameRect = new Rect(rect.x + rect.height + 4f, rect.y, rect.width - rect.height - 4f, rect.height);
                string newName = EditorGUI.TextField(nameRect, label.name);
                if (newName != label.name)
                    categorySet.RenameLabel(categorySet.selectedIndex, index, newName);
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
            if (string.IsNullOrWhiteSpace(libraryName))
            {
                EditorUtility.DisplayDialog("Export", "Thiếu tên library.", "OK");
                return;
            }
            if (exportMode == ExportMode.Override && parentLibrary == null)
            {
                EditorUtility.DisplayDialog("Export", "Chọn Parent Library để override.", "OK");
                return;
            }

            string folder = AssetDatabase.GetAssetPath(outputFolder);
            bool ok = exportMode == ExportMode.Override
                ? SpriteLibraryExporter.ExportOverride(categorySet, sprites, folder, libraryName, parentLibrary)
                : SpriteLibraryExporter.Export(categorySet, sprites, folder, libraryName);
            EditorUtility.DisplayDialog("Export", ok ? "Export thành công." : "Export thất bại. Xem Console.", "OK");
        }
    }
}