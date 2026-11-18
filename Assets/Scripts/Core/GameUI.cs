using System;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using MortgageSim.Core;
using MortgageSim.Interaction;
using MortgageSim.Python;
using MortgageSim.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace MortgageSim
{
    /// <summary>
    /// Весь интерфейс на UI Toolkit: меню, HUD, диалог коллектора,
    /// подтверждение продажи, пауза, IDE, тосты и финальный экран.
    /// </summary>
    public sealed class GameUI : MonoBehaviour
    {
        private GameManager _game;
        private UIDocument _document;
        private VisualElement _root;

        private VisualElement _menu;
        private VisualElement _hud;
        private VisualElement _dialogueLayer;
        private VisualElement _dialoguePanel;
        private VisualElement _ide;
        private VisualElement _pause;
        private VisualElement _sellBackdrop;
        private VisualElement _confirmBackdrop;
        private VisualElement _end;

        private Label _prompt;
        private Label _crosshair;
        private Label _moneyValue;
        private Label _paymentValue;
        private Label _debtValue;
        private Label _itemsValue;
        private Label _monthValue;
        private Label _objectiveTitle;
        private Label _objectiveStatus;
        private readonly List<VisualElement> _monthSegments = new List<VisualElement>();

        private Label _dialogueText;
        private Label _dialogueAmount;
        private Label _dialogueHint;
        private VisualElement _dialogueButtons;

        private VisualElement _gloom;
        private Tween _gloomTween;
        private float _gloomValue;
        private VisualElement _toastLayer;

        private Label _sellTitle;
        private Label _sellText;
        private SellableItem _sellItem;

        private Label _confirmTitle;
        private Label _confirmText;
        private Action _confirmAction;

        private Label _ideKicker;
        private Label _ideTaskTitle;
        private VisualElement _ideTags;
        private Label _ideDescription;
        private VisualElement _ideExamples;
        private Label _ideHint;
        private Label _ideTestsStatus;
        private Label _ideTab;
        private TextField _codeEditor;
        private Label _codeGutter;
        private Label _ideOutput;
        private Label _ideStatusLabel;
        private Button _runButton;
        private Button _submitButton;
        private Button _giveUpButton;

        private VisualElement _monthCurtain;
        private Label _monthCurtainLabel;
        private Label _monthCurtainSub;

        private PythonTask _currentTask;
        private bool _lastRunPassed;
        private readonly Dictionary<int, string> _savedCode = new Dictionary<int, string>();

        private Label _endKicker;
        private Label _endTitle;
        private Label _endText;
        private VisualElement _endStats;

        private readonly List<VisualElement> _blockers = new List<VisualElement>();

        public bool IsMainMenuVisible => IsShown(_menu);
        public bool IsEndVisible => IsShown(_end);
        public bool IsPauseVisible => IsShown(_pause);
        public bool IsIDEOpen => IsShown(_ide);
        public bool IsDialogueVisible => IsShown(_dialogueLayer);
        public bool IsSellConfirmVisible => IsShown(_sellBackdrop);
        public bool IsConfirmVisible => IsShown(_confirmBackdrop);

        public void Build(GameManager game)
        {
            _game = game;

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "MortgagePanelSettings";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/UnityDefaultRuntimeTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.sortingOrder = 100;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = settings;

            _root = _document.rootVisualElement;
            if (_root == null)
            {
                Debug.LogError("GameUI: UIDocument не создал rootVisualElement.");
                return;
            }

            _root.AddToClassList("root");
            var styleSheet = Resources.Load<StyleSheet>("UI/GameStyles");
            if (styleSheet != null) _root.styleSheets.Add(styleSheet);

            BuildMainMenu();
            BuildGloom();
            BuildHud();
            BuildDialogue();
            BuildSellConfirm();
            BuildConfirm();
            BuildPause();
            BuildIde();
            BuildEnd();
            BuildMonthCurtain();

            Hide(_menu);
            Hide(_hud);
            Hide(_dialogueLayer);
            Hide(_ide);
            Hide(_pause);
            Hide(_sellBackdrop);
            Hide(_confirmBackdrop);
            Hide(_end);
            Hide(_crosshair);
            Hide(_monthCurtain);
        }

        // ------------------------------------------------------------ helpers

        private static VisualElement Make(string classes, VisualElement parent = null)
        {
            var element = new VisualElement();
            if (!string.IsNullOrEmpty(classes)) element.AddToClassList(classes);
            parent?.Add(element);
            return element;
        }

        private static Label MakeLabel(string classes, string text, VisualElement parent = null)
        {
            var label = new Label(text);
            if (!string.IsNullOrEmpty(classes)) label.AddToClassList(classes);
            parent?.Add(label);
            return label;
        }

        private static Button MakeButton(string text, string classes, VisualElement parent, Action action)
        {
            var button = new Button(() =>
            {
                GameManager.Instance?.Audio?.Click();
                action?.Invoke();
            })
            { text = text };
            button.AddToClassList("btn");
            if (!string.IsNullOrEmpty(classes))
            {
                foreach (var cls in classes.Split(' '))
                {
                    if (!string.IsNullOrEmpty(cls)) button.AddToClassList(cls);
                }
            }
            parent?.Add(button);
            return button;
        }

        private static void Show(VisualElement element)
        {
            if (element == null) return;
            element.RemoveFromClassList("hidden");
        }

        private static void Hide(VisualElement element)
        {
            if (element == null) return;
            DOTween.Kill(element);
            element.AddToClassList("hidden");
        }

        private static bool IsShown(VisualElement element)
        {
            return element != null && !element.ClassListContains("hidden");
        }

        private static void FadeIn(VisualElement element, float duration = 0.24f)
        {
            if (element == null) return;
            DOTween.Kill(element);
            element.style.opacity = 0f;
            DOTween.To(() => 0f, v => element.style.opacity = v, 1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(element);
        }

        private static void AddSpacer(VisualElement parent)
        {
            var spacer = Make("", parent);
            spacer.style.flexGrow = 1f;
        }

        private static void AddRule(VisualElement parent)
        {
            var rule = Make("", parent);
            rule.style.height = 1f;
            rule.style.backgroundColor = new Color(0.12f, 0.16f, 0.21f);
            rule.style.marginTop = 8f;
            rule.style.marginBottom = 8f;
        }

        private static void AddControlRow(VisualElement parent, string key, string description)
        {
            var row = Make("control-row", parent);
            MakeLabel("control-key", key, row);
            MakeLabel("control-desc", description, row);
        }

        // --------------------------------------------------------- main menu

        private void BuildMainMenu()
        {
            _menu = Make("screen");
            _menu.AddToClassList("main-menu");
            _root.Add(_menu);
            _blockers.Add(_menu);

            var left = Make("menu-left", _menu);
            MakeLabel("menu-kicker", "MORTGAGE.EXE  //  СИМУЛЯТОР ВЫЖИВАНИЯ", left);
            var title = MakeLabel("menu-title", "ПОСЛЕДНИЙ\nПЛАТЁЖ", left);
            title.style.whiteSpace = WhiteSpace.Normal;
            var subtitle = MakeLabel("menu-subtitle",
                "12 месяцев. 12 задач. 12 платежей.\n" +
                "Пишите код, получайте зарплату и спасайте квартиру от коллектора.", left);
            subtitle.style.whiteSpace = WhiteSpace.Normal;

            var buttons = Make("menu-buttons", left);
            MakeButton("Начать игру", "btn-primary menu-btn", buttons, () => _game.StartGame());
            var quit = MakeButton("Выход", "btn-ghost menu-btn", buttons, () => Application.Quit());
            quit.style.marginRight = 0f;

            MakeLabel("menu-footer",
                "WASD — движение   ·   мышь — обзор   ·   ЛКМ / E — взаимодействие   ·   Esc — пауза",
                _menu);

            var right = Make("menu-right", _menu);
            var card = Make("menu-card", right);
            MakeLabel("menu-card-title", "ВХОДЯЩИЕ // ДОЛГ", card);
            var terminal = MakeLabel("menu-terminal",
                "> mortgage --status\n" +
                "Квартира: в залоге\n" +
                "Срок: 12 месяцев\n" +
                "Просрочка: недопустима\n\n" +
                "> _", card);
            terminal.style.whiteSpace = WhiteSpace.Normal;

            AddMenuStat(card, "ОБЩИЙ ДОЛГ", GameBalance.Money(GameBalance.TotalDebt));
            AddMenuStat(card, "ПЛАТЁЖ В МЕСЯЦ", GameBalance.Money(GameBalance.Payments[0]) + " — " +
                                                  GameBalance.Money(GameBalance.Payments[GameBalance.Payments.Length - 1]));
            AddMenuStat(card, "ЗАРПЛАТА", GameBalance.Money(GameBalance.Salaries[0]) + " — " +
                                           GameBalance.Money(GameBalance.Salaries[GameBalance.Salaries.Length - 1]));
            AddMenuStat(card, "СТАРТОВЫЙ КАПИТАЛ", GameBalance.Money(GameBalance.StartMoney));
        }

        private static void AddMenuStat(VisualElement parent, string label, string value)
        {
            var row = Make("menu-stat-row", parent);
            MakeLabel("menu-stat-label", label, row);
            MakeLabel("menu-stat-value", value, row);
        }

        // ------------------------------------------------------------ gloom

        private void BuildGloom()
        {
            _gloom = Make("gloom-overlay");
            _gloom.pickingMode = PickingMode.Ignore;
            _gloom.style.opacity = 0f;
            _root.Add(_gloom);
        }

        public void SetGloom(int remaining, int initial)
        {
            if (_gloom == null) return;
            float sold = 1f - Mathf.Clamp01(initial <= 0 ? 0f : (float)remaining / initial);
            float target = Mathf.Lerp(0f, 0.32f, sold);
            _gloomTween?.Kill();
            _gloomTween = DOTween.To(() => _gloomValue, v =>
                {
                    _gloomValue = v;
                    _gloom.style.opacity = v;
                }, target, 1.4f)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);
        }

        public void ResetGloom()
        {
            if (_gloom == null) return;
            _gloomTween?.Kill();
            _gloomValue = 0f;
            _gloom.style.opacity = 0f;
        }

        // --------------------------------------------------------------- HUD

        private void BuildHud()
        {
            _hud = Make("hud");
            _root.Add(_hud);

            var stats = Make("hud-card", _hud);
            stats.style.position = Position.Absolute;
            stats.style.left = 24f;
            stats.style.top = 22f;

            var row = Make("hud-row", stats);
            _moneyValue = AddHudStat(row, "БАЛАНС");
            AddHudSeparator(row);
            _paymentValue = AddHudStat(row, "К ОПЛАТЕ");
            AddHudSeparator(row);
            _debtValue = AddHudStat(row, "ОСТАЛОСЬ");
            AddHudSeparator(row);
            _itemsValue = AddHudStat(row, "ВЕЩЕЙ");

            var center = Make("hud-center", _hud);
            center.style.position = Position.Absolute;
            center.style.left = 0f;
            center.style.right = 0f;
            center.style.top = 16f;

            var monthCard = Make("hud-card", center);
            monthCard.style.alignItems = Align.Center;
            var monthLabel = MakeLabel("hud-label", "СРОК ИПОТЕКИ", monthCard);
            monthLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _monthValue = MakeLabel("hud-value", "МЕСЯЦ 01 / 12", monthCard);

            var segments = Make("month-segments", monthCard);
            for (int i = 0; i < GameBalance.Months; i++)
            {
                var segment = Make("month-seg", segments);
                _monthSegments.Add(segment);
            }

            var objective = Make("hud-card objective-card", _hud);
            objective.style.position = Position.Absolute;
            objective.style.right = 24f;
            objective.style.top = 22f;
            MakeLabel("hud-label", "ЦЕЛЬ МЕСЯЦА", objective);
            _objectiveTitle = MakeLabel("objective-title", "Задача месяца", objective);
            _objectiveStatus = MakeLabel("objective-status", "Откройте компьютер", objective);

            var bottom = Make("hud-bottom", _hud);
            _prompt = MakeLabel("prompt", "", bottom);
            Hide(_prompt);

            _crosshair = MakeLabel("crosshair", "", _hud);
            _crosshair.pickingMode = PickingMode.Ignore;

            _toastLayer = Make("toast-layer", _hud);
        }

        private static Label AddHudStat(VisualElement parent, string caption)
        {
            var column = Make("", parent);
            column.style.marginRight = 18f;
            MakeLabel("hud-label", caption, column);
            var value = MakeLabel("hud-value", "—", column);
            return value;
        }

        private static void AddHudSeparator(VisualElement parent)
        {
            Make("hud-sep", parent);
        }

        public void ShowHUD()
        {
            Show(_hud);
        }

        public void HideHUD()
        {
            Hide(_hud);
        }

        public void ShowMainMenu()
        {
            Show(_menu);
            FadeIn(_menu, 0.3f);
            ClearToasts();
        }

        public void HideMainMenu()
        {
            Hide(_menu);
        }

        public void SetObjective(string title, string status)
        {
            if (_objectiveTitle != null) _objectiveTitle.text = title;
            if (_objectiveStatus != null) _objectiveStatus.text = status;
        }

        public void RefreshHUD()
        {
            if (_game == null) return;

            if (_moneyValue != null)
            {
                _moneyValue.text = GameBalance.Money(_game.Money);
                _moneyValue.EnableInClassList("hud-value-green", _game.Money >= _game.CurrentPayment);
            }
            if (_paymentValue != null)
            {
                _paymentValue.text = GameBalance.Money(_game.CurrentPayment);
                _paymentValue.EnableInClassList("hud-value-red", _game.Money < _game.CurrentPayment);
            }
            if (_debtValue != null) _debtValue.text = GameBalance.Money(_game.RemainingDebt);
            if (_itemsValue != null) _itemsValue.text = _game.Items.Count.ToString();
            if (_monthValue != null)
            {
                _monthValue.text = "МЕСЯЦ " + _game.Month.ToString("00") + " / " + GameBalance.Months;
            }

            bool won = _game.Phase == GamePhase.Won;
            for (int i = 0; i < _monthSegments.Count; i++)
            {
                bool done = won || i < _game.Month - 1;
                bool current = !won && i == _game.Month - 1;
                _monthSegments[i].EnableInClassList("month-seg-done", done);
                _monthSegments[i].EnableInClassList("month-seg-current", current);
            }
        }

        public void ShowPrompt(string text)
        {
            if (_prompt == null) return;
            _prompt.text = text;
            Show(_prompt);
        }

        public void HidePrompt()
        {
            Hide(_prompt);
        }

        public void SetCrosshairActive(bool active)
        {
            if (_crosshair == null) return;
            _crosshair.EnableInClassList("crosshair-active", active);
            if (active) Show(_crosshair);
            else Hide(_crosshair);
        }

        public void ShowToast(string message, bool error = false)
        {
            if (_toastLayer == null) return;

            while (_toastLayer.childCount > 4)
            {
                _toastLayer[0].RemoveFromHierarchy();
            }

            var toast = MakeLabel(error ? "toast toast-error" : "toast", message, _toastLayer);
            toast.style.opacity = 0f;
            DOTween.Sequence()
                .Append(DOTween.To(() => 0f, v => toast.style.opacity = v, 1f, 0.18f).SetEase(Ease.OutQuad))
                .AppendInterval(3.4f)
                .Append(DOTween.To(() => 1f, v => toast.style.opacity = v, 0f, 0.3f).SetEase(Ease.InQuad))
                .AppendCallback(() =>
                {
                    if (toast.parent != null) toast.RemoveFromHierarchy();
                })
                .SetUpdate(true)
                .SetTarget(toast);
        }

        private void ClearToasts()
        {
            if (_toastLayer != null) _toastLayer.Clear();
        }

        // ---------------------------------------------------------- dialogue

        private void BuildDialogue()
        {
            _dialogueLayer = Make("dialogue-layer");
            _root.Add(_dialogueLayer);

            _dialoguePanel = Make("dialogue", _dialogueLayer);
            _blockers.Add(_dialogueLayer);

            var avatar = Make("avatar", _dialoguePanel);
            MakeLabel("avatar-text", "К", avatar);

            var main = Make("dialogue-main", _dialoguePanel);
            MakeLabel("dialogue-name", "КОЛЛЕКТОР // КВАРТИРНЫЙ ВОПРОС", main);
            _dialogueText = MakeLabel("dialogue-text", "", main);
            _dialogueAmount = MakeLabel("dialogue-amount", "", main);
            _dialogueButtons = Make("dialogue-buttons", main);
            _dialogueHint = MakeLabel("dialogue-hint", "", main);

            Hide(_dialogueLayer);
        }

        public void ShowCollectorDialogue(bool firstLine)
        {
            if (_game == null) return;

            bool canPay = _game.Money >= _game.CurrentPayment;
            _dialogueText.text = BuildCollectorLine(firstLine, canPay);
            _dialogueAmount.text = "К оплате: " + GameBalance.Money(_game.CurrentPayment) +
                                   "   ·   у вас: " + GameBalance.Money(_game.Money);

            _dialogueButtons.Clear();

            var pay = MakeButton("Оплатить " + GameBalance.Money(_game.CurrentPayment),
                "btn-primary btn-small", _dialogueButtons, () => _game.PayCollector());
            pay.style.marginRight = 8f;

            var sell = MakeButton("Продать вещи", "btn-ghost btn-small", _dialogueButtons, () =>
            {
                _game.CloseCollectorDialogue();
                ShowToast("Наведите прицел на мебель и нажмите ЛКМ, чтобы продать её.");
            });
            sell.style.marginRight = 8f;

            MakeButton("Позже", "btn-ghost btn-small", _dialogueButtons,
                () => _game.CloseCollectorDialogue());

            if (!canPay && _game.Items.Count == 0)
            {
                var bankrupt = MakeButton("Признать поражение", "btn-danger btn-small", _dialogueButtons, () =>
                {
                    ShowConfirm("Объявить дефолт?",
                        "Денег нет, продавать больше нечего. Ипотека будет отобрана.",
                        () => _game.DeclareBankruptcy());
                });
                bankrupt.style.marginLeft = 8f;
            }

            _dialogueHint.text = canPay
                ? "Можно ходить по квартире. Коллектор никуда не торопится."
                : "Не хватает " + GameBalance.Money(_game.CurrentPayment - _game.Money) +
                  ". Продайте мебель или поговорите с коллектором позже.";

            Show(_dialogueLayer);
            _dialogueLayer.BringToFront();
            FadeIn(_dialoguePanel, 0.2f);
        }

        private string BuildCollectorLine(bool firstLine, bool canPay)
        {
            string[] greetings =
            {
                "Здравствуйте. Я по поводу ипотеки.",
                "Снова я. Дом пока не ваш.",
                "Проверка платежа. Открывайте.",
                "Коллектор на пороге. Дела идут неважно.",
                "Я смотрю, вы тут неплохо устроились. Пока что.",
                "Счётчик месяцев тикает.",
                "Квартира пустеет. Свет тускнеет. Мне это нравится.",
                "Ваш код работает? А долг — нет.",
                "Ещё немного, и я заберу даже лампу.",
                "Кто-то платит вовремя. Вы — пока нет.",
                "Последние месяцы. Не подведите себя.",
                "Финал. Сегодня решается, чей это дом."
            };

            string greeting = greetings[Mathf.Clamp(_game.Month - 1, 0, greetings.Length - 1)];

            if (firstLine)
            {
                return greeting + "\n\nПришло время платить.";
            }
            if (!canPay)
            {
                return "Пришло время платить.\n\nНа счету " + GameBalance.Money(_game.Money) +
                       ", а нужно " + GameBalance.Money(_game.CurrentPayment) +
                       ". Продайте что-нибудь из мебели — я подожду.";
            }
            if (_game.Items.Count <= 4)
            {
                return "Пришло время платить.\n\nКвартира почти пустая. Ещё пара платежей — и здесь будет темно.";
            }
            if (_game.Month >= GameBalance.Months - 1)
            {
                return "Пришло время платить.\n\nОсталось совсем немного. Неужели вы дотянете до конца?";
            }
            return "Пришло время платить.\n\n" + greeting;
        }

        public void HideDialogue()
        {
            Hide(_dialogueLayer);
        }

        // ------------------------------------------------------ sell confirm

        private void BuildSellConfirm()
        {
            _sellBackdrop = Make("screen");
            _sellBackdrop.AddToClassList("modal-backdrop");
            _root.Add(_sellBackdrop);
            _blockers.Add(_sellBackdrop);

            var panel = Make("modal", _sellBackdrop);
            MakeLabel("modal-kicker", "ПРОДАЖА ИМУЩЕСТВА", panel);
            _sellTitle = MakeLabel("modal-title", "", panel);
            _sellText = MakeLabel("modal-text", "", panel);

            var buttons = Make("modal-buttons", panel);
            var cancel = MakeButton("Отмена", "btn-ghost", buttons, HideSellConfirm);
            cancel.style.marginRight = 10f;
            MakeButton("Продать", "btn-primary", buttons, ConfirmSell);

            Hide(_sellBackdrop);
        }

        public void ShowSellConfirm(SellableItem item)
        {
            if (item == null) return;
            _sellItem = item;
            _sellTitle.text = item.DisplayName;
            _sellText.text = "Вы получите " + GameBalance.Money(item.Price) +
                             ".\nРазмер: " + item.SizeLabel() +
                             ". Предмет навсегда исчезнет из квартиры.";
            Show(_sellBackdrop);
            _sellBackdrop.BringToFront();
            if (_game != null) _game.OnModalOpened();
            FadeIn(_sellBackdrop, 0.16f);
        }

        public void HideSellConfirm()
        {
            Hide(_sellBackdrop);
            _sellItem = null;
            if (_game != null) _game.OnModalClosed();
        }

        private void ConfirmSell()
        {
            var item = _sellItem;
            HideSellConfirm();
            if (item != null) item.Sell();
        }

        // ---------------------------------------------------------- confirm

        private void BuildConfirm()
        {
            _confirmBackdrop = Make("screen");
            _confirmBackdrop.AddToClassList("modal-backdrop");
            _root.Add(_confirmBackdrop);
            _blockers.Add(_confirmBackdrop);

            var panel = Make("modal", _confirmBackdrop);
            MakeLabel("modal-kicker", "ПОДТВЕРЖДЕНИЕ", panel);
            _confirmTitle = MakeLabel("modal-title", "", panel);
            _confirmText = MakeLabel("modal-text", "", panel);

            var buttons = Make("modal-buttons", panel);
            var cancel = MakeButton("Отмена", "btn-ghost", buttons, HideConfirm);
            cancel.style.marginRight = 10f;
            MakeButton("Подтвердить", "btn-danger", buttons, () =>
            {
                var action = _confirmAction;
                HideConfirm();
                action?.Invoke();
            });

            Hide(_confirmBackdrop);
        }

        public void ShowConfirm(string title, string text, Action action)
        {
            _confirmTitle.text = title;
            _confirmText.text = text;
            _confirmAction = action;
            Show(_confirmBackdrop);
            _confirmBackdrop.BringToFront();
            if (_game != null) _game.OnModalOpened();
            FadeIn(_confirmBackdrop, 0.16f);
        }

        public void HideConfirm()
        {
            Hide(_confirmBackdrop);
            _confirmAction = null;
            if (_game != null) _game.OnModalClosed();
        }

        // ------------------------------------------------------------ pause

        private void BuildPause()
        {
            _pause = Make("screen");
            _pause.AddToClassList("pause-screen");
            _root.Add(_pause);
            _blockers.Add(_pause);

            var panel = Make("pause-panel", _pause);
            MakeLabel("pause-title", "ПАУЗА", panel);
            MakeLabel("pause-subtitle", "ИПОТЕКА НЕ ЖДЁТ, НО МОЖНО ПЕРЕДОХНУТЬ", panel);

            var grid = Make("controls-grid", panel);
            AddControlRow(grid, "WASD", "Движение по квартире");
            AddControlRow(grid, "МЫШЬ", "Обзор");
            AddControlRow(grid, "ЛКМ / E", "Взаимодействие и продажа");
            AddControlRow(grid, "ESC", "Пауза, закрыть IDE или диалог");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AddControlRow(grid, "CTRL+M", "Пропустить месяц (dev)");
#endif

            var buttons = Make("", panel);
            var resume = MakeButton("Продолжить", "btn-primary", buttons, () => _game.Resume());
            resume.style.width = Length.Percent(100f);
            resume.style.marginBottom = 8f;

            var menu = MakeButton("В главное меню", "btn-ghost", buttons, () => _game.ReturnToMainMenu());
            menu.style.width = Length.Percent(100f);
            menu.style.marginBottom = 8f;

            var surrender = MakeButton("Сдаться и объявить дефолт", "btn-danger", buttons, () =>
            {
                ShowConfirm("Объявить дефолт?",
                    "Игра закончится поражением. Квартира достанется банку.",
                    () => _game.DeclareBankruptcy());
            });
            surrender.style.width = Length.Percent(100f);
            surrender.style.marginBottom = 8f;

            var quit = MakeButton("Выйти из игры", "btn-ghost", buttons, () => Application.Quit());
            quit.style.width = Length.Percent(100f);

            Hide(_pause);
        }

        public void ShowPause()
        {
            Show(_pause);
            _pause.BringToFront();
            FadeIn(_pause, 0.16f);
        }

        public void HidePause()
        {
            Hide(_pause);
        }

        // --------------------------------------------------------------- IDE

        private void BuildIde()
        {
            _ide = Make("screen");
            _ide.AddToClassList("ide-screen");
            _root.Add(_ide);
            _blockers.Add(_ide);

            var topbar = Make("ide-topbar", _ide);
            var dots = Make("ide-dots", topbar);
            Make("ide-dot ide-dot-red", dots);
            Make("ide-dot ide-dot-yellow", dots);
            Make("ide-dot ide-dot-green", dots);
            MakeLabel("ide-logo", "MORTGAGE IDE", topbar);
            _ideTab = MakeLabel("ide-tab", "task_01.py", topbar);
            Make("ide-spacer", topbar);
            MakeButton("Закрыть [Esc]", "btn-ghost btn-small", topbar, () => _game.CloseComputer());

            var body = Make("ide-body", _ide);

            var sidebar = Make("ide-sidebar", body);
            var sidebarScroll = new ScrollView(ScrollViewMode.Vertical);
            sidebarScroll.AddToClassList("ide-sidebar-scroll");
            sidebarScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sidebarScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            sidebar.Add(sidebarScroll);

            var side = sidebarScroll.contentContainer;
            _ideKicker = MakeLabel("ide-kicker", "ЗАДАЧА МЕСЯЦА", side);
            _ideTaskTitle = MakeLabel("ide-task-title", "", side);
            _ideTags = Make("tag-row", side);
            _ideDescription = MakeLabel("ide-description", "", side);

            _ideExamples = Make("ide-examples", side);

            _ideHint = MakeLabel("ide-hint", "", side);

            var testsBox = Make("ide-tests-box", side);
            MakeLabel("ide-tests-title", "СКРЫТЫЕ ТЕСТЫ", testsBox);
            _ideTestsStatus = MakeLabel("ide-tests-status", "Ещё не запускались.", testsBox);

            var editor = Make("ide-editor", body);
            var toolbar = Make("ide-toolbar", editor);
            var info = MakeLabel("toolbar-info", "solution.py  ·  Python 3.11", toolbar);
            info.style.marginRight = 12f;
            AddSpacer(toolbar);
            _runButton = MakeButton("Запустить тесты", "btn-primary btn-small", toolbar, RunTests);
            _runButton.style.marginRight = 8f;
            _submitButton = MakeButton("Сдать работу", "btn-primary btn-small", toolbar, SubmitWork);
            _submitButton.style.marginRight = 8f;
            _giveUpButton = MakeButton("Сдаться", "btn-danger btn-small", toolbar, ConfirmGiveUp);

            var editorScroll = new ScrollView(ScrollViewMode.Vertical);
            editorScroll.AddToClassList("editor-scroll");
            editorScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            editorScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            editor.Add(editorScroll);

            var editorRow = Make("editor-row", editorScroll.contentContainer);
            _codeGutter = MakeLabel("code-gutter", "1", editorRow);
            _codeGutter.style.unityFont = RuntimeAssets.TerminalFont;
            _codeGutter.pickingMode = PickingMode.Ignore;

            _codeEditor = new TextField { multiline = true };
            _codeEditor.AddToClassList("code-editor");
            _codeEditor.style.unityFont = RuntimeAssets.TerminalFont;
            _codeEditor.style.flexGrow = 1f;
            _codeEditor.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _codeEditor.RegisterCallback<KeyDownEvent>(OnEditorKeyDown, TrickleDown.TrickleDown);
            _codeEditor.RegisterValueChangedCallback(evt => UpdateCodeGutter(evt.newValue));
            editorRow.Add(_codeEditor);

            UpdateCodeGutter(string.Empty);

            var output = Make("ide-output", editor);
            MakeLabel("ide-output-title", "ВЫВОД", output);
            var outputScroll = new ScrollView(ScrollViewMode.Vertical);
            outputScroll.AddToClassList("ide-output-scroll");
            outputScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            outputScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            output.Add(outputScroll);
            _ideOutput = MakeLabel("ide-output-text", "> Готово к запуску.", outputScroll.contentContainer);
            _ideOutput.style.unityFont = RuntimeAssets.TerminalFont;

            var status = Make("ide-status", _ide);
            _ideStatusLabel = MakeLabel("status-text", "Python 3.11 · встроенный интерпретатор", status);

            Hide(_ide);
        }

        private void OnEditorKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Tab) return;

            evt.StopImmediatePropagation();

            string value = _codeEditor.value ?? string.Empty;
            var selection = _codeEditor.textSelection;
            int start = Mathf.Clamp(Mathf.Min(selection.cursorIndex, selection.selectIndex), 0, value.Length);
            int end = Mathf.Clamp(Mathf.Max(selection.cursorIndex, selection.selectIndex), 0, value.Length);

            if (evt.shiftKey)
            {
                int lineStart = start > 0 ? value.LastIndexOf('\n', start - 1) + 1 : 0;
                int remove = 0;
                while (remove < 4 && lineStart + remove < value.Length && value[lineStart + remove] == ' ')
                {
                    remove++;
                }
                if (remove == 0) return;

                value = value.Remove(lineStart, remove);
                start = Mathf.Max(lineStart, start - remove);
                end = Mathf.Max(lineStart, end - remove);
            }
            else
            {
                value = value.Insert(start, "    ");
                start += 4;
                end = start;
            }

            _codeEditor.value = value;
            _codeEditor.textSelection.cursorIndex = start;
            _codeEditor.textSelection.selectIndex = end;
            _codeEditor.Focus();
        }

        private void UpdateCodeGutter(string code)
        {
            if (_codeGutter == null) return;

            code ??= string.Empty;
            int lines = 1;
            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] == '\n') lines++;
            }

            var builder = new StringBuilder(lines * 3);
            for (int i = 1; i <= lines; i++)
            {
                if (i > 1) builder.Append('\n');
                builder.Append(i);
            }
            _codeGutter.text = builder.ToString();

            float height = Mathf.Max(330f, lines * 21f + 28f);
            _codeEditor.style.height = height;
            _codeGutter.style.height = height;
        }

        private static string NormalizeTabs(string code)
        {
            return string.IsNullOrEmpty(code) ? string.Empty : code.Replace("\t", "    ");
        }

        public void OpenIDE(PythonTask task)
        {
            _currentTask = task;
            if (task == null) return;

            _ideTab.text = task.Id + ".py";
            _ideKicker.text = "МЕСЯЦ " + task.Month.ToString("00") + " // " + task.Difficulty.ToUpperInvariant();
            _ideTaskTitle.text = task.Title;
            _ideDescription.text = task.Description;
            _ideHint.text = "ПОДСКАЗКА: " + task.Hint;

            _ideTags.Clear();
            _ideTags.Add(MakeTag("МЕСЯЦ " + task.Month));
            _ideTags.Add(MakeTag(task.Difficulty.ToUpperInvariant()));
            _ideTags.Add(MakeTag("PYTHON 3.11"));

            _ideExamples.Clear();
            MakeLabel("ide-tests-title", "ПРИМЕРЫ", _ideExamples);
            if (task.Examples != null && task.Examples.Length > 0)
            {
                foreach (var example in task.Examples)
                {
                    MakeLabel("ide-example", example, _ideExamples);
                }
            }
            else
            {
                MakeLabel("ide-example", "Примеры не указаны.", _ideExamples);
            }

            if (_savedCode.TryGetValue(task.Month, out string saved) && !string.IsNullOrEmpty(saved))
                _codeEditor.value = NormalizeTabs(saved);
            else
                _codeEditor.value = NormalizeTabs(task.StarterCode);
            UpdateCodeGutter(_codeEditor.value);

            _lastRunPassed = false;
            _submitButton.SetEnabled(false);
            _runButton.SetEnabled(true);
            _ideTestsStatus.text = _game != null && _game.TaskSolvedThisMonth
                ? "Задача уже сдана. Ждите коллектора."
                : "Ещё не запускались.";
            _ideTestsStatus.EnableInClassList("status-ok", false);
            _ideTestsStatus.EnableInClassList("status-fail", false);
            _ideOutput.text = "> solution.py загружен. Запустите тесты.";
            _ideStatusLabel.text = "Python 3.11 · встроенный интерпретатор";
            _ideStatusLabel.EnableInClassList("status-ok", false);
            _ideStatusLabel.EnableInClassList("status-fail", false);

            Show(_ide);
            FadeIn(_ide, 0.2f);
        }

        private static Label MakeTag(string text)
        {
            var tag = new Label(text);
            tag.AddToClassList("tag");
            return tag;
        }

        public void CloseIDE()
        {
            if (_currentTask != null && _codeEditor != null)
            {
                _savedCode[_currentTask.Month] = _codeEditor.value;
            }
            Hide(_ide);
        }

        public void HideIDE()
        {
            Hide(_ide);
        }

        private void RunTests()
        {
            if (_currentTask == null || _game == null) return;

            var result = PythonTaskRunner.Run(_currentTask, _codeEditor.value);
            _ideOutput.text = BuildRunOutput(result);
            _lastRunPassed = result.Success;
            _submitButton.SetEnabled(result.Success);
            if (result.Success) _game.Audio?.TestsPassed();
            else _game.Audio?.TestsFailed();

            if (result.Success)
            {
                _ideTestsStatus.text = "Все тесты пройдены (" + result.PassedCount + "/" + result.TotalCount + ").";
                _ideTestsStatus.EnableInClassList("status-ok", true);
                _ideTestsStatus.EnableInClassList("status-fail", false);
                _ideStatusLabel.text = "Тесты пройдены. Можно сдавать работу.";
                _ideStatusLabel.EnableInClassList("status-ok", true);
                _ideStatusLabel.EnableInClassList("status-fail", false);
            }
            else
            {
                _ideTestsStatus.text = result.Error != null
                    ? "Ошибка: " + result.Error
                    : "Пройдено " + result.PassedCount + " из " + result.TotalCount + " тестов.";
                _ideTestsStatus.EnableInClassList("status-ok", false);
                _ideTestsStatus.EnableInClassList("status-fail", true);
                _ideStatusLabel.text = "Есть ошибки. Исправьте код и запустите снова.";
                _ideStatusLabel.EnableInClassList("status-ok", false);
                _ideStatusLabel.EnableInClassList("status-fail", true);
            }

            if (_game.Terminal != null)
            {
                _game.Terminal.LogCommand("pytest " + _currentTask.Id + ".py");
                if (result.Success) _game.Terminal.LogOk("Все тесты пройдены.");
                else _game.Terminal.LogFail("Тесты: " + result.PassedCount + "/" + result.TotalCount);
            }
        }

        private static string BuildRunOutput(PythonRunResult result)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(result.Error))
            {
                builder.AppendLine(result.Error);
                builder.AppendLine();
            }
            if (!string.IsNullOrEmpty(result.ConsoleOutput))
            {
                builder.AppendLine("stdout:");
                builder.AppendLine(result.ConsoleOutput.TrimEnd());
                builder.AppendLine();
            }

            foreach (var outcome in result.Outcomes)
            {
                builder.AppendLine(outcome.ToConsoleLine());
            }

            if (result.Outcomes.Count == 0 && string.IsNullOrEmpty(result.Error))
            {
                builder.AppendLine("Тестов нет.");
            }
            return builder.ToString().TrimEnd();
        }

        private void SubmitWork()
        {
            if (_currentTask == null || _game == null) return;

            if (!_lastRunPassed)
            {
                _game.Audio?.Deny();
                ShowToast("Сначала запустите тесты и убедитесь, что всё проходит.", true);
                return;
            }

            var result = _game.SubmitTaskSolution(_codeEditor.value);
            if (result.Success)
            {
                _game.CloseComputer();
            }
            else
            {
                _lastRunPassed = false;
                _submitButton.SetEnabled(false);
                _ideOutput.text = BuildRunOutput(result);
                _ideTestsStatus.text = result.Error != null ? result.Error : "Тесты не пройдены.";
                _ideTestsStatus.EnableInClassList("status-fail", true);
                ShowToast("Работа не принята. Проверьте тесты.", true);
            }
        }

        private void ConfirmGiveUp()
        {
            ShowConfirm("Сдаться без зарплаты?",
                "Задача останется нерешённой, зарплаты не будет. Коллектор всё равно придёт за платежом.",
                () => _game.GiveUpMonth());
        }

        // --------------------------------------------------------------- end

        private void BuildEnd()
        {
            _end = Make("screen");
            _end.AddToClassList("end-screen");
            _root.Add(_end);
            _blockers.Add(_end);

            var panel = Make("end-panel", _end);
            _endKicker = MakeLabel("end-kicker", "КОНЕЦ", panel);
            _endTitle = MakeLabel("end-title", "", panel);
            _endText = MakeLabel("end-text", "", panel);

            _endStats = Make("end-stats", panel);

            var menu = MakeButton("В главное меню", "btn-primary", panel, () => _game.ReturnToMainMenu());
            menu.style.marginTop = 4f;

            Hide(_end);
        }

        public void ShowEnd(bool won)
        {
            if (_game == null) return;

            Hide(_dialogueLayer);
            Hide(_pause);
            Hide(_ide);
            Hide(_sellBackdrop);
            Hide(_confirmBackdrop);

            if (won)
            {
                _endKicker.text = "ИПОТЕКА ЗАКРЫТА";
                _endTitle.text = "ДОМ ВАШ";
                _endText.text =
                    "Двенадцать месяцев, двенадцать задач и ни одной просрочки.\n" +
                    "Коллектор ушёл ни с чем. Квартира больше не принадлежит банку.";
            }
            else
            {
                _endKicker.text = "ДЕФОЛТ";
                _endTitle.text = "КВАРТИРА УШЛА";
                _endText.text =
                    "Денег не хватило, а продавать было уже нечего.\n" +
                    "Коллектор забрал ключи. В следующий раз пишите код быстрее.";
            }

            _endStats.Clear();
            AddEndStat(_endStats, _game.Month + " / " + GameBalance.Months, "МЕСЯЦЕВ");
            AddEndStat(_endStats, GameBalance.Money(_game.Money), "БАЛАНС");
            AddEndStat(_endStats, (_game.InitialItemCount - _game.Items.Count).ToString(), "ПРОДАНО ВЕЩЕЙ");

            RefreshHUD();
            Show(_end);
            _end.BringToFront();
            FadeIn(_end, 0.35f);
        }

        private static void AddEndStat(VisualElement parent, string value, string label)
        {
            var stat = Make("end-stat", parent);
            MakeLabel("end-stat-value", value, stat);
            MakeLabel("end-stat-label", label, stat);
        }

        public void HideEnd()
        {
            Hide(_end);
        }

        // ------------------------------------------------------ month curtain

        private void BuildMonthCurtain()
        {
            _monthCurtain = Make("month-curtain");
            _monthCurtain.pickingMode = PickingMode.Ignore;
            _root.Add(_monthCurtain);

            _monthCurtainLabel = MakeLabel("month-curtain-label", "", _monthCurtain);
            _monthCurtainSub = MakeLabel("month-curtain-sub", "", _monthCurtain);
            Hide(_monthCurtain);
        }

        public Tween FadeMonthCurtainIn(int month)
        {
            if (_monthCurtain == null) return null;

            _monthCurtainLabel.text = "МЕСЯЦ " + month.ToString("00");
            _monthCurtainSub.text = "ИПОТЕКА · ПЛАТЁЖ " + GameBalance.Money(GameBalance.PaymentForMonth(month));
            Show(_monthCurtain);
            _monthCurtain.BringToFront();
            _monthCurtain.style.opacity = 0f;
            DOTween.Kill(_monthCurtain);
            return DOTween.To(() => 0f, v => _monthCurtain.style.opacity = v, 1f, 0.5f)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .SetTarget(_monthCurtain);
        }

        public Tween FadeMonthCurtainOut()
        {
            if (_monthCurtain == null) return null;

            DOTween.Kill(_monthCurtain);
            return DOTween.To(() => 1f, v => _monthCurtain.style.opacity = v, 0f, 0.65f)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true)
                .SetTarget(_monthCurtain)
                .OnComplete(() => Hide(_monthCurtain));
        }

        // -------------------------------------------------------- pointer UI

        public bool IsPointerOverUI(Vector2 screenPoint)
        {
            if (_document == null) return false;
            var panel = _document.rootVisualElement != null ? _document.rootVisualElement.panel : null;
            if (panel == null) return false;

            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(panel, screenPoint);
            for (int i = 0; i < _blockers.Count; i++)
            {
                var blocker = _blockers[i];
                if (!IsShown(blocker)) continue;
                if (blocker.worldBound.Contains(panelPoint)) return true;
            }
            return false;
        }
    }
}
