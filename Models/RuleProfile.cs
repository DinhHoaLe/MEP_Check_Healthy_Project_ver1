using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace MEP_Check_Healthy_Project_ver1.Models
{
    [DataContract]
    public sealed class NamingRule
    {
        [DataMember(Name = "id")] public string Id { get; set; }
        [DataMember(Name = "pattern")] public string Pattern { get; set; }
    }
    [DataContract]
    public sealed class RuleProfile
    {
        [DataMember(Name = "name")] public string Name { get; set; } = "Tiêu chuẩn dự án — chưa cấu hình";
        [DataMember(Name = "namingRules")] public List<NamingRule> NamingRules { get; set; } = new List<NamingRule>();
        [DataMember(Name = "requiredWorksets")] public List<string> RequiredWorksets { get; set; } = new List<string>();
        [DataMember(Name = "basePointEastWestMeters")] public double? BasePointEastWestMeters { get; set; }
        [DataMember(Name = "basePointNorthSouthMeters")] public double? BasePointNorthSouthMeters { get; set; }
        [DataMember(Name = "basePointElevationMeters")] public double? BasePointElevationMeters { get; set; }
        [DataMember(Name = "basePointAngleDegrees")] public double? BasePointAngleDegrees { get; set; }
        [DataMember(Name = "positionToleranceMeters")] public double PositionToleranceMeters { get; set; } = 0.001;
        [DataMember(Name = "angleToleranceDegrees")] public double AngleToleranceDegrees { get; set; } = 0.001;
        public static readonly string[] NamingIds = { "FILE_NAMING", "WORKSET_NAMING", "SHARED_PARAMETER_NAMING", "PROJECT_PARAMETER_NAMING",
            "FAMILY_NAMING", "TAG_NAMING", "DIMENSION_NAMING", "TEXT_NAMING", "FILTER_NAMING", "TEMPLATE_NAMING", "SCHEDULE_NAMING", "FILLED_REGION_NAMING" };
        public static RuleProfile Empty() => new RuleProfile { NamingRules = NamingIds.Select(id => new NamingRule { Id = id, Pattern = "" }).ToList() };
        public string PatternFor(string id) => NamingRules.FirstOrDefault(x => x.Id == id)?.Pattern;
        public bool Matches(string id, string value)
        {
            string pattern = PatternFor(id);
            // Always require a full-name match, even if user omits anchors.
            return Regex.IsMatch(value ?? "", "\\A(?:" + pattern + ")\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
        }
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name) || NamingRules == null || RequiredWorksets == null) throw new InvalidDataException("Profile cần name, namingRules và requiredWorksets.");
            if (NamingRules.Any(x => x == null || !NamingIds.Contains(x.Id)) || NamingRules.GroupBy(x => x.Id).Any(x => x.Count() > 1))
                throw new InvalidDataException("Mã naming rule không hợp lệ hoặc bị trùng.");
            foreach (var rule in NamingRules.Where(x => !string.IsNullOrWhiteSpace(x.Pattern)))
                new Regex("\\A(?:" + rule.Pattern + ")\\z", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
            foreach (double? number in new[] { BasePointEastWestMeters, BasePointNorthSouthMeters, BasePointElevationMeters, BasePointAngleDegrees, PositionToleranceMeters, AngleToleranceDegrees })
                if (number.HasValue && (double.IsNaN(number.Value) || double.IsInfinity(number.Value))) throw new InvalidDataException("Giá trị tọa độ/tolerance phải là số hữu hạn.");
            if (PositionToleranceMeters <= 0 || AngleToleranceDegrees <= 0) throw new InvalidDataException("Tolerance phải lớn hơn 0.");
            if (RequiredWorksets.Any(string.IsNullOrWhiteSpace) || RequiredWorksets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != RequiredWorksets.Count)
                throw new InvalidDataException("Tên workset bắt buộc không được trống hoặc trùng.");
        }
        public string ToJson()
        {
            using (var buffer = new MemoryStream()) {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(buffer, Encoding.UTF8, false, true, "  "))
                    new DataContractJsonSerializer(typeof(RuleProfile)).WriteObject(writer, this);
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }
        public static RuleProfile FromJson(string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json))) {
                var profile = (RuleProfile)new DataContractJsonSerializer(typeof(RuleProfile)).ReadObject(stream);
                if (profile == null) throw new InvalidDataException("Profile không được null.");
                profile.Validate();
                return profile;
            }
        }
    }
}
