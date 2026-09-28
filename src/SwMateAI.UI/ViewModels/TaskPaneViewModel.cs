using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Windows.Input;
using SwMateAI.Core.Agent;
using SwMateAI.Core.Models;
using SwMateAI.Core.Models.Understanding;
using SwMateAI.Core.Models.Assembly;
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
        private string _statusText      = "Đang kết nối…";
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
        private string _planGoal = "Chưa có kế hoạch";
        private string _lastResultText = "Sẵn sàng nhận lệnh.";
        private string _agentStageText = "Idle";
        private string _activeConfiguration = "—";
        private string _selectionSummary = "0 đã chọn";
        private bool _hasPlan;
        private bool _lastRunSucceeded;
        private string _dimensionName = string.Empty;
        private string _dimensionValue = "20";
        private string _modelSummary = "Chưa đọc model.";
        private string _uiLanguageCode = "vi-VN";
        private TaskPlan _currentPlan;

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
        public string ModelSummary { get => _modelSummary; private set { _modelSummary = value; OnPropertyChanged(); } }
        public string NaturalLanguageCommand { get => _naturalLanguageCommand; set { _naturalLanguageCommand = value; OnPropertyChanged(); } }
        public string PlanGoal { get => _planGoal; private set { _planGoal = value; OnPropertyChanged(); } }
        public string LastResultText { get => _lastResultText; private set { _lastResultText = value; OnPropertyChanged(); } }
        public string AgentStageText { get => _agentStageText; private set { _agentStageText = value; OnPropertyChanged(); OnPropertyChanged(nameof(AgentStageDisplay)); OnPropertyChanged(nameof(AgentStageColor)); } }
        public string ActiveConfiguration { get => _activeConfiguration; private set { _activeConfiguration = value; OnPropertyChanged(); } }
        public string SelectionSummary { get => _selectionSummary; private set { _selectionSummary = value; OnPropertyChanged(); } }
        public bool HasPlan { get => _hasPlan; private set { _hasPlan = value; OnPropertyChanged(); } }
        public bool LastRunSucceeded { get => _lastRunSucceeded; private set { _lastRunSucceeded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultColor)); } }
        public string AgentStageDisplay => TranslateAgentStage(AgentStageText);
        public string AgentStageColor => AgentStageText == "Failed" ? "#EF4444" : AgentStageText == "Completed" ? "#22C55E" : "#38BDF8";
        public string ResultColor => LastRunSucceeded ? "#22C55E" : "#94A3B8";

        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(); RelayCommand.RaiseCanExecuteChanged(); }
        }

        /// <summary>Log entries shown in the agent console panel.</summary>
        public ObservableCollection<string> ConsoleLog { get; } = new ObservableCollection<string>();
        public ObservableCollection<PlanStepUiItem> CurrentPlanSteps { get; } = new ObservableCollection<PlanStepUiItem>();
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
        public ICommand InspectModelCommand { get; }

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

            InspectModelCommand = new RelayCommand(
                execute:    InspectModel,
                canExecute: () => !IsRefreshing);
        }

        public void SetUiLanguage(string languageCode)
        {
            _uiLanguageCode = string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase) ? "en-US" : "vi-VN";
            OnPropertyChanged(nameof(AgentStageDisplay));
            RefreshPlanDisplay();
            SelectionSummary = Tr($"{_agentCore.ObserveContext().SelectedObjectCount} đã chọn", $"{_agentCore.ObserveContext().SelectedObjectCount} selected");
            StatusText = IsConnected ? $"SOLIDWORKS {SwVersion}" : Tr("Chưa kết nối", "Not connected");

            if (_currentPlan == null)
                LastResultText = Tr("Sẵn sàng nhận lệnh.", "Ready for a command.");
            else if (LastRunSucceeded)
                LastResultText = Tr($"Hoàn thành và đã kiểm tra {_currentPlan.Steps.Count}/{_currentPlan.Steps.Count} skill.", $"Completed and verified {_currentPlan.Steps.Count}/{_currentPlan.Steps.Count} skill(s).");
        }

        private bool IsVietnamese => _uiLanguageCode != "en-US";
        private string Tr(string vi, string en) => IsVietnamese ? vi : en;

        private string TranslateAgentStage(string stage)
        {
            switch (stage)
            {
                case "Planning": return Tr("Đang lập kế hoạch", "Planning");
                case "Executing": return Tr("Đang thực hiện", "Executing");
                case "Completed": return Tr("Hoàn thành", "Completed");
                case "Failed": return Tr("Thất bại", "Failed");
                default: return Tr("Sẵn sàng", "Idle");
            }
        }

        private string TranslatePlanGoal(string goal)
        {
            if (goal == "Create requested CAD part") return Tr("Tạo chi tiết CAD theo yêu cầu", goal);
            if (goal == "Modify requested CAD dimension") return Tr("Chỉnh sửa kích thước CAD theo yêu cầu", goal);
            if (goal == "Read requested CAD model data") return Tr("Đọc dữ liệu model theo yêu cầu", goal);
            if (goal == "Analyze feature change impact") return Tr("Phân tích ảnh hưởng khi thay đổi Feature", goal);
            return goal;
        }

        private string TranslateStepDescription(PlanStep step)
        {
            if (!IsVietnamese) return step.Description;
            switch (step.Description)
            {
                case "Create base plate geometry": return "Tạo hình học tấm cơ sở";
                case "Fillet four vertical plate edges": return "Bo 4 cạnh đứng của tấm";
                case "Chamfer four vertical plate edges": return "Vát 4 cạnh đứng của tấm";
                case "Read data from the active SOLIDWORKS model": return "Đọc dữ liệu từ model SOLIDWORKS đang mở";
            }
            if (step.Description.StartsWith("Find downstream dependencies of ", StringComparison.OrdinalIgnoreCase))
                return "Tìm các Feature phía sau phụ thuộc vào " + step.Description.Substring("Find downstream dependencies of ".Length);
            if (step.Description.StartsWith("Set ", StringComparison.OrdinalIgnoreCase))
                return "Đặt " + step.Description.Substring(4).Replace(" to ", " thành ");
            return step.Description;
        }

        private string TranslatePlanStatus(PlanStepStatus status)
        {
            if (!IsVietnamese) return status.ToString();
            switch (status)
            {
                case PlanStepStatus.Running: return "Đang chạy";
                case PlanStepStatus.Completed: return "Hoàn thành";
                case PlanStepStatus.Failed: return "Thất bại";
                case PlanStepStatus.Skipped: return "Bỏ qua";
                default: return "Chờ thực hiện";
            }
        }

        private void RefreshPlanDisplay()
        {
            CurrentPlanSteps.Clear();
            if (_currentPlan == null) return;
            PlanGoal = TranslatePlanGoal(_currentPlan.Goal);
            foreach (var step in _currentPlan.Steps)
                CurrentPlanSteps.Add(new PlanStepUiItem { Index = step.Index, SkillName = step.SkillName, Description = TranslateStepDescription(step), Status = TranslatePlanStatus(step.Status), IsVerified = step.IsVerified });
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
                    StatusText = Tr("Lỗi khi đọc thông tin model.", "Error retrieving model info.");
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
                StatusText    = IsConnected ? $"SOLIDWORKS {SwVersion}" : Tr("Chưa kết nối", "Not connected");

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
                SelectionSummary = Tr($"{context.SelectedObjectCount} đã chọn", $"{context.SelectedObjectCount} selected");
                AddLog($"  Active Config : {ActiveConfiguration}");
                AddLog($"  Selection     : {context.SelectedObjectCount} object(s)");
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = Tr("Lỗi không mong muốn.", "Unexpected error.");
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
                    StatusText = Tr("Không thể tạo Part.", "Failed to create Part.");
                    return;
                }

                AddLog($"  [OK] {result.Data}");
                StatusText = Tr("Đã tạo Part mới.", "New Part created.");
                RefreshInfo();
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = Tr("Lỗi không mong muốn khi tạo Part.", "Unexpected error creating Part.");
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
                    StatusText = Tr("Không thể tạo Sketch.", "Failed to create Sketch.");
                    return;
                }

                AddLog($"  [OK] {result.Data}");
                StatusText = Tr("Đã tạo Sketch mới.", "New Sketch created.");
                RefreshInfo();
            }
            catch (Exception ex)
            {
                AddLog($"  [EXCEPTION] {ex.Message}");
                StatusText = Tr("Lỗi không mong muốn khi tạo Sketch.", "Unexpected error creating Sketch.");
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
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể tạo hình chữ nhật.", "Failed to create Rectangle."); return; }
                AddLog($"  [OK] {result.Data}");
                StatusText = Tr("Đã tạo hình chữ nhật.", "Rectangle created.");
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi khi tạo hình chữ nhật.", "Unexpected rectangle error."); }
        }

        private void Extrude()
        {
            AddLog($"> Extrude called ({ExtrudeDepth} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("Extrude", new Dictionary<string, object> { ["Depth"] = ExtrudeDepth });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể Extrude.", "Failed to Extrude."); return; }
                AddLog($"  [OK] {result.Data}");
                StatusText = Tr("Đã tạo Boss-Extrude.", "Boss-Extrude created.");
                RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi Extrude không mong muốn.", "Unexpected extrude error."); }
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
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể tạo đường tròn.", "Failed to create Circle."); return; }
                AddLog($"  [OK] {result.Data}"); StatusText = Tr("Đã tạo đường tròn.", "Circle created.");
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi khi tạo đường tròn.", "Unexpected circle error."); }
        }

        private void CutExtrude()
        {
            AddLog($"> CutExtrude called ({CutDepth} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("CutExtrude", new Dictionary<string, object> { ["Depth"] = CutDepth });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể Cut-Extrude.", "Failed to Cut-Extrude."); return; }
                AddLog($"  [OK] {result.Data}"); StatusText = Tr("Đã tạo Cut-Extrude.", "Cut-Extrude created."); RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi Cut-Extrude không mong muốn.", "Unexpected cut error."); }
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
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Quy trình tự động thất bại.", "Auto workflow failed."); return; }
                AddLog($"  [OK] {result.Data}"); StatusText = Tr("Đã tự động tạo tấm và lỗ.", "Plate with hole created automatically."); RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi quy trình tự động.", "Unexpected auto workflow error."); }
        }

        private void AddDimension()
        {
            AddLog($"> AddDimension called ({DimensionValue} mm)");
            try
            {
                var result = _agentCore.ExecuteTool("AddDimension", new Dictionary<string, object> { ["Value"] = DimensionValue });
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể thêm kích thước.", "Failed to add dimension."); return; }
                AddLog($"  [OK] {result.Data}"); StatusText = Tr("Đã thêm kích thước.", "Dimension added."); RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi kích thước không mong muốn.", "Unexpected dimension error."); }
        }

        private void ModifyDimension()
        {
            AddLog($"> ModifyDimension called ({DimensionName}, {DimensionValue} mm)");
            try
            {
                var args = new Dictionary<string, object> { ["Value"] = DimensionValue };
                if (!string.IsNullOrWhiteSpace(DimensionName)) args["Name"] = DimensionName;
                var result = _agentCore.ExecuteTool("ModifyDimension", args);
                if (!result.IsSuccess) { AddLog($"  [ERR] {result.ErrorMessage}"); StatusText = Tr("Không thể sửa kích thước.", "Failed to modify dimension."); return; }
                AddLog($"  [OK] {result.Data}"); StatusText = Tr("Đã sửa kích thước.", "Dimension modified."); RefreshInfo();
            }
            catch (Exception ex) { AddLog($"  [EXCEPTION] {ex.Message}"); StatusText = Tr("Lỗi kích thước không mong muốn.", "Unexpected dimension error."); }
        }

        private void InspectModel()
        {
            try
            {
                if (string.Equals(DocumentType, "Assembly", StringComparison.OrdinalIgnoreCase))
                {
                    var assembly = _agentCore.ExecuteTool("ReadAssembly").Data as AssemblyInfo;
                    var components = _agentCore.ExecuteTool("ReadComponents").Data as List<AssemblyComponentInfo> ?? new List<AssemblyComponentInfo>();
                    var mates = _agentCore.ExecuteTool("ReadMates").Data as List<AssemblyMateInfo> ?? new List<AssemblyMateInfo>();
                    ModelSummary = assembly == null ? Tr("Không thể đọc Assembly.", "Could not read Assembly.") : Tr(
                        $"Assembly: {assembly.Name}\nComponent: {components.Count}   Mate: {mates.Count}   Suppressed: {assembly.SuppressedComponentCount}\nConfiguration: {assembly.Configuration}   Lightweight: {assembly.LightweightComponentCount}",
                        $"Assembly: {assembly.Name}\nComponents: {components.Count}   Mates: {mates.Count}   Suppressed: {assembly.SuppressedComponentCount}\nConfiguration: {assembly.Configuration}   Lightweight: {assembly.LightweightComponentCount}");
                    StatusText = Tr("Đã đọc Assembly thành công.", "Assembly inspection completed.");
                    AddLog($"  [ASSEMBLY] {components.Count} components, {mates.Count} mates");
                    return;
                }
                var features = _agentCore.ExecuteTool("ReadFeatureTree").Data as List<FeatureInfo> ?? new List<FeatureInfo>();
                var sketches = _agentCore.ExecuteTool("ReadSketches").Data as List<FeatureInfo> ?? new List<FeatureInfo>();
                var dimensions = _agentCore.ExecuteTool("ReadDimensions").Data as List<DimensionInfo> ?? new List<DimensionInfo>();
                string material = _agentCore.ExecuteTool("ReadMaterial").Data as string ?? string.Empty;
                var mass = _agentCore.ExecuteTool("ReadMassProperties").Data as MassPropertiesInfo;
                var box = _agentCore.ExecuteTool("ReadBoundingBox").Data as BoundingBoxInfo;
                var selected = _agentCore.ExecuteTool("ReadSelectedObject").Data as List<SelectedObjectInfo> ?? new List<SelectedObjectInfo>();
                var dependencies = _agentCore.ExecuteTool("ReadFeatureDependencies").Data as List<FeatureDependencyInfo> ?? new List<FeatureDependencyInfo>();

                ModelSummary = Tr(
                    $"Feature: {features.Count}   Sketch: {sketches.Count}   Kích thước: {dimensions.Count}\n" +
                    $"Vật liệu: {(string.IsNullOrWhiteSpace(material) ? "<chưa chỉ định>" : material)}\n" +
                    $"Khối lượng: {(mass == null ? "?" : mass.MassKg.ToString("0.###") + " kg")}   Kích thước tổng thể: {(box == null ? "?" : box.ToString())}\n" +
                    $"Đang chọn: {selected.Count} đối tượng   Quan hệ feature: {dependencies.Sum(x => x.Children.Count)}",
                    $"Features: {features.Count}   Sketches: {sketches.Count}   Dimensions: {dimensions.Count}\n" +
                    $"Material: {(string.IsNullOrWhiteSpace(material) ? "<not specified>" : material)}\n" +
                    $"Mass: {(mass == null ? "?" : mass.MassKg.ToString("0.###") + " kg")}   Size: {(box == null ? "?" : box.ToString())}\n" +
                    $"Selection: {selected.Count} object(s)   Feature relations: {dependencies.Sum(x => x.Children.Count)}");
                StatusText = Tr("Đã đọc model thành công.", "Model inspection completed.");
                AddLog($"  [MODEL] {features.Count} features, {dimensions.Count} dimensions, size={(box == null ? "?" : box.ToString())}");
            }
            catch (Exception ex)
            {
                ModelSummary = Tr($"Đọc model thất bại: {ex.Message}", $"Model inspection failed: {ex.Message}");
                AddLog($"  [MODEL ERR] {ex.Message}");
            }
        }

        private void ExecuteNaturalLanguage()
        {
            AddLog($"> COMMAND: {NaturalLanguageCommand}");
            if (!NaturalLanguageCadParser.TryParse(NaturalLanguageCommand, out var command, out var parseError))
            {
                AddLog($"  [PARSE ERR] {parseError}");
                StatusText = Tr("Không hiểu được lệnh CAD.", "Could not understand CAD command.");
                return;
            }

            if (IsReadIntent(command.Intent))
            {
                AddLog($"  [PARSED] Read-model intent: {command.Intent}");
            }
            else if (command.Intent == "ModifyDimension")
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
            _currentPlan = plan;
            HasPlan = true;
            RefreshPlanDisplay();
            AgentStageText = "Planning";
            LastResultText = Tr("Kế hoạch đã sẵn sàng. Đang thực hiện các skill...", "Plan ready. Executing skills...");
            LastRunSucceeded = false;
            AddLog($"  [PLAN] {plan.Goal}");
            foreach (var step in plan.Steps) AddLog($"    {step.Index}. {step.SkillName} - {step.Description}");

            _agentCore.State.CurrentRequest = NaturalLanguageCommand;
            AgentStageText = "Executing";
            var execution = _agentCore.ExecutePlan(plan);
            if (!execution.IsSuccess)
            {
                RefreshPlanDisplay();
                AgentStageText = "Failed";
                LastResultText = execution.Error;
                LastRunSucceeded = false;
                CommandHistory.Insert(0, $"FAIL • {NaturalLanguageCommand}");
                AddLog($"  [AGENT ERR] {execution.Error}");
                StatusText = Tr("Kế hoạch Agent thất bại.", "Agent plan failed.");
                return;
            }
            RefreshPlanDisplay();
            AgentStageText = "Completed";
            LastRunSucceeded = true;
            LastResultText = IsReadIntent(command.Intent)
                ? FormatModelQueryResult(command.Intent, execution.LastData)
                : Tr($"Hoàn thành và đã kiểm tra {execution.CompletedSteps}/{plan.Steps.Count} skill.", $"Completed and verified {execution.CompletedSteps}/{plan.Steps.Count} skill(s).");
            CommandHistory.Insert(0, $"OK • {NaturalLanguageCommand}");
            while (CommandHistory.Count > 20) CommandHistory.RemoveAt(CommandHistory.Count - 1);
            AddLog($"  [CHECK] Completed {execution.CompletedSteps}/{plan.Steps.Count} step(s). Model verification passed.");

            StatusText = Tr("Lệnh CAD đã hoàn thành.", "CAD command completed.");
            RefreshInfo();
        }

        private static bool IsReadIntent(string intent)
        {
            return intent == "ReadFeatureTree" || intent == "ReadFeatures" || intent == "ReadFeatureDependencies" || intent == "AnalyzeFeatureImpact" || intent == "ReadSketches" ||
                   intent == "ReadDimensions" || intent == "ReadMaterial" || intent == "ReadMassProperties" ||
                   intent == "ReadCustomProperties" || intent == "ReadSelectedObject" || intent == "ReadBoundingBox" ||
                   intent == "ReadAssembly" || intent == "ReadComponents" || intent == "ReadMates" || intent == "CheckInterference";
        }

        private string FormatModelQueryResult(string intent, object data)
        {
            if (intent == "ReadAssembly" && data is AssemblyInfo assembly)
                return Tr($"Assembly: {assembly.Name}\nComponent: {assembly.TotalComponentCount} (top-level {assembly.TopLevelComponentCount})\nMate: {assembly.MateCount}\nConfiguration: {assembly.Configuration}\nSuppressed: {assembly.SuppressedComponentCount}   Lightweight: {assembly.LightweightComponentCount}",
                          $"Assembly: {assembly.Name}\nComponents: {assembly.TotalComponentCount} (top-level {assembly.TopLevelComponentCount})\nMates: {assembly.MateCount}\nConfiguration: {assembly.Configuration}\nSuppressed: {assembly.SuppressedComponentCount}   Lightweight: {assembly.LightweightComponentCount}");
            if (intent == "ReadComponents" && data is List<AssemblyComponentInfo> components)
            {
                string list = string.Join("\n", components.Take(18).Select(c => $"• {new string('·', Math.Min(c.Depth, 8))} {c.Name} | {c.ReferencedConfiguration} | {c.SuppressionStateName}"));
                return Tr($"Assembly có {components.Count} component occurrence:\n{list}", $"Assembly has {components.Count} component occurrence(s):\n{list}");
            }
            if (intent == "ReadMates" && data is List<AssemblyMateInfo> mates)
            {
                string list = string.Join("\n", mates.Take(18).Select(m => $"• {m.Name} [{m.TypeName}] → {string.Join(", ", m.Components)}"));
                return Tr($"Assembly có {mates.Count} Mate:\n{list}", $"Assembly has {mates.Count} mate(s):\n{list}");
            }
            if (intent == "CheckInterference" && data is AssemblyInterferenceResult interference)
            {
                if (interference.Count == 0) return Tr("Không phát hiện va chạm vật lý trong Assembly.", "No physical interference detected in the Assembly.");
                string list = string.Join("\n", interference.Items.Take(12).Select(i => $"• #{i.Index}: {string.Join(" ↔ ", i.Components)} | {i.VolumeMm3:0.###} mm³"));
                return Tr($"Phát hiện {interference.Count} vùng va chạm:\n{list}", $"Detected {interference.Count} interference(s):\n{list}");
            }
            if (intent == "ReadMaterial")
            {
                var value = data as string;
                return Tr("Vật liệu: " + (string.IsNullOrWhiteSpace(value) ? "chưa được chỉ định" : value),
                          "Material: " + (string.IsNullOrWhiteSpace(value) ? "not specified" : value));
            }
            if (intent == "ReadBoundingBox" && data is BoundingBoxInfo box)
                return Tr("Kích thước tổng thể: " + box, "Overall size: " + box);
            if (intent == "ReadMassProperties" && data is MassPropertiesInfo mass)
                return Tr($"Khối lượng: {mass.MassKg:0.###} kg\nThể tích: {mass.VolumeMm3:0.###} mm³\nDiện tích bề mặt: {mass.SurfaceAreaMm2:0.###} mm²",
                          $"Mass: {mass.MassKg:0.###} kg\nVolume: {mass.VolumeMm3:0.###} mm³\nSurface area: {mass.SurfaceAreaMm2:0.###} mm²");
            if ((intent == "ReadFeatures" || intent == "ReadFeatureTree" || intent == "ReadSketches") && data is List<FeatureInfo> features)
            {
                string list = string.Join(", ", features.Take(12).Select(f => f.Name + " [" + f.TypeName + "]"));
                if (features.Count > 12) list += Tr($" … và {features.Count - 12} mục khác", $" … and {features.Count - 12} more");
                return Tr($"Tìm thấy {features.Count} mục: {list}", $"Found {features.Count} item(s): {list}");
            }
            if (intent == "ReadFeatureDependencies" && data is List<FeatureDependencyInfo> dependencies)
            {
                var linked = dependencies.Where(x => x.Parents.Count > 0 || x.Children.Count > 0).Take(16).ToList();
                string list = string.Join("\n", linked.Select(x =>
                    $"• {x.Name} [{x.TypeName}]  ← {(x.Parents.Count == 0 ? "—" : string.Join(", ", x.Parents))}  → {(x.Children.Count == 0 ? "—" : string.Join(", ", x.Children))}"));
                int relations = dependencies.Sum(x => x.Children.Count);
                return Tr($"Đồ thị phụ thuộc: {dependencies.Count} feature, {relations} quan hệ trực tiếp:\n{list}",
                          $"Dependency graph: {dependencies.Count} features, {relations} direct relation(s):\n{list}");
            }
            if (intent == "AnalyzeFeatureImpact" && data is FeatureImpactInfo impact)
            {
                string direct = impact.DirectChildren.Count == 0 ? "—" : string.Join(", ", impact.DirectChildren);
                string all = impact.AffectedFeatures.Count == 0 ? "—" : string.Join(", ", impact.AffectedFeatures);
                return Tr(
                    $"Feature phân tích: {impact.TargetFeature} [{impact.TargetTypeName}]\nPhụ thuộc trực tiếp phía sau: {direct}\nCó thể bị ảnh hưởng khi thay đổi ({impact.AffectedFeatures.Count}): {all}",
                    $"Analyzed feature: {impact.TargetFeature} [{impact.TargetTypeName}]\nDirect downstream dependencies: {direct}\nPotentially affected by a change ({impact.AffectedFeatures.Count}): {all}");
            }
            if (intent == "ReadDimensions" && data is List<DimensionInfo> dimensions)
            {
                string list = string.Join("\n", dimensions.Take(12).Select(d => $"• {d.FullName}: {d.ValueMm:0.###} mm"));
                return Tr($"Có {dimensions.Count} kích thước:\n{list}", $"{dimensions.Count} dimension(s):\n{list}");
            }
            if (intent == "ReadCustomProperties" && data is List<CustomPropertyInfo> props)
            {
                string list = string.Join("\n", props.Take(12).Select(x => $"• {x.Name}: {x.ResolvedValue}"));
                return Tr($"Có {props.Count} thuộc tính tùy chỉnh:\n{list}", $"{props.Count} custom propertie(s):\n{list}");
            }
            if (intent == "ReadSelectedObject" && data is List<SelectedObjectInfo> selected)
            {
                if (selected.Count == 0) return Tr("Hiện không có đối tượng nào được chọn.", "No object is currently selected.");
                string list = string.Join("\n", selected.Select(x => $"• {x.TypeName}: {x.Name}"));
                return Tr($"Đang chọn {selected.Count} đối tượng:\n{list}", $"{selected.Count} selected object(s):\n{list}");
            }
            return Tr("Đã đọc dữ liệu model thành công.", "Model data read successfully.");
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
