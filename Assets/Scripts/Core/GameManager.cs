using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MortgageSim.Audio;
using MortgageSim.CameraTool;
using MortgageSim.Core;
using MortgageSim.Interaction;
using MortgageSim.Player;
using MortgageSim.Python;
using MortgageSim.Tasks;
using MortgageSim.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MortgageSim
{
    public enum GamePhase
    {
        MainMenu,
        Working,
        CollectorComing,
        CollectorWaiting,
        CollectorInside,
        CollectorLeaving,
        Won,
        Lost
    }

    /// <summary>
    /// Корень игры: месяц, деньги, долг, задачи, коллектор, пауза и финал.
    /// Сцена SampleScene не меняется — всё собирается в рантайме.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameUI UI { get; private set; }
        public MonitorTerminal Terminal { get; set; }
        public CollectorController Collector { get; set; }
        public FirstPersonPlayer Player { get; set; }
        public PlayerInteractor Interactor { get; set; }
        public CameraDirector CameraDirector { get; private set; }
        public AmbienceController Ambience { get; set; }
        public GameAudio Audio { get; private set; }

        public List<SellableItem> Items { get; } = new List<SellableItem>();

        public int Money { get; private set; }
        public int Month { get; private set; } = 1;
        public int CurrentPayment { get; private set; }
        public int TotalPaid { get; private set; }
        public int InitialItemCount { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.MainMenu;
        public bool TaskSolvedThisMonth { get; private set; }
        public PythonTask CurrentTask { get; private set; }

        public Transform DoorPivot { get; set; }
        public Quaternion DoorClosedRotation { get; set; }
        public Quaternion DoorOpenRotation { get; set; }
        public Bounds DoorBounds { get; set; }
        public bool IsDoorOpen { get; private set; }

        public bool IsPaused { get; private set; }
        public bool IsIDEOpen { get; private set; }
        public bool IsSwitchingView { get; private set; }
        public bool IsChangingMonth { get; private set; }

        public int RemainingDebt => Mathf.Max(0, GameBalance.TotalDebt - TotalPaid);

        public bool CanPlayerInteract =>
            !IsPaused &&
            !IsIDEOpen &&
            !IsSwitchingView &&
            !IsChangingMonth &&
            UI != null &&
            !UI.IsMainMenuVisible &&
            !UI.IsEndVisible &&
            !UI.IsSellConfirmVisible &&
            !UI.IsConfirmVisible &&
            (Phase == GamePhase.Working ||
             Phase == GamePhase.CollectorComing ||
             Phase == GamePhase.CollectorWaiting ||
             Phase == GamePhase.CollectorInside ||
             Phase == GamePhase.CollectorLeaving);

        private readonly List<PythonTask> _tasks = new List<PythonTask>();
        private readonly Vector3 _spawnPoint = new Vector3(0f, 0.02f, -1.35f);
        private Coroutine _arrivalRoutine;
        private Coroutine _returnRoutine;
        private Coroutine _ideRoutine;
        private Coroutine _monthRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Boot()
        {
            DOTween.Init(false, true, LogBehaviour.ErrorsOnly);
            Application.targetFrameRate = 120;

            _tasks.Clear();
            _tasks.AddRange(TaskDatabase.CreateAll());
            Money = GameBalance.StartMoney;
            Month = 1;
            TotalPaid = 0;
            CurrentPayment = GameBalance.PaymentForMonth(1);
            CurrentTask = _tasks[0];

            CameraDirector = gameObject.AddComponent<CameraDirector>();

            Audio = gameObject.AddComponent<GameAudio>();
            Audio.Initialize();

            UI = gameObject.AddComponent<GameUI>();
            UI.Build(this);

            var apartment = new GameObject("Apartment");
            var builder = apartment.AddComponent<ApartmentBuilder>();
            builder.Build(this);

            InitialItemCount = Items.Count;

            if (Player != null)
            {
                Player.Teleport(_spawnPoint, 180f);
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            Phase = GamePhase.MainMenu;
            UI.ShowMainMenu();
            UI.HideHUD();
            UI.HideIDE();
            UI.HideDialogue();
            UI.HidePause();
            UI.HideEnd();
            UI.ResetGloom();

            if (CameraDirector != null) CameraDirector.MenuView();
            if (Terminal != null) Terminal.LogInfo("MORTGAGE_OS: ипотека активна. 12 платежей.");
        }

        private void Update()
        {
            if (UI == null) return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Keyboard.current != null &&
                Keyboard.current.ctrlKey.isPressed &&
                Keyboard.current.mKey.wasPressedThisFrame)
            {
                DevSkipMonth();
                return;
            }
#endif

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
                return;
            }

            if (Phase == GamePhase.MainMenu &&
                Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
            {
                StartGame();
            }
        }

        private void HandleEscape()
        {
            if (IsChangingMonth) return;

            if (UI.IsConfirmVisible)
            {
                UI.HideConfirm();
                return;
            }
            if (UI.IsSellConfirmVisible)
            {
                UI.HideSellConfirm();
                return;
            }
            if (IsIDEOpen)
            {
                CloseComputer();
                return;
            }
            if (UI.IsDialogueVisible)
            {
                CloseCollectorDialogue();
                return;
            }
            if (Phase == GamePhase.MainMenu || Phase == GamePhase.Won || Phase == GamePhase.Lost) return;

            if (IsPaused) Resume();
            else Pause();
        }

        // ---------------------------------------------------------------- run

        public void StartGame()
        {
            StopAllCoroutines();
            _arrivalRoutine = null;
            _returnRoutine = null;
            _monthRoutine = null;

            IsPaused = false;
            IsIDEOpen = false;
            IsSwitchingView = false;
            IsChangingMonth = false;
            IsDoorOpen = false;
            Time.timeScale = 1f;

            Money = GameBalance.StartMoney;
            Month = 1;
            TotalPaid = 0;
            TaskSolvedThisMonth = false;
            CurrentPayment = GameBalance.PaymentForMonth(Month);
            CurrentTask = _tasks[Mathf.Clamp(Month - 1, 0, _tasks.Count - 1)];

            if (DoorPivot != null) DoorPivot.rotation = DoorClosedRotation;
            if (Collector != null) Collector.HideImmediate();

            UI.HideMainMenu();
            UI.HideEnd();
            UI.HidePause();
            UI.HideDialogue();
            UI.HideIDE();
            UI.HideSellConfirm();
            UI.ResetGloom();
            UI.ShowHUD();

            if (Player != null)
            {
                Player.Teleport(_spawnPoint, 180f);
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(true);
            }

            Phase = GamePhase.Working;
            BeginMonth();
            UI.ShowToast("Месяц 1. Решите задачу, чтобы получить зарплату.");
            if (Terminal != null)
            {
                Terminal.LogCommand("start_month.py --month 1");
                Terminal.LogInfo("Зарплата за задачу: " + GameBalance.Money(GameBalance.SalaryForMonth(1)));
            }
        }

        private void BeginMonth()
        {
            TaskSolvedThisMonth = false;
            CurrentPayment = GameBalance.PaymentForMonth(Month);
            CurrentTask = _tasks[Mathf.Clamp(Month - 1, 0, _tasks.Count - 1)];
            Phase = GamePhase.Working;

            UI.RefreshHUD();
            UI.SetObjective(
                "Месяц " + Month + ": " + CurrentTask.Title,
                "Откройте компьютер и сдайте работу. Платёж: " + GameBalance.Money(CurrentPayment));

            if (Collector != null) Collector.HideImmediate();
            if (Terminal != null)
            {
                Terminal.LogInfo("=== МЕСЯЦ " + Month + " / " + GameBalance.Months + " ===");
                Terminal.LogInfo("К оплате: " + GameBalance.Money(CurrentPayment) +
                                 " · зарплата: " + GameBalance.Money(GameBalance.SalaryForMonth(Month)));
            }
        }

        public void ReturnToMainMenu()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            IsPaused = false;
            IsIDEOpen = false;
            IsChangingMonth = false;
            Phase = GamePhase.MainMenu;

            if (UI != null)
            {
                UI.HideConfirm();
                UI.HideSellConfirm();
                UI.HideDialogue();
                UI.HidePause();
                UI.HideIDE();
                UI.HideEnd();
            }
            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Pause()
        {
            if (IsPaused || IsChangingMonth || Phase == GamePhase.MainMenu || Phase == GamePhase.Won || Phase == GamePhase.Lost) return;
            if (IsIDEOpen) return;

            IsPaused = true;
            Time.timeScale = 0f;
            UI.ShowPause();
            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Resume()
        {
            if (!IsPaused) return;

            IsPaused = false;
            Time.timeScale = 1f;
            UI.HidePause();
            UI.ShowHUD();
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(!UI.IsDialogueVisible);
            }
            Cursor.lockState = UI.IsDialogueVisible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = UI.IsDialogueVisible;
        }

        // ------------------------------------------------------------- modals

        /// <summary>Любое модальное окно (продажа, подтверждение) забирает курсор у камеры.</summary>
        public void OnModalOpened()
        {
            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Возвращает управление и захват курсора, если не открыто другое окно.</summary>
        public void OnModalClosed()
        {
            if (UI != null && (UI.IsConfirmVisible || UI.IsSellConfirmVisible)) return;
            if (IsPaused || IsIDEOpen || IsChangingMonth) return;
            if (Phase == GamePhase.MainMenu || Phase == GamePhase.Won || Phase == GamePhase.Lost) return;

            bool dialogue = UI != null && UI.IsDialogueVisible;
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(!dialogue);
            }
            Cursor.lockState = dialogue ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = dialogue;
        }

        // ------------------------------------------------------------ computer

        public void OpenComputer()
        {
            if (IsIDEOpen || IsPaused || IsSwitchingView) return;
            if (Phase != GamePhase.Working && Phase != GamePhase.CollectorComing &&
                Phase != GamePhase.CollectorWaiting && Phase != GamePhase.CollectorInside) return;

            IsIDEOpen = true;
            IsSwitchingView = true;

            UI.HideHUD();
            if (CameraDirector != null && Terminal != null) CameraDirector.EnterComputer(Terminal);

            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Terminal != null) Terminal.LogCommand("open ide/" + CurrentTask.Id + ".py");

            if (_returnRoutine != null) StopCoroutine(_returnRoutine);
            _returnRoutine = StartCoroutine(ClearViewSwitchAfter(1.15f));

            // Сначала даём камере «прилипнуть» к монитору, и только потом
            // раскрываем IDE — так виден сам эффект приближения.
            if (_ideRoutine != null) StopCoroutine(_ideRoutine);
            _ideRoutine = StartCoroutine(OpenIdeAfterCamera(0.82f));
        }

        private IEnumerator OpenIdeAfterCamera(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (!IsIDEOpen) yield break;
            UI.OpenIDE(CurrentTask);
        }

        public void CloseComputer()
        {
            if (!IsIDEOpen) return;

            IsIDEOpen = false;
            IsSwitchingView = true;
            if (_ideRoutine != null)
            {
                StopCoroutine(_ideRoutine);
                _ideRoutine = null;
            }
            UI.CloseIDE();

            if (CameraDirector != null) CameraDirector.ExitComputer();
            if (Player != null) Player.SetInputEnabled(false);

            if (Terminal != null) Terminal.LogInfo("IDE закрыта.");

            if (_returnRoutine != null) StopCoroutine(_returnRoutine);
            _returnRoutine = StartCoroutine(RestorePlayerAfter(0.95f));
        }

        private IEnumerator ClearViewSwitchAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            IsSwitchingView = false;
        }

        private IEnumerator RestorePlayerAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            IsSwitchingView = false;
            if (IsIDEOpen || IsPaused || IsChangingMonth || Phase == GamePhase.MainMenu ||
                Phase == GamePhase.Won || Phase == GamePhase.Lost) yield break;

            UI.ShowHUD();
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(!UI.IsDialogueVisible);
            }
            Cursor.lockState = UI.IsDialogueVisible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = UI.IsDialogueVisible;
        }

        public PythonRunResult SubmitTaskSolution(string code)
        {
            var result = PythonTaskRunner.Run(CurrentTask, code);
            if (!result.Success)
            {
                if (Terminal != null) Terminal.LogFail("Тесты не пройдены: " + result.PassedCount + "/" + result.TotalCount);
                return result;
            }
            if (TaskSolvedThisMonth)
            {
                result.Error = "Эта задача уже сдана. Ждите коллектора.";
                return result;
            }

            TaskSolvedThisMonth = true;
            int salary = GameBalance.SalaryForMonth(Month);
            Money += salary;

            if (Terminal != null)
            {
                Terminal.LogCommand("submit " + CurrentTask.Id + ".py");
                Terminal.LogOk("Задача принята. Зарплата: " + GameBalance.Money(salary));
            }

            UI.ShowToast("Работа сдана. Зарплата " + GameBalance.Money(salary) + " зачислена.");
            UI.SetObjective(
                "Работа сдана",
                "Коллектор уже в пути. Пришло время платить: " + GameBalance.Money(CurrentPayment));
            UI.RefreshHUD();

            BeginCollectorArrival();
            return result;
        }

        public void GiveUpMonth()
        {
            if (Phase != GamePhase.Working || TaskSolvedThisMonth) return;

            if (IsIDEOpen) CloseComputer();

            UI.ShowToast("Вы сдали месяц без зарплаты. Коллектор уже в пути.", true);
            UI.SetObjective(
                "Работа не сдана",
                "Зарплаты не будет. Придётся платить из своих или продавать мебель.");
            if (Terminal != null) Terminal.LogWarn("Работа не сдана. Зарплаты не будет.");

            BeginCollectorArrival();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Dev-хоткей Ctrl+M: закрывает текущий месяц без штрафов и сразу
        /// переходит к следующей задаче. Нужен, чтобы быстро просматривать уровни.
        /// </summary>
        public void DevSkipMonth()
        {
            if (Phase == GamePhase.MainMenu || Phase == GamePhase.Won ||
                Phase == GamePhase.Lost || IsChangingMonth) return;

            GamePhase previousPhase = Phase;

            StopAllCoroutines();
            _arrivalRoutine = null;
            _returnRoutine = null;
            _ideRoutine = null;
            _monthRoutine = null;

            IsPaused = false;
            IsSwitchingView = false;
            Time.timeScale = 1f;
            Phase = GamePhase.Working;

            UI.HidePause();
            UI.HideConfirm();
            UI.HideSellConfirm();
            UI.HideDialogue();

            if (IsIDEOpen)
            {
                IsIDEOpen = false;
                UI.CloseIDE();
                if (CameraDirector != null) CameraDirector.ReturnToPlayerImmediate();
            }

            // Безболезненно: зарплата начисляется, платёж считается внесённым.
            if (!TaskSolvedThisMonth)
            {
                TaskSolvedThisMonth = true;
                Money += GameBalance.SalaryForMonth(Month);
            }
            if (previousPhase != GamePhase.CollectorLeaving) TotalPaid += CurrentPayment;

            if (Collector != null) Collector.HideImmediate();
            CloseDoor();
            if (Audio != null) Audio.StopCollectorTheme();

            if (Terminal != null)
            {
                Terminal.LogWarn("[DEV] Месяц " + Month + " пропущен без штрафов.");
            }

            if (Month >= GameBalance.Months)
            {
                Win();
                return;
            }

            Month++;
            _monthRoutine = StartCoroutine(NextMonthRoutine());
        }
#endif

        // ------------------------------------------------------------ collector

        private void BeginCollectorArrival()
        {
            if (_arrivalRoutine != null) StopCoroutine(_arrivalRoutine);
            _arrivalRoutine = StartCoroutine(CollectorArrivalRoutine());
            if (Audio != null) Audio.PlayCollectorTheme();
        }

        private IEnumerator CollectorArrivalRoutine()
        {
            Phase = GamePhase.CollectorComing;
            UI.SetObjective(
                "Коллектор уже в пути",
                "Когда он постучит, откройте картину-дверь.");
            if (Terminal != null) Terminal.LogWarn("В подъезде шаги: коллектор идёт.");

            yield return new WaitForSeconds(1.35f);
            if (Phase != GamePhase.CollectorComing) yield break;

            if (Collector != null) Collector.BeginArrival(OnCollectorKnocked);
        }

        private void OnCollectorKnocked()
        {
            if (Phase != GamePhase.CollectorComing) return;
            Phase = GamePhase.CollectorWaiting;

            UI.SetObjective(
                "Коллектор стучит в дверь",
                "Откройте картину-дверь. К оплате: " + GameBalance.Money(CurrentPayment));
            if (Audio != null) Audio.Knock();
            UI.ShowToast("В дверь стучат. Пришло время платить " + GameBalance.Money(CurrentPayment) + ".");
            if (Terminal != null) Terminal.LogWarn("Коллектор стучит. Сумма: " + GameBalance.Money(CurrentPayment));
        }

        public void InteractDoor()
        {
            if (Phase == GamePhase.MainMenu || Phase == GamePhase.Won || Phase == GamePhase.Lost) return;

            if (IsDoorOpen)
            {
                if (Phase == GamePhase.CollectorWaiting || Phase == GamePhase.CollectorInside)
                {
                    UI.ShowToast("Дверь открыта для коллектора.");
                    return;
                }
                CloseDoor();
                return;
            }

            if (Collector != null && Collector.IsAtDoor)
            {
                OpenDoor();
                return;
            }

            if (Phase == GamePhase.CollectorInside)
            {
                UI.ShowToast("Коллектор уже внутри. Поговорите с ним.");
                return;
            }

            UI.ShowToast("Пока никто не стучит. Сначала сдайте работу.");
        }

        public void OpenDoor()
        {
            if (DoorPivot == null || IsDoorOpen) return;
            IsDoorOpen = true;
            DoorPivot.DOKill();
            DoorPivot.DORotateQuaternion(DoorOpenRotation, 0.55f).SetEase(Ease.OutCubic);
            if (Audio != null) Audio.Door();
            if (Terminal != null) Terminal.LogInfo("Картина-дверь открыта.");

            if (Collector != null && Collector.IsAtDoor)
            {
                if (_arrivalRoutine != null) StopCoroutine(_arrivalRoutine);
                _arrivalRoutine = StartCoroutine(CollectorSteppingInside());
            }
        }

        private IEnumerator CollectorSteppingInside()
        {
            yield return new WaitForSeconds(0.42f);
            if (Collector == null) yield break;

            Collector.Enter(OnCollectorEntered);
        }

        public void CloseDoor()
        {
            if (DoorPivot == null || !IsDoorOpen) return;
            IsDoorOpen = false;
            DoorPivot.DOKill();
            DoorPivot.DORotateQuaternion(DoorClosedRotation, 0.5f).SetEase(Ease.InOutQuad);
            if (Audio != null) Audio.Door();
        }

        private void OnCollectorEntered()
        {
            Phase = GamePhase.CollectorInside;

            UI.SetObjective(
                "Коллектор в квартире",
                "Оплатите " + GameBalance.Money(CurrentPayment) + " или продайте вещи.");
            UI.RefreshHUD();
            UI.ShowCollectorDialogue(true);
            LockCursorForDialogue();

            if (Terminal != null)
            {
                Terminal.LogWarn("Коллектор: «Пришло время платить — " + GameBalance.Money(CurrentPayment) + "»");
                Terminal.LogInfo("Можно ходить по квартире и продавать мебель.");
            }
        }

        public void TalkToCollector()
        {
            if (Phase != GamePhase.CollectorInside || Collector == null || !Collector.IsInside) return;

            UI.ShowCollectorDialogue(false);
            LockCursorForDialogue();
            if (Terminal != null) Terminal.LogCommand("talk collector");
        }

        private void LockCursorForDialogue()
        {
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void CloseCollectorDialogue()
        {
            UI.HideDialogue();
            if (IsPaused || IsIDEOpen) return;
            if (Phase == GamePhase.MainMenu || Phase == GamePhase.Won || Phase == GamePhase.Lost) return;
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(true);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void PayCollector()
        {
            if (Phase != GamePhase.CollectorInside) return;

            if (Money < CurrentPayment)
            {
                UI.ShowToast("Не хватает " + GameBalance.Money(CurrentPayment - Money) + ". Продайте что-нибудь.", true);
                if (Terminal != null) Terminal.LogFail("Недостаточно средств. Нужно " + GameBalance.Money(CurrentPayment));
                return;
            }

            Money -= CurrentPayment;
            TotalPaid += CurrentPayment;
            int paid = CurrentPayment;
            if (Audio != null) Audio.Pay();

            if (Terminal != null)
            {
                Terminal.LogCommand("pay collector " + GameBalance.Money(paid));
                Terminal.LogOk("Платёж принят. Осталось долга: " + GameBalance.Money(RemainingDebt));
            }

            UI.HideDialogue();
            UI.ShowToast("Платёж " + GameBalance.Money(paid) + " принят. Месяц закрыт.");
            UI.SetObjective("Месяц " + Month + " закрыт", "Коллектор уходит.");
            UI.RefreshHUD();

            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(true);
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Phase = GamePhase.CollectorLeaving;
            if (Collector != null) Collector.Leave(OnCollectorLeft);
            else OnCollectorLeft();
        }

        private void OnCollectorLeft()
        {
            if (Phase != GamePhase.CollectorLeaving) return;

            CloseDoor();

            if (Month >= GameBalance.Months)
            {
                Win();
                return;
            }

            Month++;
            if (_monthRoutine != null) StopCoroutine(_monthRoutine);
            _monthRoutine = StartCoroutine(NextMonthRoutine());
        }

        private IEnumerator NextMonthRoutine()
        {
            IsChangingMonth = true;
            if (Player != null) Player.SetInputEnabled(false);
            if (Audio != null) Audio.MonthTransition();

            Tween fadeIn = UI.FadeMonthCurtainIn(Month);
            if (fadeIn != null) yield return fadeIn.WaitForCompletion();

            if (IsIDEOpen) CloseComputer();
            Phase = GamePhase.Working;
            BeginMonth();

            yield return new WaitForSecondsRealtime(0.65f);
            Tween fadeOut = UI.FadeMonthCurtainOut();
            if (fadeOut != null) yield return fadeOut.WaitForCompletion();

            IsChangingMonth = false;
            if (Player != null)
            {
                Player.SetInputEnabled(true);
                Player.SetCursorLocked(!UI.IsDialogueVisible);
            }
            Cursor.lockState = UI.IsDialogueVisible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = UI.IsDialogueVisible;

            UI.ShowToast("Месяц " + Month + " начался. Новая задача уже в компьютере.");
            if (Terminal != null) Terminal.LogOk("Месяц " + Month + " начался.");
            _monthRoutine = null;
        }

        public void OnCollectorLeftEvent()
        {
            OnCollectorLeft();
        }

        public void OnItemSold(SellableItem item)
        {
            if (item == null) return;
            Money += item.Price;
            Items.Remove(item);

            if (Ambience != null) Ambience.Apply(Items.Count, InitialItemCount);
            if (UI != null) UI.SetGloom(Items.Count, InitialItemCount);
            if (Audio != null) Audio.Sell();

            UI.ShowToast("Продано: " + item.DisplayName + " (+" + GameBalance.Money(item.Price) + ")");
            UI.RefreshHUD();

            if (Terminal != null)
                Terminal.LogInfo("Осталось предметов: " + Items.Count + " · баланс " + GameBalance.Money(Money));
        }

        public void DeclareBankruptcy()
        {
            if (Phase == GamePhase.Won || Phase == GamePhase.Lost || Phase == GamePhase.MainMenu) return;

            StopAllCoroutines();
            Time.timeScale = 1f;
            IsPaused = false;
            IsIDEOpen = false;
            IsChangingMonth = false;
            Phase = GamePhase.Lost;

            UI.HideConfirm();
            UI.HideSellConfirm();
            UI.HideDialogue();
            UI.HidePause();
            UI.HideIDE();
            UI.ShowHUD();

            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UI.ShowEnd(false);
            if (Terminal != null) Terminal.LogFail("Ипотека не выплачена. Игра окончена.");
        }

        private void Win()
        {
            StopAllCoroutines();
            Time.timeScale = 1f;
            IsPaused = false;
            IsIDEOpen = false;
            IsChangingMonth = false;
            Phase = GamePhase.Won;

            UI.HideDialogue();
            UI.HidePause();
            UI.HideIDE();
            UI.ShowHUD();

            if (Player != null)
            {
                Player.SetInputEnabled(false);
                Player.SetCursorLocked(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UI.ShowEnd(true);
            if (Terminal != null) Terminal.LogOk("ИПОТЕКА ВЫПЛАЧЕНА. Дом ваш.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
            DOTween.KillAll();
        }
    }
}
