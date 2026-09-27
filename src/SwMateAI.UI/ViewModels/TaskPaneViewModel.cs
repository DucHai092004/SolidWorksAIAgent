using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Models;

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
        public string NaturalLanguageCommand { get => _naturalLanguageCommand; set { _naturalLanguageCommand = value; OnPropertyChanged(); } }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        /// <summary>Log entries shown in the agent console panel.</summary>
        public ObservableCollection<string> ConsoleLog { get; } = new ObservableCollection<string>();

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

        private void ExecuteNaturalLanguage()
        {
            AddLog($"> COMMAND: {NaturalLanguageCommand}");
            if (!NaturalLanguageCadParser.TryParse(NaturalLanguageCommand, out var command, out var parseError))
            {
                AddLog($"  [PARSE ERR] {parseError}");
                StatusText = "Could not understand CAD command.";
                return;
            }

            string holesInfo = command.Holes.Count > 0 ? $"{command.Holes.Count} hole(s)" : "no holes";
            string cornerInfo = command.FilletRadius > 0 ? $", fillet R{command.FilletRadius}" :
                                command.ChamferDistance > 0 ? $", chamfer {command.ChamferDistance} mm" : string.Empty;
            AddLog($"  [PARSED] Plate {command.Width} x {command.Height} x {command.Thickness} mm, {holesInfo}{cornerInfo}");

            var toolParameters = new Dictionary<string, object>
            {
                ["Width"] = command.Width,
                ["Height"] = command.Height,
                ["Thickness"] = command.Thickness
            };
            if (command.Holes.Count > 0)
            {
                toolParameters["Holes"] = command.Holes;
                toolParameters["HoleDepth"] = command.Thickness;
            }

            var result = _agentCore.ExecuteTool(command.Intent, toolParameters);
            if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = "CAD command failed."; return; }
            AddLog($"  [OK] {result.Data}");

            if (command.FilletRadius > 0)
            {
                var fillet = _agentCore.ExecuteTool("FilletPlateCorners", new Dictionary<string, object> { ["Radius"] = command.FilletRadius });
                if (!fillet.IsSuccess) { AddLog($"  [ERR] Base created, fillet failed: {fillet.ErrorMessage}"); StatusText = "Part created; fillet failed."; return; }
                AddLog($"  [OK] {fillet.Data}");
            }
            else if (command.ChamferDistance > 0)
            {
                var chamfer = _agentCore.ExecuteTool("ChamferPlateCorners", new Dictionary<string, object> { ["Distance"] = command.ChamferDistance });
                if (!chamfer.IsSuccess) { AddLog($"  [ERR] Base created, chamfer failed: {chamfer.ErrorMessage}"); StatusText = "Part created; chamfer failed."; return; }
                AddLog($"  [OK] {chamfer.Data}");
            }

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
