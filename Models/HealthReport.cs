using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace MEP_Check_Healthy_Project_ver1.Models
{
    [DataContract]
    public sealed class HealthReport
    {
        [DataMember(Name = "schemaVersion", Order = 0)] public string SchemaVersion { get; set; } = "1.1";
        [DataMember(Name = "toolVersion", Order = 1)] public string ToolVersion { get; set; } = "1.1.1";
        [DataMember(Name = "generatedAtUtc", Order = 2)] public string GeneratedAtUtc { get; set; }
        [DataMember(Name = "project", Order = 3)] public ProjectInfo Project { get; set; }
        [DataMember(Name = "summary", Order = 4)] public ReportSummary Summary { get; set; }
        [DataMember(Name = "checks", Order = 5)] public List<CheckResult> Checks { get; set; } = new List<CheckResult>();
        [DataMember(Name = "groups", Order = 6)] public List<GroupSummary> Groups { get; set; } = new List<GroupSummary>();
        [DataMember(Name = "ruleProfile", Order = 7)] public RuleProfile RuleProfile { get; set; }
        [DataMember(Name = "modelPreview", Order = 8, EmitDefaultValue = false)] public ModelPreview ModelPreview { get; set; }
        public void UpdateSummary()
        {
            Summary = new ReportSummary {
                Total = Checks.Count, Passed = Checks.Count(x => x.Status == "pass"),
                Review = Checks.Count(x => x.Status == "review"), Errors = Checks.Count(x => x.Status == "error"),
                NotApplicable = Checks.Count(x => x.Status == "notApplicable"),
                Failed = Checks.Count(x => x.Status == "fail"), NotConfigured = Checks.Count(x => x.Status == "notConfigured"),
                HealthPercent = Score(Checks), ScoredChecks = Checks.Count(x => x.IsScored && (x.Status == "pass" || x.Status == "fail")),
                IsComplete = !Checks.Any(x => x.Status == "error" || x.Status == "notConfigured")
            };
            Groups = Checks.GroupBy(x => x.Category ?? "General").Select(g => new GroupSummary {
                Name = g.Key, Total = g.Count(), Passed = g.Count(x => x.Status == "pass"), Failed = g.Count(x => x.Status == "fail"),
                Review = g.Count(x => x.Status == "review"), Errors = g.Count(x => x.Status == "error"),
                NotApplicable = g.Count(x => x.Status == "notApplicable"), NotConfigured = g.Count(x => x.Status == "notConfigured"),
                HealthPercent = Score(g)
            }).ToList();
        }
        private static double? Score(IEnumerable<CheckResult> checks)
        {
            var assessed = checks.Where(x => x.IsScored && (x.Status == "pass" || x.Status == "fail")).ToList();
            return assessed.Count == 0 ? (double?)null : Math.Round(100.0 * assessed.Count(x => x.Status == "pass") / assessed.Count, 1);
        }
    }
    [DataContract]
    public sealed class ProjectInfo
    {
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "revitVersion")] public string RevitVersion { get; set; }
        [DataMember(Name = "isWorkshared")] public bool IsWorkshared { get; set; }
        [DataMember(Name = "isSaved")] public bool IsSaved { get; set; }
        [DataMember(Name = "projectNumber")] public string ProjectNumber { get; set; }
        [DataMember(Name = "projectName")] public string ProjectName { get; set; }
    }
    [DataContract]
    public sealed class ReportSummary
    {
        [DataMember(Name = "total")] public int Total { get; set; }
        [DataMember(Name = "passed")] public int Passed { get; set; }
        [DataMember(Name = "review")] public int Review { get; set; }
        [DataMember(Name = "errors")] public int Errors { get; set; }
        [DataMember(Name = "notApplicable")] public int NotApplicable { get; set; }
        [DataMember(Name = "failed")] public int Failed { get; set; }
        [DataMember(Name = "notConfigured")] public int NotConfigured { get; set; }
        [DataMember(Name = "healthPercent")] public double? HealthPercent { get; set; }
        [DataMember(Name = "scoredChecks")] public int ScoredChecks { get; set; }
        [DataMember(Name = "isComplete")] public bool IsComplete { get; set; }
    }
    [DataContract]
    public sealed class GroupSummary
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "total")] public int Total { get; set; }
        [DataMember(Name = "passed")] public int Passed { get; set; }
        [DataMember(Name = "failed")] public int Failed { get; set; }
        [DataMember(Name = "review")] public int Review { get; set; }
        [DataMember(Name = "errors")] public int Errors { get; set; }
        [DataMember(Name = "notApplicable")] public int NotApplicable { get; set; }
        [DataMember(Name = "notConfigured")] public int NotConfigured { get; set; }
        [DataMember(Name = "healthPercent")] public double? HealthPercent { get; set; }
        public string ScoreLabel => HealthPercent.HasValue ? HealthPercent.Value.ToString("0.#") + "%" : "—";
        public double BarValue => HealthPercent ?? 0;
        public string CountsLabel => $"{Total} mục · {Passed} đạt · {Failed} không đạt · {NotConfigured} chưa cấu hình · {Errors} lỗi";
    }
    [DataContract]
    public sealed class ModelPreview
    {
        [DataMember(Name = "frames")] public List<string> Frames { get; set; } = new List<string>();
        [DataMember(Name = "error", EmitDefaultValue = false)] public string Error { get; set; }
    }
    [DataContract]
    public sealed class CheckResult
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "category")] public string Category { get; set; }
        [DataMember(Name = "title")] public string Title { get; set; }
        [DataMember(Name = "status")] public string Status { get; set; }
        [DataMember(Name = "severity")] public string Severity { get; set; }
        [DataMember(Name = "isScored")] public bool IsScored { get; set; }
        [DataMember(Name = "count")] public int Count { get; set; }
        [DataMember(Name = "evaluatedCount")] public int EvaluatedCount { get; set; }
        [DataMember(Name = "recommendation")] public string Recommendation { get; set; }
        [DataMember(Name = "error", EmitDefaultValue = false)] public string Error { get; set; }
        [DataMember(Name = "findings")] public List<Finding> Findings { get; set; } = new List<Finding>();
        public string StatusLabel => Status == "pass" ? "Đạt" : Status == "fail" ? "Không đạt" : Status == "review" ? "Cần rà soát" : Status == "error" ? "Lỗi kiểm tra" : Status == "notConfigured" ? "Chưa cấu hình" : "Không áp dụng";
    }
    [DataContract]
    public sealed class Finding
    {
        [DataMember(Name = "message")] public string Message { get; set; }
        [DataMember(Name = "elementIds")] public List<int> ElementIds { get; set; } = new List<int>();
        [DataMember(Name = "uniqueIds")] public List<string> UniqueIds { get; set; } = new List<string>();
        [DataMember(Name = "elements")] public List<ElementInfo> Elements { get; set; } = new List<ElementInfo>();
        [DataMember(Name = "failureDefinitionId", EmitDefaultValue = false)] public string FailureDefinitionId { get; set; }
        public string ElementIdText => string.Join(", ", ElementIds);
    }
    [DataContract]
    public sealed class ElementInfo
    {
        [DataMember(Name = "id")] public int? Id { get; set; }
        [DataMember(Name = "uniqueId", EmitDefaultValue = false)] public string UniqueId { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "category")] public string Category { get; set; }
        [DataMember(Name = "type")] public string Type { get; set; }
    }
}
