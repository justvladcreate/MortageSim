using System.Collections.Generic;
using DG.Tweening;
using MortgageSim.Core;
using MortgageSim.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace MortgageSim.World
{
    /// <summary>
    /// Монитор на cabinet_medium. На экране — живой терминал, в который
    /// пишутся действия игрока. Камера умеет «прилипать» к этому экрану.
    /// </summary>
    public sealed class MonitorTerminal : MonoBehaviour
    {
        public Transform ScreenTransform { get; private set; }
        public float ScreenWidth { get; private set; }
        public float ScreenHeight { get; private set; }

        private readonly List<string> _lines = new List<string>();
        private Text _terminalText;
        private Text _headerText;
        private Renderer _screenRenderer;
        private Material _screenMaterial;
        private Color _baseEmission;
        private Tween _hoverTween;
        private Tween _cursorTween;
        private CanvasGroup _cursorGroup;

        private const int MaxLines = 16;

        public static MonitorTerminal Create(Bounds cabinetBounds)
        {
            var root = new GameObject("Computer_Monitor");
            root.transform.position = new Vector3(cabinetBounds.center.x, cabinetBounds.max.y, cabinetBounds.center.z + 0.08f);
            var monitor = root.AddComponent<MonitorTerminal>();
            monitor.Build();
            return monitor;
        }

        private void Build()
        {
            // Подставка.
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Stand_Base", transform,
                new Vector3(0f, 0.018f, 0f), new Vector3(0.52f, 0.035f, 0.34f), RuntimeAssets.Metal, true);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Stand_Neck", transform,
                new Vector3(0f, 0.20f, -0.03f), new Vector3(0.07f, 0.37f, 0.07f), RuntimeAssets.Metal, true);

            // Корпус монитора.
            var body = RuntimeAssets.Primitive(PrimitiveType.Cube, "Screen_Body", transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(1.58f, 0.91f, 0.055f), RuntimeAssets.Metal, true);
            ScreenTransform = body.transform;

            // Экран.
            var screen = RuntimeAssets.Primitive(PrimitiveType.Cube, "Screen_Glass", transform,
                new Vector3(0f, 0.58f, 0.032f), new Vector3(1.50f, 0.83f, 0.012f), RuntimeAssets.Screen, false);
            _screenRenderer = screen.GetComponent<Renderer>();
            _screenMaterial = _screenRenderer.material;
            _baseEmission = _screenMaterial.HasProperty("_EmissionColor")
                ? _screenMaterial.GetColor("_EmissionColor")
                : new Color(0.03f, 0.22f, 0.08f);

            // Клавиатура.
            var keyboard = RuntimeAssets.Primitive(PrimitiveType.Cube, "Keyboard", transform,
                new Vector3(0f, -0.005f, 0.46f), new Vector3(1.06f, 0.03f, 0.34f), RuntimeAssets.Metal, true);
            keyboard.transform.localRotation = Quaternion.Euler(-7f, 0f, 0f);
            RuntimeAssets.Primitive(PrimitiveType.Cube, "Keyboard_Glow", transform,
                new Vector3(0f, 0.012f, 0.46f), new Vector3(1.02f, 0.006f, 0.30f),
                RuntimeAssets.Lit("KeyboardGlow", new Color(0.03f, 0.06f, 0.05f), 0.4f, 0f, new Color(0.1f, 0.65f, 0.25f)), false);

            BuildCanvas();
            AddInteractable();
            Boot();
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Screen_Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, 0.58f, 0.042f);
            canvasGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            canvasGo.transform.localScale = Vector3.one * 0.0012f;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 20;
            ScreenWidth = 1.536f;
            ScreenHeight = 0.864f;

            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1280f, 720f);

            var bg = CreateImage(canvasGo.transform, "Background", new Color(0.008f, 0.014f, 0.011f, 1f));
            Stretch(bg.rectTransform);

            var headerBg = CreateImage(canvasGo.transform, "HeaderBg", new Color(0.02f, 0.05f, 0.03f, 1f));
            headerBg.rectTransform.anchorMin = new Vector2(0f, 1f);
            headerBg.rectTransform.anchorMax = new Vector2(1f, 1f);
            headerBg.rectTransform.pivot = new Vector2(0.5f, 1f);
            headerBg.rectTransform.anchoredPosition = Vector2.zero;
            headerBg.rectTransform.sizeDelta = new Vector2(0f, 52f);

            var dot = CreateImage(canvasGo.transform, "Dot", new Color(0.45f, 1f, 0.48f, 1f));
            dot.rectTransform.anchorMin = new Vector2(0f, 1f);
            dot.rectTransform.anchorMax = new Vector2(0f, 1f);
            dot.rectTransform.pivot = new Vector2(0f, 1f);
            dot.rectTransform.anchoredPosition = new Vector2(20f, -19f);
            dot.rectTransform.sizeDelta = new Vector2(14f, 14f);

            _headerText = CreateText(canvasGo.transform, "Header", "root@mortgage:~/work", 20, new Color(0.45f, 0.94f, 0.48f));
            _headerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _headerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _headerText.rectTransform.pivot = new Vector2(0f, 1f);
            _headerText.rectTransform.anchoredPosition = new Vector2(48f, -13f);
            _headerText.rectTransform.sizeDelta = new Vector2(-72f, 32f);

            _terminalText = CreateText(canvasGo.transform, "Terminal", "", 24, new Color(0.55f, 0.95f, 0.62f));
            _terminalText.alignment = TextAnchor.UpperLeft;
            _terminalText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _terminalText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _terminalText.rectTransform.offsetMin = new Vector2(22f, 24f);
            _terminalText.rectTransform.offsetMax = new Vector2(-22f, -66f);

            var cursor = CreateImage(canvasGo.transform, "Cursor", new Color(0.55f, 0.95f, 0.62f, 0.9f));
            cursor.rectTransform.anchorMin = new Vector2(0f, 0f);
            cursor.rectTransform.anchorMax = new Vector2(0f, 0f);
            cursor.rectTransform.pivot = new Vector2(0f, 0f);
            cursor.rectTransform.anchoredPosition = new Vector2(24f, 14f);
            cursor.rectTransform.sizeDelta = new Vector2(16f, 24f);
            _cursorGroup = cursor.gameObject.AddComponent<CanvasGroup>();
            _cursorTween = DOTween.To(() => 1f, a => _cursorGroup.alpha = a, 0f, 0.55f)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = RuntimeAssets.TerminalFont;
            text.fontSize = size;
            text.color = color;
            text.text = value;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.05f;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void AddInteractable()
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.44f, 0.03f);
            box.size = new Vector3(1.7f, 1.1f, 0.35f);

            var computer = gameObject.AddComponent<ComputerInteractable>();
            computer.Monitor = this;
        }

        private void Boot()
        {
            AddLine("<color=#5E6E66>MORTGAGE_OS 1.0.12  //  kernel 6.12</color>");
            AddLine("<color=#5E6E66>CPU: 4x Python 3.11   RAM: 8 GB   DEBT: active</color>");
            AddLine("");
            AddLine("<color=#8CFF9A>root@mortgage</color>:<color=#7FB2FF>~/work</color>$ ./start_month.py");
            AddLine("<color=#C8FF5A>[OK]</color> рабочий стол загружен");
            AddLine("<color=#5E6E66>Нажмите на компьютер, чтобы открыть IDE.</color>");
            AddLine("");
            Refresh();
        }

        public void Log(string richLine)
        {
            AddLine(richLine);
            Refresh();
            Flash();
        }

        public void LogCommand(string command)
        {
            Log("<color=#8CFF9A>root@mortgage</color>:<color=#7FB2FF>~/work</color>$ " + command);
        }

        public void LogOk(string message) => Log("<color=#8CFF9A>[ OK ]</color> " + message);
        public void LogFail(string message) => Log("<color=#FF7A6A>[FAIL]</color> " + message);
        public void LogWarn(string message) => Log("<color=#FFC85A>[WARN]</color> " + message);
        public void LogInfo(string message) => Log("<color=#6E8FB8>[INFO]</color> " + message);

        private void AddLine(string line)
        {
            _lines.Add(line);
            while (_lines.Count > MaxLines) _lines.RemoveAt(0);
        }

        private void Refresh()
        {
            if (_terminalText != null) _terminalText.text = string.Join("\n", _lines);
        }

        public void Flash()
        {
            if (_screenMaterial == null) return;
            _screenMaterial.SetColor("_EmissionColor", _baseEmission + new Color(0.04f, 0.35f, 0.08f));
            DOTween.To(() => _screenMaterial.GetColor("_EmissionColor"),
                    c => _screenMaterial.SetColor("_EmissionColor", c),
                    _baseEmission, 0.45f)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        public void SetHover(bool hovered)
        {
            if (_screenMaterial == null) return;
            _hoverTween?.Kill();
            Color target = _baseEmission + (hovered ? new Color(0.05f, 0.45f, 0.10f) : Color.black);
            _hoverTween = DOTween.To(() => _screenMaterial.GetColor("_EmissionColor"),
                    c => _screenMaterial.SetColor("_EmissionColor", c),
                    target, 0.2f)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void OnDestroy()
        {
            _cursorTween?.Kill();
            _hoverTween?.Kill();
        }
    }
}
