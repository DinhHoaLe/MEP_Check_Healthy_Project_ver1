using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using MEP_Check_Healthy_Project_ver1.Models;

namespace MEP_Check_Healthy_Project_ver1.Services
{
    public static partial class HealthAnalyzer
    {
        private static List<CheckDefinition> StandardCatalog(RuleProfile p)
        {
            const string file = "File Settings Naming", naming = "Naming Convention", datum = "Project Datum",
                levels = "Levels and Grids", links = "BIM Links", general = "General Checks", warnings = "Project Warnings";
            var result = new List<CheckDefinition> {
                Standard("FILE_NAMING", file, "Tên file Revit", "Tên file .rvt phải khớp biểu thức naming trong profile.", (d,r) => {
                    if (!Configured(p,r)) return;
                    if (string.IsNullOrEmpty(d.PathName)) { r.Status = "notApplicable"; return; }
                    r.EvaluatedCount = 1;
                    string name = d.Title.EndsWith(".rvt", StringComparison.OrdinalIgnoreCase) ? d.Title : d.Title + ".rvt";
                    if (!p.Matches(r.Id,name)) r.Findings.Add(new Finding { Message = name, Elements = { new ElementInfo { Name = name, Category = "File", Type = "Revit Project (.rvt)" } } });
                }),
                Standard("WORKSET_NAMING", file, "Tên workset", "Kiểm tra user workset theo naming trong profile.", (d,r) => {
                    if (!Configured(p,r) || !d.IsWorkshared) return;
                    foreach (var w in Worksets(d)) { r.EvaluatedCount++; if (!p.Matches(r.Id,w.Name)) r.Findings.Add(WorksetFinding(w)); }
                }),
                Standard("SHARED_PARAMETER_NAMING", file, "Tên shared parameter", "Kiểm tra SharedParameterElement đã nạp trong project.", (d,r) => Named(p,r,Elements<SharedParameterElement>(d))),
                Standard("PROJECT_PARAMETER_NAMING", file, "Tên project parameter", "Kiểm tra tên parameter trong Document.ParameterBindings, gồm shared và non-shared.", (d,r) => {
                    if (!Configured(p,r)) return;
                    var iterator = d.ParameterBindings.ForwardIterator(); iterator.Reset();
                    while (iterator.MoveNext()) { string name = iterator.Key.Name; r.EvaluatedCount++;
                        if (!p.Matches(r.Id,name)) { var definition = iterator.Key as InternalDefinition; var e = definition == null ? null : d.GetElement(definition.Id);
                            r.Findings.Add(e == null ? new Finding { Message = name, Elements = { new ElementInfo { Name = name, Category = "Project parameter", Type = iterator.Current.GetType().Name } } } : Finding(d,name,new[] {e.Id})); }
                    }
                }),
                Standard("FAMILY_NAMING", naming, "Tên family", "Kiểm tra family được nạp, trừ tag family.", (d,r) => Named(p,r,Elements<Family>(d).Where(x => x.FamilyCategory == null || !x.FamilyCategory.IsTagCategory))),
                Standard("TAG_NAMING", naming, "Tên tag family", "Kiểm tra FamilyCategory.IsTagCategory theo profile.", (d,r) => Named(p,r,Elements<Family>(d).Where(x => x.FamilyCategory?.IsTagCategory == true))),
                Standard("DIMENSION_NAMING", naming, "Tên dimension style", "Kiểm tra DimensionType theo profile.", (d,r) => Named(p,r,Elements<DimensionType>(d))),
                Standard("TEXT_NAMING", naming, "Tên text style", "Kiểm tra TextNoteType theo profile.", (d,r) => Named(p,r,Elements<TextNoteType>(d))),
                Standard("FILTER_NAMING", naming, "Tên filter", "Kiểm tra parameter filter và selection filter.", (d,r) => Named(p,r,Elements<FilterElement>(d))),
                Standard("TEMPLATE_NAMING", naming, "Tên view template", "Kiểm tra view có IsTemplate = true.", (d,r) => Named(p,r,Elements<View>(d).Where(x => x.IsTemplate))),
                Standard("SCHEDULE_NAMING", naming, "Tên schedule", "Kiểm tra schedule, trừ template và titleblock revision schedule.", (d,r) => Named(p,r,Elements<ViewSchedule>(d).Where(x => !x.IsTemplate && !x.IsTitleblockRevisionSchedule))),
                Standard("FILLED_REGION_NAMING", naming, "Tên filled region", "Kiểm tra FilledRegionType theo profile.", (d,r) => Named(p,r,Elements<FilledRegionType>(d))),
                Standard("ONE_BASE_POINT", datum, "Một Project Base Point", "Project phải có đúng một Project Base Point.", (d,r) => PointCount(d,r,false)),
                Standard("ONE_SURVEY_POINT", datum, "Một Survey Point", "Project phải có đúng một Survey Point.", (d,r) => PointCount(d,r,true)),
                Standard("BASE_POINT_VALUES", datum, "Tọa độ Project Base Point", "So sánh thông số E/W, N/S, Elevation trong Properties với giá trị profile (mét).", (d,r) => BasePointValues(d,r,p)),
                Standard("BASE_POINT_ANGLE", datum, "Góc Project Base Point", "So sánh Angle to True North theo profile (độ).", (d,r) => BasePointAngle(d,r,p)),
                Standard("PINNED_LEVELS", levels, "Level được pin", "Tất cả level phải được pin.", (d,r) => Inspect(r,Elements<Level>(d),x => !x.Pinned)),
                Standard("PINNED_GRIDS", levels, "Grid được pin", "Tất cả grid phải được pin.", (d,r) => Inspect(r,Elements<Grid>(d),x => !x.Pinned)),
                Standard("DUPLICATE_LEVELS", levels, "Level trùng cao độ", "Phát hiện nhóm level có Elevation trùng trong tolerance của profile.", (d,r) => DuplicateLevels(d,r,p)),
                Standard("PINNED_LINKS", links, "Link instance được pin", "Kiểm tra Revit link và CAD link instance.", (d,r) => {
                    Inspect(r,Elements<RevitLinkInstance>(d),x => !x.Pinned); Inspect(r,Elements<ImportInstance>(d).Where(x => x.IsLinked),x => !x.Pinned);
                }),
                Standard("CAD_CLEAR", links, "Mô hình không có CAD", "Không có CAD import hoặc CAD link. Chỉ chọn nếu tiêu chuẩn/giai đoạn yêu cầu.", (d,r) => {
                    r.EvaluatedCount=1; foreach (var cad in Elements<ImportInstance>(d)) r.Findings.Add(Finding(d,cad.Name,new[] {cad.Id}));
                }),
                Standard("REQUIRED_WORKSETS", general, "Workset bắt buộc", "Danh sách requiredWorksets của profile phải có đủ trong user worksets.", (d,r) => RequiredWorksets(d,r,p)),
                Standard("DUPLICATE_GRIDS", general, "Grid trùng hình học", "So sánh đường thẳng đồng trục và cung tròn cùng tâm/bán kính (không phụ thuộc extent).", (d,r) => DuplicateGrids(d,r,p)),
                Standard("TYPE_MARK_DUPLICATES", warnings, "Type Mark trùng", "Phân tích Type Mark khác rỗng của ElementType; nhóm theo category và giá trị, không phụ thuộc ngôn ngữ warning.", DuplicateTypeMarks),
                Standard("WARNING_WALL_OVERLAP", warnings, "Warning: wall chồng lấn", "Warning WallsOverlap theo FailureDefinitionId.", (d,r) => WarningKind(d,r,BuiltInFailures.OverlapFailures.WallsOverlap,BuiltInFailures.PerformanceFailures.WallsOverlap)),
                Standard("WARNING_FLOOR_OVERLAP", warnings, "Warning: floor chồng lấn", "Warning FloorsOverlap theo FailureDefinitionId.", (d,r) => WarningKind(d,r,BuiltInFailures.OverlapFailures.FloorsOverlap)),
                Standard("WARNING_INSERT_CONFLICT", warnings, "Warning: insert xung đột joined wall", "Warning InsertJoinedWall theo FailureDefinitionId.", (d,r) => WarningKind(d,r,BuiltInFailures.InfillFailures.InsertJoinedWall)),
                Standard("WARNING_ROOM_ENCLOSURE", warnings, "Warning: room chưa bao kín", "Warning RoomNotEnclosed/RoomNotEnclosedRooms theo FailureDefinitionId.", (d,r) => WarningKind(d,r,BuiltInFailures.RoomFailures.RoomNotEnclosed,BuiltInFailures.RoomFailures.RoomNotEnclosedRooms))
            };
            return result;
        }
        private static CheckDefinition Standard(string id,string category,string title,string guidance,Action<Document,CheckResult> action)
        {
            var rule = Rule(id,category,title,guidance,action); rule.IsScored=true; return rule;
        }
        private static bool Configured(RuleProfile p, CheckResult r)
        {
            if (!string.IsNullOrWhiteSpace(p.PatternFor(r.Id))) return true;
            r.Status="notConfigured"; r.Recommendation += " Chưa có naming pattern của dự án."; return false;
        }
        private static void Named<T>(RuleProfile p,CheckResult r,IEnumerable<T> elements) where T:Element
        {
            if (Configured(p,r)) Inspect(r,elements,e => !p.Matches(r.Id,e.Name));
        }
        private static List<Workset> Worksets(Document d)
        {
            using (var collector = new FilteredWorksetCollector(d)) return collector.OfKind(WorksetKind.UserWorkset).ToWorksets().ToList();
        }
        private static Finding WorksetFinding(Workset w) => new Finding { Message=w.Name, Elements = { new ElementInfo {Id=w.Id.IntegerValue,Name=w.Name,Category="Workset",Type="UserWorkset"} } };
        private static void RequiredWorksets(Document d,CheckResult r,RuleProfile p)
        {
            if (p.RequiredWorksets.Count == 0) {r.Status="notConfigured";return;}
            if (!d.IsWorkshared) {r.EvaluatedCount=1;r.Findings.Add(new Finding {Message="Project chưa bật worksharing nhưng profile yêu cầu workset."});return;}
            var existing = new HashSet<string>(Worksets(d).Select(x => x.Name),StringComparer.OrdinalIgnoreCase);
            r.EvaluatedCount=p.RequiredWorksets.Count;
            foreach (string name in p.RequiredWorksets.Where(x => !existing.Contains(x))) r.Findings.Add(new Finding {Message="Thiếu workset: "+name,Elements={new ElementInfo{Name=name,Category="Workset",Type="Missing"}}});
        }
        private static List<BasePoint> Points(Document d,bool shared) => Elements<BasePoint>(d).Where(x => x.IsShared==shared).ToList();
        private static void PointCount(Document d,CheckResult r,bool shared)
        {
            var points=Points(d,shared);r.EvaluatedCount=1;
            if (points.Count!=1) r.Findings.Add(Finding(d,"Số base point: "+points.Count,points.Select(x => x.Id)));
        }
        private static double ParameterValue(Element e,BuiltInParameter id)
        {
            var parameter=e.get_Parameter(id);
            if(parameter==null || !parameter.HasValue) throw new InvalidOperationException("Không đọc được thông số: "+id);
            return parameter.AsDouble();
        }
        private static void BasePointValues(Document d,CheckResult r,RuleProfile p)
        {
            if(!p.BasePointEastWestMeters.HasValue || !p.BasePointNorthSouthMeters.HasValue || !p.BasePointElevationMeters.HasValue) {r.Status="notConfigured";return;}
            var points=Points(d,false);r.EvaluatedCount=1;
            if(points.Count!=1) {r.Findings.Add(Finding(d,"Cần đúng một Project Base Point trước khi so sánh tọa độ.",points.Select(x=>x.Id)));return;}
            var point=points[0];
            var actual=new[]{BuiltInParameter.BASEPOINT_EASTWEST_PARAM,BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM,BuiltInParameter.BASEPOINT_ELEVATION_PARAM}
                .Select(id=>UnitUtils.ConvertFromInternalUnits(ParameterValue(point,id),UnitTypeId.Meters)).ToArray();
            var expected=new[]{p.BasePointEastWestMeters.Value,p.BasePointNorthSouthMeters.Value,p.BasePointElevationMeters.Value};
            r.Recommendation += " Mong muốn: "+string.Join(", ",expected.Select(x=>x.ToString("0.###",CultureInfo.InvariantCulture)))+" m.";
            if(actual.Where((value,i)=>Math.Abs(value-expected[i])>p.PositionToleranceMeters).Any())
                r.Findings.Add(Finding(d,"E/W, N/S, Elevation = "+string.Join(", ",actual.Select(x=>x.ToString("0.###",CultureInfo.InvariantCulture)))+" m",new[]{point.Id}));
        }
        private static void BasePointAngle(Document d,CheckResult r,RuleProfile p)
        {
            if(!p.BasePointAngleDegrees.HasValue) {r.Status="notConfigured";return;}
            var points=Points(d,false);r.EvaluatedCount=1;
            if(points.Count!=1) {r.Findings.Add(Finding(d,"Cần đúng một Project Base Point trước khi so sánh góc.",points.Select(x=>x.Id)));return;}
            double angle=UnitUtils.ConvertFromInternalUnits(ParameterValue(points[0],BuiltInParameter.BASEPOINT_ANGLETON_PARAM),UnitTypeId.Degrees);
            double delta=Math.Abs(((angle-p.BasePointAngleDegrees.Value)%360+540)%360-180);
            if(delta>p.AngleToleranceDegrees) r.Findings.Add(Finding(d,"Angle to True North = "+angle.ToString("0.###",CultureInfo.InvariantCulture)+"°; mong muốn "+p.BasePointAngleDegrees.Value+"°",new[]{points[0].Id}));
        }
        private static void DuplicateLevels(Document d,CheckResult r,RuleProfile p)
        {
            var items=Elements<Level>(d).OrderBy(x=>x.Elevation).ToList();r.EvaluatedCount=items.Count;
            double tolerance=UnitUtils.ConvertToInternalUnits(p.PositionToleranceMeters,UnitTypeId.Meters);
            for(int i=0;i<items.Count;) {
                var group=new List<Level>{items[i++]};
                while(i<items.Count && items[i].Elevation-group[0].Elevation<=tolerance) group.Add(items[i++]);
                if(group.Count>1) r.Findings.Add(Finding(d,"Level trùng cao độ: "+string.Join(", ",group.Select(x=>x.Name)),group.Select(x=>x.Id)));
            }
        }
        private static void DuplicateGrids(Document d,CheckResult r,RuleProfile p)
        {
            var grids=Elements<Grid>(d);r.EvaluatedCount=grids.Count;
            double tolerance=UnitUtils.ConvertToInternalUnits(p.PositionToleranceMeters,UnitTypeId.Meters);
            for(int i=0;i<grids.Count;i++) for(int j=i+1;j<grids.Count;j++) {
                var a=grids[i].Curve;var b=grids[j].Curve;bool same=false;
                if(a is Line la && b is Line lb) same=Math.Abs(la.Direction.DotProduct(lb.Direction))>1-1e-10 &&
                    (lb.GetEndPoint(0)-la.GetEndPoint(0)).CrossProduct(la.Direction).GetLength()<=tolerance;
                else if(a is Arc aa && b is Arc ab) same=aa.Center.DistanceTo(ab.Center)<=tolerance && Math.Abs(aa.Radius-ab.Radius)<=tolerance && Math.Abs(aa.Normal.DotProduct(ab.Normal))>1-1e-10;
                if(same) r.Findings.Add(Finding(d,grids[i].Name+" / "+grids[j].Name,new[]{grids[i].Id,grids[j].Id}));
            }
        }
        private static void DuplicateTypeMarks(Document d,CheckResult r)
        {
            var types=Elements<ElementType>(d);r.EvaluatedCount=types.Count;
            var values=types.Select(x=>new{Element=x,Mark=x.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK)?.AsString()}).Where(x=>!string.IsNullOrWhiteSpace(x.Mark));
            foreach(var group in values.GroupBy(x=>new {Category=x.Element.Category?.Id.IntegerValue ?? 0,x.Mark}).Where(x=>x.Count()>1))
                r.Findings.Add(Finding(d,"Type Mark trùng: "+group.Key.Mark,group.Select(x=>x.Element.Id)));
        }
        private static void WarningKind(Document d,CheckResult r,params FailureDefinitionId[] ids)
        {
            r.EvaluatedCount=1;
            foreach(var warning in d.GetWarnings().Where(x=>ids.Any(id=>id.Guid==x.GetFailureDefinitionId().Guid))) {
                var finding=Finding(d,warning.GetDescriptionText(),warning.GetFailingElements().Concat(warning.GetAdditionalElements()));
                finding.FailureDefinitionId=warning.GetFailureDefinitionId().Guid.ToString();r.Findings.Add(finding);
            }
        }
    }
}
