using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Models;
using SwMateAI.Core.Planning;

namespace SwMateAI.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the SW-MATE AI Task Pane.
    /// Coordinates between <see cref="AgentCore"/> and the WPF data bindings.
    /// Implements MVVM pattern via <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public class TaskPaneViewModel : INotifyPropertyChanged
    {
        private readonly AgentCore _agentCore;

        // ─── Backing fields ───────────────────────────────────────────────────

        private bool   _isConnected;
        private string _swVersion       = string.Empty;
        private bool   _hasActiveDoc;
        private string _documentType    = "None";
        private string _documentTitle   = string.Empty;
        private string _filePath        = string.Empty;
        private bool   _isSaved;
        private string _statusText      = "Connecting…";
        private bool   _isRefreshing;
        private string _rectangleWidth = "60";
        private string _rectangleHeight = "40";
        private string _extrudeDepth = "20";
        private string _circleDiameter = "10";
        private string _circleX = "0";
        private string _circleY = "0";
        private string _cutDepth = "10";
        private string _plateWidth = "100";
        private string _plateHeight = "60";
        private string _plateThickness = "10";
        private string _plateHoleDiameter = "10";
        private string _plateHoleDepth = "10";
        private string _naturalLanguageCommand = "Tạo tấm 120 x 80 x 15 mm, lỗ phi 12 ở giữa";
        private string _planGoal = "No active plan";
        private string _lastResultText = "Ready for a command.";
        private string _agentStageText = "Idle";
        private string _activeConfiguration = "—";
        private string _selectionSummary = "0 selected";
        private bool _hasPlan;
        private bool _lastRunSucceeded;
        private string _dimensionName = string.Empty;
        private string _dimensionValue = "20";

        // ─── Public properties (bound to XAML) ───────────────────────────────

        public bool IsConnected
        {
            get => _isConnected;
            private set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionColor)); }
        }

        public string SwVersion
        {
            get => _swVersion;
            private set { _swVersion = value; OnPropertyChanged(); }
        }

        public bool HasActiveDoc
        {
            get => _hasActiveDoc;
            private set { _hasActiveDoc = value; OnPropertyChanged(); }
        }

        public string DocumentType
        {
            get => _documentType;
            private set { _documentType = value; OnPropertyChanged(); OnPropertyChanged(nameof(DocTypeIcon)); }
        }

        public string DocumentTitle
        {
            get => _documentTitle;
            private set { _documentTitle = value; OnPropertyChanged(); }
        }

        public string FilePath
        {
            get => _filePath;
            private set { _filePath = value; OnPropertyChanged(); }
        }

        public bool IsSaved
        {
            get => _isSaved;
            private set { _isSaved = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        /// <summary>Green when connected, red when not.</summary>
        public string ConnectionColor => IsConnected ? "#2ECC71" : "#E74C3C";

        /// <summary>Icon character representing the document type.</summary>
        public string DocTypeIcon
        {
            get
            {
                return DocumentType switch
                {
                    "Part"     => "◈",
                    "Assembly" => "⬡",
                    "Drawing"  => "▭",
                    _          => "○"
                };
            }
        }

        public string RectangleWidth { get => _rectangleWidth; set { _rectangleWidth = value; OnPropertyChanged(); } }
        public string RectangleHeight { get => _rectangleHeight; set { _rectangleHeight = value; OnPropertyChanged(); } }
        public string ExtrudeDepth { get => _extrudeDepth; set { _extrudeDepth = value; OnPropertyChanged(); } }
        public string CircleDiameter { get => _circleDiameter; set { _circleDiameter = value; OnPropertyChanged(); } }
        public string CircleX { get => _circleX; set { _circleX = value; OnPropertyChanged(); } }
        public string CircleY { get => _circleY; set { _circleY = value; OnPropertyChanged(); } }
        public string CutDepth { get => _cutDepth; set { _cutDepth = value; OnPropertyChanged(); } }
        public string PlateWidth { get => _plateWidth; set { _plateWidth = value; OnPropertyChanged(); } }
        public string PlateHeight { get => _plateHeight; set { _plateHeight = value; OnPropertyChanged(); } }
        public string PlateThickness { get => _plateThickness; set { _plateThickness = value; OnPropertyChanged(); } }
        public string PlateHoleDiameter { get => _plateHoleDiameter; set { _plateHoleDiameter = value; OnPropertyChanged(); } }
        public string PlateHoleDepth { get => _plateHoleDepth; set { _plateHoleDepth = value; OnPropertyChanged(); } }
        public string DimensionName { get => _dimensionName; set { _dimensionName = value; OnPropertyChanged(); } }
        public string DimensionValue { get => _dimensionValue; set { _dimensionValue = value; OnPropertyChanged(); } }
        public string NaturalLanguageCommand { get => _naturalLanguageCommand; set { _naturalLanguageCommand = value; OnPropertyChanged(); } }
        public string PlanGoal { get => _planGoal; private set { _planGoal = value; OnPropertyChanged(); } }
        public string LastResultText { get => _lastResultText; private set { _lastResultText = value; OnPropertyChanged(); } }
        public string AgentStageText { get => _agentStageText; private set { _agentStageText = value; OnPropertyChanged(); OnPropertyChanged(nameof(AgentStageColor)); } }
        public string ActiveConfiguration { get => _activeConfiguration; private set { _activeConfiguration = value; OnPropertyChanged(); } }
        public string SelectionSummary { get => _selectionSummary; private set { _selectionSummary = value; OnPropertyChanged(); } }
        public bool HasPlan { get => _hasPlan; private set { _hasPlan = value; OnPropertyChanged(); } }
        public bool LastRunSucceeded { get => _lastRunSucceeded; private set { _lastRunSucceeded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultColor)); } }
        public string AgentStageColor => AgentStageText == "Failed" ? "#EF4444" : AgentStageText == "Completed" ? "#22C55E" : "#38BDF8";
        public string ResultColor => LastRunSucceeded ? "#22C55E" : "#94A3B8";

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        /// <summary>Log entries shown in the agent console panel.</summary>
        public ObservableCollection<string> ConsoleLog { get; } = new ObservableCollection<string>();
        public ObservableCollection<PlanStep> CurrentPlanSteps { get; } = new ObservableCollection<PlanStep>();
        public ObservableCollection<string> CommandHistory { get; } = new ObservableCollection<string>();

        // ─── Commands ─────────────────────────────────────────────────────────

        public ICommand RefreshInfoCommand { get; }
        public ICommand CreatePartCommand { get; }
        public ICommand CreateSketchCommand { get; }
        public ICommand CreateRectangleCommand { get; }
        public ICommand ExtrudeCommand { get; }
        public ICommand CreateCircleCommand { get; }
        public ICommand CutExtrudeCommand { get; }
        public ICommand CreatePlateWithHoleCommand { get; }
        public ICommand ExecuteNaturalLanguageCommand { get; }
        public ICommand AddDimensionCommand { get; }
        public ICommand ModifyDimensionCommand { get; }

        // ─── Constructor ──────────────────────────────────────────────────────

        public TaskPaneViewModel(AgentCore agentCore)
        {
            _agentCore = agentCore ?? throw new ArgumentNullException(nameof(agentCore));

            RefreshInfoCommand = new RelayCommand(
                execute:    RefreshInfo,
                canExecute: () => !IsRefreshing);

            CreatePartCommand = new RelayCommand(
                execute:    CreatePart,
                canExecute: () => !IsRefreshing);

            CreateSketchCommand = new RelayCommand(
                execute:    CreateSketch,
                canExecute: () => !IsRefreshing);

            CreateRectangleCommand = new RelayCommand(
                execute:    CreateRectangle,
                canExecute: () => !IsRefreshing);

            ExtrudeCommand = new RelayCommand(
                execute:    Extrude,
                canExecute: () => !IsRefreshing);

            CreateCircleCommand = new RelayCommand(
                execute:    CreateCircle,
                canExecute: () => !IsRefreshing);

            CutExtrudeCommand = new RelayCommand(
                execute:    CutExtrude,
                canExecute: () => !IsRefreshing);

            CreatePlateWithHoleCommand = new RelayCommand(
                execute:    CreatePlateWithHole,
                canExecute: () => !IsRefreshing);

            ExecuteNaturalLanguageCommand = new RelayCommand(
                execute:    ExecuteNaturalLanguage,
                canExecute: () => !IsRefreshing);

            AddDimensionCommand = new RelayCommand(
                execute:    AddDimension,
                canExecute: () => !IsRefreshing);

            ModifyDimensionCommand = new RelayCommand(
                execute:    ModifyDimension,
                canExecute: () => !IsRefreshing);
        }

        // ─── Actions ──────────────────────────────────────────────────────────

        /// <summary>
        /// Invokes the GetModelInfo tool and updates all bound properties.
        /// Called on startup and when the user presses Refresh.
        /// </summary>
        public void RefreshInfo()
        {
            IsRefreshing = true;
            AddLog("> GetModelInfo called");

            try
            {
                var result = _agentCore.ExecuteTool("GetModelInfo");

                if (!result.IsSuccess)
                {
                    AddLog($"  [ERR] {result.ErrorMessage}");
                    StatusText = "Error retrieving model info.";
                    return;
                }

                var info = result.Data as ModelInfo;
                if (info == null)
                {
                    AddLog("  [ERR] Unexpected result type from GetModelInfo.");
                    return;
                }

                // Update all bound properties
                IsConnected   = info.IsConnected;
                SwVersion     = info.SolidWorksVersion;
                HasActiveDoc  = info.HasActiveDocument;
                DocumentType  = info.DocumentType;
                DocumentTitle = info.DocumentTitle;
                FilePath      = info.FilePath;
                IsSaved       = info.IsSaved;
                StatusText    = IsConnected ? $"SOLIDWORKS {SwVersion}" : "Not connected";

                AddLog($"  IsConnected   : {info.IsConnected}");
                AddLog($"  SW Version    : {info.SolidWorksVersion}");
                AddLog($"  Document Type : {info.DocumentType}");
                if (info.HasActiveDocument)
                {
                    AddLog($"  Title         : {info.DocumentTitle}");
                    AddLog($"  Saved         : {info.IsSaved}");
                    if (info.IsSaved) AddLog($"  Path          : {info.FilePath}");
                }

                var context = _agentCore.ObserveContext();
                ActiveConfiguration = string.IsNullOrWhiteSpace(context.ActiveConfiguration) ? "—" : context.ActiveConfiguration;
                SelectionSummary = $"{context.SelectedObjectCount} selected";
                AddLog($"  Active Config : {ActiveConfiguration}");
                AddLog($"  Selection     : {context.SelectedObjectCount} object(s)");
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = "Unexpected error.";
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void CreatePart()
        {
            AddLog("> CreatePart called");

            try
            {
                var result = _agentCore.ExecuteTool("CreatePart");

                if (!result.IsSuccess)
                {
                    AddLog($"  [ERR] {result.ErrorMessage}");
                    StatusText = "Failed to create Part.";
                    return;
                }

                AddLog($"  [OK] {result.Data}");
                StatusText = "New Part created.";
                RefreshInfo();
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = "Unexpected error creating Part.";
            }
        }

        private void CreateSketch()
        {
            AddLog("> CreateSketch called");

            try
            {
                var result = _agentCore.ExecuteTool("CreateSketch");

                if (!result.IsSuccess)
                {
                    AddLog($"  [ERR] {result.ErrorMessage}");
                    StatusText = "Failed to create Sketch.";
                    return;
                }

                AddLog($"  [OK] {result.Data}");
                StatusText = "New Sketch created.";
                RefreshInfo();
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = "Unexpected error creating Sketch.";
            }
        }

        private void CreateRectangle()
        {
            AddLog($"> CreateRectangle called ({RectangleWidth} x {RectangleHeight} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("CreateRectangle", new Dictionary<string, object>
                {
                    ["Width"] = RectangleWidth,
                    ["Height"] = RectangleHeight
                });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to create Rectangle."; return; }
                AddLog($"  [OK] {result.Data}");
                StatusText = "Rectangle created.";
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected rectangle error."; }
        }

        private void Extrude()
        {
            AddLog($"> Extrude called ({ExtrudeDepth} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("Extrude", new Dictionary<string, object> { ["Depth"] = ExtrudeDepth });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to Extrude."; return; }
                AddLog($"  [OK] {result.Data}");
                StatusText = "Boss-Extrude created.";
                RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected extrude error."; }
        }

        private void CreateCircle()
        {
            AddLog($"> CreateCircle called (Ø{CircleDiameter} mm, X={CircleX}, Y={CircleY})");
            try
            {
                var result = _agentCore.ExecuteTool("CreateCircle", new Dictionary<string, object>
                {
                    ["Diameter"] = CircleDiameter, ["X"] = CircleX, ["Y"] = CircleY
                });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to create Circle."; return; }
                AddLog($"  [OK] {result.Data}"); StatusText = "Circle created.";
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected circle error."; }
        }

        private void CutExtrude()
        {
            AddLog($"> CutExtrude called ({CutDepth} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("CutExtrude", new Dictionary<string, object> { ["Depth"] = CutDepth });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to Cut-Extrude."; return; }
                AddLog($"  [OK] {result.Data}"); StatusText = "Cut-Extrude created."; RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected cut error."; }
        }

        private void CreatePlateWithHole()
        {
            AddLog($"> AUTO PlateWithHole ({PlateWidth} x {PlateHeight} x {PlateThickness}, Ø{PlateHoleDiameter})");
            try
            {
                var result = _agentCore.ExecuteTool("CreatePlateWithHole", new Dictionary<string, object>
                {
                    ["Width"] = PlateWidth,
                    ["Height"] = PlateHeight,
                    ["Thickness"] = PlateThickness,
                    ["HoleDiameter"] = PlateHoleDiameter,
                    ["HoleDepth"] = PlateHoleDepth
                });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Auto workflow failed."; return; }
                AddLog($"  [OK] {result.Data}"); StatusText = "Plate with hole created automatically."; RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected auto workflow error."; }
        }

        private void AddDimension()
        {
            AddLog($"> AddDimension called ({DimensionValue} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("AddDimension", new Dictionary<string, object> { ["Value"] = DimensionValue });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to add dimension."; return; }
                AddLog($"  [OK] {result.Data}"); StatusText = "Dimension added."; RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected dimension error."; }
        }

        private void ModifyDimension()
        {
            AddLog($"> ModifyDimension called ({DimensionName}, {DimensionValue} mm)");
            try
            {
                var args = new Dictionary<string, object> { ["Value"] = DimensionValue };
                if (!string.IsNullOrWhiteSpace(DimensionName)) args["Name"] = DimensionName;
                var result = _agentCore.ExecuteTool("ModifyDimension", args);
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "Failed to modify dimension."; return; }
                AddLog($"  [OK] {result.Data}"); StatusText = "Dimension modified."; RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = "Unexpected dimension error."; }
        }

        private void ExecuteNaturalLanguage()
        {
            AddLog($"> COMMAND: {NaturalLanguageCommand}");
            if (!NaturalLanguageCadParser.TryParse(NaturalLanguageCommand, out var command, out var parseError))
            {
                AddLog($"  [PARSE ERR] {parseError}");
                StatusText = "Could not understand CAD command.";
                return;
            }

            if (command.Intent == "ModifyDimension")
            {
                AddLog($"  [PARSED] Modify dimension {command.DimensionName} -> {command.DimensionValue:0.###} mm");
            }
            else
            {
                string holesInfo = command.Holes.Count > 0 ? $"{command.Holes.Count} hole(s)" : "no holes";
                string cornerInfo = command.FilletRadius > 0 ? $", fillet R{command.FilletRadius}" :
                                    command.ChamferDistance > 0 ? $", chamfer {command.ChamferDistance} mm" : string.Empty;
                AddLog($"  [PARSED] Plate {command.Width} x {command.Height} x {command.Thickness} mm, {holesInfo}{cornerInfo}");
            }

            var plan = BasicCadPlanner.Build(command);
            PlanGoal = plan.Goal;
            HasPlan = true;
            CurrentPlanSteps.Clear();
            foreach (var step in plan.Steps) CurrentPlanSteps.Add(step);
            AgentStageText = "Planning";
            LastResultText = "Plan ready. Executing skills...";
            LastRunSucceeded = false;
            AddLog($"  [PLAN] {plan.Goal}");
            foreach (var step in plan.Steps) AddLog($"    {step.Index}. {step.SkillName} - {step.Description}");

            _agentCore.State.CurrentRequest = NaturalLanguageCommand;
            AgentStageText = "Executing";
            var execution = _agentCore.ExecutePlan(plan);
            if (!execution.IsSuccess)
            {
                CurrentPlanSteps.Clear(); foreach (var step in plan.Steps) CurrentPlanSteps.Add(step);
                AgentStageText = "Failed";
                LastResultText = execution.Error;
                LastRunSucceeded = false;
                CommandHistory.Insert(0, $"FAIL • {NaturalLanguageCommand}");
                AddLog($"  [AGENT ERR] {execution.Error}");
                StatusText = "Agent plan failed.";
                return;
            }
            CurrentPlanSteps.Clear(); foreach (var step in plan.Steps) CurrentPlanSteps.Add(step);
            AgentStageText = "Completed";
            LastRunSucceeded = true;
            LastResultText = $"Completed and verified {execution.CompletedSteps}/{plan.Steps.Count} skill(s).";
            CommandHistory.Insert(0, $"OK • {NaturalLanguageCommand}");
            while (CommandHistory.Count > 20) CommandHistory.RemoveAt(CommandHistory.Count - 1);
            AddLog($"  [CHECK] Completed {execution.CompletedSteps}/{plan.Steps.Count} step(s). Model verification passed.");

            StatusText = "CAD command completed.";
            RefreshInfo();
        }

        private void AddLog(string message)
        {
            ConsoleLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            // Keep console manageable — cap at 200 entries
            while (ConsoleLog.Count > 200)
                ConsoleLog.RemoveAt(0);
        }

        // ─── INotifyPropertyChanged ───────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
