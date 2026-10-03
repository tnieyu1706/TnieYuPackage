using System.IO;
using UnityEditor;
using UnityEngine;

namespace TnieYuPackage.Tools
{
    public class SpriteResizeTool : EditorWindow
    {
        private Texture2D _reference;
        private Texture2D _selection;
        private Texture2D _selectionReadable;
        private Texture2D _referenceReadable;
        private Texture2D _selectionPreview;
        private Texture2D _refCutPreview;
        private Texture2D _selCutPreview;

        private Rect _refCutRect;
        private Rect _selCutRect;
        private bool _hasRefCut;
        private bool _hasSelCut;

        private string _draggingImage;
        private Vector2 _dragStartNorm;
        private Vector2 _dragCurrentNorm;

        private float _scalePercent = 100f;
        private int _filterIndex = 1; // 0 = Point, 1 = Bilinear
        private DefaultAsset _saveFolder;
        private string _fileName = "resized";

        private Vector2 _scroll;
        private Vector2 _overlayScroll;
        private float _refOpacity = 0.5f;
        private Color _refTint = Color.white;

        private static readonly string[] FilterNames = { "Point", "Bilinear" };
        private const float ImageAreaHeight = 280f;
        private const float CutPreviewSize = 120f;
        private const float OverlayViewportHeight = 300f;
        private const float OverlayPadding = 8f;

        [MenuItem("Tools/TnieYu/Texture/Sprite Resize")]
        public static void Open()
        {
            GetWindow<SpriteResizeTool>("Sprite Resize");
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            OnSelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            ReleaseTemp(ref _selectionReadable);
            ReleaseTemp(ref _referenceReadable);
            ReleaseTemp(ref _selectionPreview);
            ReleaseTemp(ref _refCutPreview);
            ReleaseTemp(ref _selCutPreview);
        }

        private void OnSelectionChanged()
        {
            object obj = Selection.activeObject;
            Texture2D tex = null;
            if (obj is Texture2D t)
                tex = t;
            else if (obj is Sprite s)
                tex = s.texture;

            if (tex == null)
                return;

            _selection = tex;
            _scalePercent = 100f;
            _fileName = tex.name + "_resized";
            _hasSelCut = false;
            RebuildAll();
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.BeginHorizontal();
            DrawLeftColumn();
            DrawRightColumn();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawLeftColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawImageBlock("Reference Image", "ref");
            GUILayout.Space(8);
            DrawImageBlock("Selection Image", "sel");
            EditorGUILayout.EndVertical();
        }

        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(300));
            DrawCutPreviews();
            GUILayout.Space(8);
            DrawActualSizeOverlay();
            GUILayout.Space(8);
            DrawToolsArea();
            EditorGUILayout.EndVertical();
        }

        private void DrawImageBlock(string title, string imageId)
        {
            GUILayout.Label(title, EditorStyles.boldLabel);

            if (imageId == "ref")
            {
                EditorGUI.BeginChangeCheck();
                _reference = (Texture2D)EditorGUILayout.ObjectField(
                    "Reference", _reference, typeof(Texture2D), false);
                if (EditorGUI.EndChangeCheck())
                {
                    _hasRefCut = false;
                    EnsureReadable(ref _referenceReadable, _reference);
                    RebuildCutPreview("ref");
                }
            }
            else
            {
                string info;
                if (_selection != null)
                {
                    info = _selection.name + " (" + _selection.width + "x" + _selection.height + ")";
                    if (_selectionPreview != null)
                        info += " -> " + _selectionPreview.width + "x" + _selectionPreview.height;
                }
                else
                {
                    info = "Select a Texture2D or Sprite in Project";
                }
                EditorGUILayout.LabelField(info, EditorStyles.miniLabel);
            }

            DrawImageWithCut(imageId);

            bool has = imageId == "ref" ? _hasRefCut : _hasSelCut;
            if (has && GUILayout.Button("Clear cut", GUILayout.Width(90)))
            {
                if (imageId == "ref")
                {
                    _hasRefCut = false;
                    ReleaseTemp(ref _refCutPreview);
                }
                else
                {
                    _hasSelCut = false;
                    ReleaseTemp(ref _selCutPreview);
                }
            }
        }

        private void DrawImageWithCut(string imageId)
        {
            Texture2D tex = imageId == "ref"
                ? _reference
                : (_selectionPreview != null ? _selectionPreview : _selection);

            Rect area = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(ImageAreaHeight));
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.15f));

            if (tex == null)
            {
                GUI.Label(area, "No image", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            Rect draw = GetFitRect(area, tex.width, tex.height);
            GUI.DrawTexture(draw, tex, ScaleMode.StretchToFill);

            HandleCutDrag(draw, imageId);

            bool has = imageId == "ref" ? _hasRefCut : _hasSelCut;
            Rect r = imageId == "ref" ? _refCutRect : _selCutRect;
            if (has)
                DrawNormRectOverlay(draw, r);
        }

        private static Rect GetFitRect(Rect area, int tw, int th)
        {
            float scale = Mathf.Min(area.width / tw, area.height / th);
            float w = tw * scale;
            float h = th * scale;
            return new Rect(
                area.x + (area.width - w) * 0.5f,
                area.y + (area.height - h) * 0.5f,
                w, h);
        }

        private void HandleCutDrag(Rect draw, string imageId)
        {
            int controlId = GUIUtility.GetControlID(
                ("SpriteResizeCut" + imageId).GetHashCode(), FocusType.Passive);
            Event e = Event.current;
            EventType type = e.GetTypeForControl(controlId);

            if (type == EventType.MouseDown && e.button == 0 && draw.Contains(e.mousePosition))
            {
                _draggingImage = imageId;
                _dragStartNorm = ToNorm(e.mousePosition, draw);
                _dragCurrentNorm = _dragStartNorm;
                GUIUtility.hotControl = controlId;
                e.Use();
            }
            else if (type == EventType.MouseDrag
                && _draggingImage == imageId
                && GUIUtility.hotControl == controlId)
            {
                _dragCurrentNorm = ToNorm(e.mousePosition, draw);
                SetCutRect(imageId, MakeNormRect(_dragStartNorm, _dragCurrentNorm));
                RebuildCutPreview(imageId);
                e.Use();
            }
            else if (type == EventType.MouseUp
                && _draggingImage == imageId
                && GUIUtility.hotControl == controlId)
            {
                Rect r = imageId == "ref" ? _refCutRect : _selCutRect;
                if (r.width < 0.01f || r.height < 0.01f)
                {
                    SetCutRect(imageId, new Rect(0, 0, 0, 0), false);
                    RebuildCutPreview(imageId);
                }
                _draggingImage = null;
                GUIUtility.hotControl = 0;
                e.Use();
            }
        }

        private static Vector2 ToNorm(Vector2 mouse, Rect draw)
        {
            float x = Mathf.Clamp01((mouse.x - draw.x) / draw.width);
            float y = Mathf.Clamp01((mouse.y - draw.y) / draw.height);
            return new Vector2(x, y);
        }

        private static Rect MakeNormRect(Vector2 a, Vector2 b)
        {
            float x = Mathf.Min(a.x, b.x);
            float y = Mathf.Min(a.y, b.y);
            return new Rect(x, y, Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        private void SetCutRect(string imageId, Rect r, bool has = true)
        {
            r.x = Mathf.Clamp01(r.x);
            r.y = Mathf.Clamp01(r.y);
            r.width = Mathf.Clamp(r.width, 0f, 1f - r.x);
            r.height = Mathf.Clamp(r.height, 0f, 1f - r.y);

            if (imageId == "ref")
            {
                _refCutRect = r;
                _hasRefCut = has;
            }
            else
            {
                _selCutRect = r;
                _hasSelCut = has;
            }
        }

        private static void DrawNormRectOverlay(Rect draw, Rect norm)
        {
            Rect r = new Rect(
                draw.x + norm.x * draw.width,
                draw.y + norm.y * draw.height,
                norm.width * draw.width,
                norm.height * draw.height);

            EditorGUI.DrawRect(r, new Color(1f, 0.8f, 0f, 0.15f));

            Color border = Color.yellow;
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), border);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), border);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), border);
            EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), border);
        }

        private void DrawCutPreviews()
        {
            GUILayout.Label("Cut Compare", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            DrawCutBox("Ref (cut)", _refCutPreview);
            DrawCutBox("Select (cut)", _selCutPreview);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCutBox(string title, Texture2D tex)
        {
            EditorGUILayout.BeginVertical();
            GUILayout.Label(title, EditorStyles.miniLabel);
            Rect r = GUILayoutUtility.GetRect(
                CutPreviewSize, CutPreviewSize,
                GUILayout.Width(CutPreviewSize), GUILayout.Height(CutPreviewSize));
            EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));

            string dims = "--";
            if (tex != null)
            {
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                dims = tex.width + " x " + tex.height;
            }
            else
            {
                GUI.Label(r, "--", EditorStyles.centeredGreyMiniLabel);
            }

            GUILayout.Label(dims, EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawActualSizeOverlay()
        {
            GUILayout.Label("Actual Size", EditorStyles.boldLabel);

            int fw = 0;
            int fh = 0;
            if (_refCutPreview != null)
            {
                fw = _refCutPreview.width;
                fh = _refCutPreview.height;
            }
            if (_selCutPreview != null)
            {
                fw = Mathf.Max(fw, _selCutPreview.width);
                fh = Mathf.Max(fh, _selCutPreview.height);
            }

            if (fw <= 0 || fh <= 0)
            {
                EditorGUILayout.HelpBox(
                    "Draw cut boxes on both images to compare at actual size.",
                    MessageType.Info);
                return;
            }

            _overlayScroll = EditorGUILayout.BeginScrollView(
                _overlayScroll, GUILayout.Height(OverlayViewportHeight));

            Rect frame = GUILayoutUtility.GetRect(
                fw + OverlayPadding * 2f,
                fh + OverlayPadding * 2f);
            DrawCheckerboard(frame);

            DrawCenteredWithAlpha(_selCutPreview, frame, Color.white);

            Color refTint = _refTint;
            refTint.a = _refOpacity;
            DrawCenteredWithAlpha(_refCutPreview, frame, refTint);

            EditorGUILayout.EndScrollView();

            float pct = Mathf.Round(_refOpacity * 100f);
            pct = EditorGUILayout.Slider("Ref Opacity %", pct, 0f, 100f);
            _refOpacity = Mathf.Clamp01(pct / 100f);

            _refTint = EditorGUILayout.ColorField(new GUIContent("Ref Tint"), _refTint, true, false, false);
        }

        private static void DrawCheckerboard(Rect area, float cell = 8f)
        {
            Color a = new Color(0.26f, 0.26f, 0.26f);
            Color b = new Color(0.18f, 0.18f, 0.18f);

            int cols = Mathf.CeilToInt(area.width / cell);
            int rows = Mathf.CeilToInt(area.height / cell);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Rect cr = new Rect(
                        area.x + c * cell,
                        area.y + r * cell,
                        Mathf.Min(cell, area.xMax - (area.x + c * cell)),
                        Mathf.Min(cell, area.yMax - (area.y + r * cell)));
                    EditorGUI.DrawRect(cr, (r + c) % 2 == 0 ? a : b);
                }
            }
        }

        private static void DrawCenteredWithAlpha(Texture2D tex, Rect frame, Color tint)
        {
            if (tex == null)
                return;

            Rect r = new Rect(
                frame.x + (frame.width - tex.width) * 0.5f,
                frame.y + (frame.height - tex.height) * 0.5f,
                tex.width,
                tex.height);

            Color prev = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            GUI.color = prev;
        }

        private void DrawToolsArea()
        {
            GUILayout.Label("Tools", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _scalePercent = EditorGUILayout.Slider("Scale %", _scalePercent, 1f, 400f);
            _scalePercent = EditorGUILayout.FloatField("Exact %", _scalePercent);
            if (EditorGUI.EndChangeCheck())
            {
                _scalePercent = Mathf.Clamp(_scalePercent, 1f, 400f);
                RebuildPreview();
            }

            EditorGUI.BeginChangeCheck();
            _filterIndex = EditorGUILayout.Popup("Filter Mode", _filterIndex, FilterNames);
            if (EditorGUI.EndChangeCheck())
                RebuildPreview();

            EditorGUILayout.BeginHorizontal();
            _saveFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                "Save Folder", _saveFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Folder Inside Assets", "Assets", "");
                if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                {
                    string relative = "Assets" + path.Substring(Application.dataPath.Length);
                    _saveFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(relative);
                }
            }
            EditorGUILayout.EndHorizontal();

            DrawFolderDrop();

            _fileName = EditorGUILayout.TextField("File Name", _fileName);

            GUILayout.Space(8);
            if (GUILayout.Button("Save", GUILayout.Height(32)))
                Save();
        }

        private void DrawFolderDrop()
        {
            Rect drop = GUILayoutUtility.GetRect(
                GUIContent.none, GUIStyle.none,
                GUILayout.ExpandWidth(true), GUILayout.Height(36));
            EditorGUI.DrawRect(drop, new Color(0.2f, 0.2f, 0.2f));
            GUI.Label(drop, "Drop folder here", EditorStyles.centeredGreyMiniLabel);

            Event e = Event.current;
            if (!drop.Contains(e.mousePosition))
                return;

            if (e.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                e.Use();
            }
            else if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object o in DragAndDrop.objectReferences)
                {
                    string p = AssetDatabase.GetAssetPath(o);
                    if (!string.IsNullOrEmpty(p) && AssetDatabase.IsValidFolder(p))
                    {
                        _saveFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(p);
                        break;
                    }
                }
                e.Use();
            }
        }

        private void Save()
        {
            if (_selection == null)
            {
                EditorUtility.DisplayDialog("Error", "No selection image.", "OK");
                return;
            }

            if (_saveFolder == null || string.IsNullOrEmpty(_fileName))
            {
                EditorUtility.DisplayDialog("Error", "Missing folder or filename.", "OK");
                return;
            }

            int w = Mathf.Max(1, Mathf.RoundToInt(_selection.width * _scalePercent / 100f));
            int h = Mathf.Max(1, Mathf.RoundToInt(_selection.height * _scalePercent / 100f));

            string folderPath = AssetDatabase.GetAssetPath(_saveFolder);
            string fullPath = Path.Combine(folderPath, _fileName + ".png");

            if (File.Exists(fullPath))
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Overwrite?",
                    "File already exists:\n" + fullPath + "\nOverwrite?",
                    "Overwrite", "Cancel");
                if (!overwrite)
                    return;
            }

            Texture2D readable = GetReadableCopy(_selection);
            if (readable == null)
            {
                EditorUtility.DisplayDialog("Error", "Cannot read source texture.", "OK");
                return;
            }

            Texture2D resized = ResizeTexture(readable, w, h, CurrentFilter());
            Object.DestroyImmediate(readable);

            if (resized == null)
            {
                EditorUtility.DisplayDialog("Error", "Resize failed.", "OK");
                return;
            }

            File.WriteAllBytes(fullPath, resized.EncodeToPNG());
            Object.DestroyImmediate(resized);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Success", "Saved to:\n" + fullPath, "OK");
        }

        private FilterMode CurrentFilter()
        {
            return _filterIndex == 0 ? FilterMode.Point : FilterMode.Bilinear;
        }

        private void RebuildAll()
        {
            EnsureReadable(ref _selectionReadable, _selection);
            RebuildPreview();
            RebuildCutPreview("ref");
        }

        private void EnsureReadable(ref Texture2D cache, Texture2D src)
        {
            ReleaseTemp(ref cache);
            cache = GetReadableCopy(src);
        }

        private void RebuildPreview()
        {
            ReleaseTemp(ref _selectionPreview);
            ReleaseTemp(ref _selCutPreview);

            if (_selection == null || _selectionReadable == null)
                return;

            int w = Mathf.Max(1, Mathf.RoundToInt(_selection.width * _scalePercent / 100f));
            int h = Mathf.Max(1, Mathf.RoundToInt(_selection.height * _scalePercent / 100f));

            _selectionPreview = ResizeTexture(_selectionReadable, w, h, CurrentFilter());
            RebuildCutPreview("sel");
        }

        private void RebuildCutPreview(string imageId)
        {
            if (imageId == "ref")
            {
                ReleaseTemp(ref _refCutPreview);
                if (_hasRefCut && _referenceReadable != null)
                    _refCutPreview = CropTexture(_referenceReadable, _refCutRect);
            }
            else
            {
                ReleaseTemp(ref _selCutPreview);
                if (_hasSelCut && _selectionPreview != null)
                    _selCutPreview = CropTexture(_selectionPreview, _selCutRect);
            }
        }

        private static Texture2D GetReadableCopy(Texture2D src)
        {
            if (src == null)
                return null;

            RenderTexture rt = RenderTexture.GetTemporary(
                src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static Texture2D CropTexture(Texture2D src, Rect norm)
        {
            if (src == null)
                return null;

            int x = Mathf.Clamp(Mathf.FloorToInt(norm.x * src.width), 0, src.width - 1);
            int top = Mathf.Clamp(Mathf.FloorToInt(norm.y * src.height), 0, src.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(norm.width * src.width), 1, src.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(norm.height * src.height), 1, src.height - top);
            int y = src.height - top - h;

            Texture2D crop = new Texture2D(w, h, TextureFormat.RGBA32, false);
            crop.SetPixels(src.GetPixels(x, y, w, h));
            crop.Apply();
            return crop;
        }

        private static Texture2D ResizeTexture(Texture2D src, int w, int h, FilterMode filter)
        {
            if (src == null || w < 1 || h < 1)
                return null;

            src.filterMode = filter;

            RenderTexture rt = RenderTexture.GetTemporary(
                w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.filterMode = filter;
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static void ReleaseTemp(ref Texture2D tex)
        {
            if (tex != null)
            {
                Object.DestroyImmediate(tex);
                tex = null;
            }
        }
    }
}
