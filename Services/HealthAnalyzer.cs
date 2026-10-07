using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using MEP_Check_Healthy_Project_ver1.Models;

namespace MEP_Check_Healthy_Project_ver1.Services
{
    public sealed class CheckDefinition
    {
        public string Id { get; set; }
        public string Category { get; set; }
        public string Title { get; set; }
        public string Recommendation { get; set; }
        public string Severity { get; set; } = "warning";
        public bool IsSelected { get; set; } = true;
        public bool IsScored { get; set; }
        internal Action<Document, CheckResult> Evaluate { get; set; }
    }
    public static partial class HealthAnalyzer
    {
        public static List<CheckDefinition> Catalog(RuleProfile profile = null)
        {
            var catalog = StandardCatalog(profile ?? RuleProfile.Empty());
            catalog.AddRange(new List<CheckDefinition> {
                Rule("REVIT_WARNINGS", "Mô hình", "Cảnh báo Revit", "Rà soát từng warning trong Manage → Review Warnings.", Warnings),
                Rule("CAD_IMPORTS", "Liên kết", "CAD import nhúng", "Cân nhắc thay CAD import bằng link; xác nhận trước khi xóa.", (d,r) => Inspect(r, Elements<ImportInstance>(d), e => !e.IsLinked)),
                Rule("CAD_IN_VIEWS", "Liên kết", "CAD chỉ hiển thị trong view", "Rà soát CAD phụ thuộc view và nhu cầu giữ lại.", (d,r) => Inspect(r, Elements<ImportInstance>(d), e => e.ViewSpecific), "info"),
                Rule("REVIT_LINKS", "Liên kết", "Revit link chưa được tải", "Kiểm tra đường dẫn, quyền truy cập và các link chủ động unload.", (d,r) => Inspect(r, Elements<RevitLinkType>(d), e => e.GetLinkedFileStatus() != LinkedFileStatus.Loaded)),
                Rule("IN_PLACE", "Family", "Family in-place", "Rà soát khả năng thay bằng loadable family để dễ quản lý.", (d,r) => Inspect(r, Elements<Family>(d), e => e.IsInPlace)),
                Rule("UNUSED_TYPES", "Family", "Family type chưa có instance", "Đây là ứng viên rà soát, không đồng nghĩa có thể Purge an toàn.", UnusedTypes, "info"),
                Rule("VIEWS_NO_TEMPLATE", "Tài liệu", "View chưa gán template", "Cân nhắc view template theo tiêu chuẩn dự án; có thể có ngoại lệ hợp lệ.", ViewsWithoutTemplate, "info"),
                Rule("VIEWS_OFF_SHEETS", "Tài liệu", "View chưa đặt lên sheet", "Rà soát view làm việc và view phát hành; schedule và legend được loại trừ.", ViewsOffSheets, "info"),
                Rule("EMPTY_SHEETS", "Tài liệu", "Sheet trống", "Xác nhận sheet dự phòng; kiểm tra viewport và schedule trước khi xóa.", EmptySheets, "info"),
                Rule("ROOMS", "Không gian", "Room chưa đặt / chưa bao kín", "Kiểm tra vị trí, phase và room-bounding của room có diện tích bằng 0.", (d,r) => Inspect(r, Elements<Autodesk.Revit.DB.Architecture.Room>(d), e => e.Location == null || e.Area <= 0)),
                Rule("SPACES", "Không gian", "Space chưa đặt / chưa bao kín", "Kiểm tra vị trí, phase và room-bounding của space có diện tích bằng 0.", (d,r) => Inspect(r, Elements<Space>(d), e => e.Location == null || e.Area <= 0)),
                Rule("MEP_CONNECTIONS", "MEP", "Hệ duct / pipe chưa kết nối tốt", "Rà soát kết nối hệ thống duct và pipe bằng System Browser.", Systems),
                Rule("MEP_UNASSIGNED", "MEP", "Duct / pipe chưa gán hệ thống", "Kiểm tra các đoạn duct/pipe có MEPSystem trống; không bao gồm fabrication.", Unassigned)
            });
            return catalog;
        }
        private static CheckDefinition Rule(string id, string category, string title, string recommendation,
            Action<Document, CheckResult> evaluate, string severity = "warning") => new CheckDefinition {
                Id = id, Category = category, Title = title, Recommendation = recommendation, Evaluate = evaluate, Severity = severity };
        public static HealthReport Analyze(Document document, IEnumerable<CheckDefinition> checks, RuleProfile profile = null)
        {
            var report = new HealthReport {
                GeneratedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                RuleProfile = profile,
                Project = new Models.ProjectInfo { Title = document.Title, RevitVersion = document.Application.VersionNumber,
                    IsWorkshared = document.IsWorkshared, IsSaved = !string.IsNullOrEmpty(document.PathName),
                    ProjectName = document.ProjectInformation.Name, ProjectNumber = document.ProjectInformation.Number }
            };
            foreach (var check in checks.Where(x => x.IsSelected))
            {
                var result = new CheckResult { Id = check.Id, Category = check.Category, Title = check.Title,
                    Recommendation = check.Recommendation, Severity = check.Severity, IsScored = check.IsScored };
                try {
                    check.Evaluate(document, result);
                    result.Count = result.Findings.Count;
                    if (result.Status == null) result.Status = result.EvaluatedCount == 0 ? "notApplicable" : result.Count == 0 ? "pass" : check.IsScored ? "fail" : "review";
                }
                catch (Exception ex) { result.Status = "error"; result.Error = ex.Message; result.Count = result.Findings.Count; }
                report.Checks.Add(result);
            }
            report.UpdateSummary();
            return report;
        }
        private static List<T> Elements<T>(Document d) where T : Element
        {
            // Room and Space are unsupported by ElementClassFilter in Revit 2023.
            Type filterClass = typeof(SpatialElement).IsAssignableFrom(typeof(T)) ? typeof(SpatialElement) : typeof(T);
            using (var collector = new FilteredElementCollector(d))
                return collector.OfClass(filterClass).OfType<T>().ToList();
        }
        private static Finding Finding(Document d, string message, IEnumerable<ElementId> ids)
        {
            var list = ids.Distinct().ToList();
            return new Finding { Message = message, ElementIds = list.Select(x => x.IntegerValue).ToList(),
                UniqueIds = list.Select(d.GetElement).Where(x => x != null).Select(x => x.UniqueId).ToList(),
                Elements = list.Select(d.GetElement).Where(x => x != null).Select(e => new ElementInfo {
                    Id = e.Id.IntegerValue, UniqueId = e.UniqueId, Name = e.Name,
                    Category = e is Family family ? family.FamilyCategory?.Name ?? "Family" : e.Category?.Name ?? e.GetType().Name,
                    Type = e is Family familyElement ? (familyElement.FamilyCategory?.CategoryType == CategoryType.Annotation ? "2D Family" : "3D Family") : d.GetElement(e.GetTypeId())?.Name ?? e.GetType().Name
                }).ToList() };
        }
        private static void Inspect<T>(CheckResult r, IEnumerable<T> elements, Func<T,bool> predicate) where T : Element
        {
            foreach (var e in elements) {
                r.EvaluatedCount++;
                if (predicate(e)) r.Findings.Add(Finding(e.Document, (e.Category?.Name ?? e.GetType().Name) + " · " + e.Name, new[] { e.Id }));
            }
        }
        private static void Warnings(Document d, CheckResult r)
        {
            r.EvaluatedCount = 1; // Document-level check: zero warnings is a pass.
            foreach (var w in d.GetWarnings()) {
                var finding = Finding(d, w.GetDescriptionText(), w.GetFailingElements().Concat(w.GetAdditionalElements()));
                finding.FailureDefinitionId = w.GetFailureDefinitionId().Guid.ToString();
                r.Findings.Add(finding);
            }
        }
        private static void UnusedTypes(Document d, CheckResult r)
        {
            var used = new HashSet<int>(Elements<FamilyInstance>(d).Select(x => x.GetTypeId().IntegerValue));
            Inspect(r, Elements<FamilySymbol>(d), x => !used.Contains(x.Id.IntegerValue));
        }
        private static bool DocumentationView(View v) => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.CeilingPlan ||
            v.ViewType == ViewType.EngineeringPlan || v.ViewType == ViewType.Section || v.ViewType == ViewType.Elevation ||
            v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.Detail || v.ViewType == ViewType.DraftingView || v.ViewType == ViewType.AreaPlan);
        private static void ViewsWithoutTemplate(Document d, CheckResult r) => Inspect(r, Elements<View>(d).Where(DocumentationView), x => x.ViewTemplateId == ElementId.InvalidElementId);
        private static void ViewsOffSheets(Document d, CheckResult r)
        {
            var placed = new HashSet<int>(Elements<Viewport>(d).Select(x => x.ViewId.IntegerValue));
            Inspect(r, Elements<View>(d).Where(DocumentationView), x => !placed.Contains(x.Id.IntegerValue));
        }
        private static void EmptySheets(Document d, CheckResult r)
        {
            var schedules = new HashSet<int>(Elements<ScheduleSheetInstance>(d).Select(x => x.OwnerViewId.IntegerValue));
            Inspect(r, Elements<ViewSheet>(d).Where(x => !x.IsPlaceholder), x => x.GetAllViewports().Count == 0 && !schedules.Contains(x.Id.IntegerValue));
        }
        private static void Systems(Document d, CheckResult r)
        {
            Inspect(r, Elements<MechanicalSystem>(d), x => !x.IsWellConnected);
            Inspect(r, Elements<PipingSystem>(d), x => !x.IsWellConnected);
        }
        private static void Unassigned(Document d, CheckResult r)
        {
            Inspect(r, Elements<Duct>(d), x => x.MEPSystem == null);
            Inspect(r, Elements<FlexDuct>(d), x => x.MEPSystem == null);
            Inspect(r, Elements<Pipe>(d), x => x.MEPSystem == null);
            Inspect(r, Elements<FlexPipe>(d), x => x.MEPSystem == null);
        }
    }
}
