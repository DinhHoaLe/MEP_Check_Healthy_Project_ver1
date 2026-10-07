using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using MEP_Check_Healthy_Project_ver1.Models;

namespace MEP_Check_Healthy_Project_ver1.Services
{
    public static class ModelPreviewCapture
    {
        public static ModelPreview Capture(Document document)
        {
            var result = new ModelPreview();
            string directory = Path.Combine(Path.GetTempPath(), "MEPHealthPreview_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try {
                var source = document.ActiveView as View3D;
                if (source == null || source.IsTemplate || source.IsPerspective) throw new InvalidOperationException("Mở một view 3D isometric trước khi chụp ảnh xoay.");
                XYZ min=null,max=null;
                using (var collector = new FilteredElementCollector(document,source.Id)) {
                    foreach (var element in collector.WhereElementIsNotElementType()) {
                        if(element.Category?.CategoryType!=CategoryType.Model || element is BasePoint) continue;
                        var bounds=element.get_BoundingBox(source); if(bounds==null) continue;
                        for(int x=0;x<2;x++) for(int y=0;y<2;y++) for(int z=0;z<2;z++) {
                            var point=bounds.Transform.OfPoint(new XYZ(x==0?bounds.Min.X:bounds.Max.X,y==0?bounds.Min.Y:bounds.Max.Y,z==0?bounds.Min.Z:bounds.Max.Z));
                            min=min==null?point:new XYZ(Math.Min(min.X,point.X),Math.Min(min.Y,point.Y),Math.Min(min.Z,point.Z));
                            max=max==null?point:new XYZ(Math.Max(max.X,point.X),Math.Max(max.Y,point.Y),Math.Max(max.Z,point.Z));
                        }
                    }
                }
                if(min==null || max==null) throw new InvalidOperationException("View 3D không có model geometry để chụp.");
                var center=(min+max)*0.5;
                double radius=Math.Max(min.DistanceTo(max),10);
                // Temporary view and orientation transactions are always rolled back.
                using (var group=new TransactionGroup(document,"Health preview (temporary)")) {
                    group.Start();
                    try {
                        View3D view;
                        using(var tx=new Transaction(document,"Temporary preview view")) {
                            tx.Start();
                            view=source.CanViewBeDuplicated(ViewDuplicateOption.Duplicate)
                                ? (View3D)document.GetElement(source.Duplicate(ViewDuplicateOption.Duplicate))
                                : View3D.CreateIsometric(document,source.GetTypeId());
                            view.Name="MEP Health Preview "+Guid.NewGuid().ToString("N");
                            view.ViewTemplateId=ElementId.InvalidElementId;
                            if(view.IsLocked) view.Unlock();
                            view.DetailLevel=source.DetailLevel;view.DisplayStyle=source.DisplayStyle;
                            if(source.IsSectionBoxActive) {view.SetSectionBox(source.GetSectionBox());view.IsSectionBoxActive=true;}
                            tx.Commit();
                        }
                        for(int frame=0;frame<16;frame++) {
                            double angle=2*Math.PI*frame/16;
                            var forward=new XYZ(-Math.Cos(angle),-Math.Sin(angle),-0.65).Normalize();
                            var up=(XYZ.BasisZ-forward*XYZ.BasisZ.DotProduct(forward)).Normalize();
                            using(var tx=new Transaction(document,"Preview orientation")) {
                                tx.Start();view.SetOrientation(new ViewOrientation3D(center-forward*radius*2,up,forward));document.Regenerate();tx.Commit();
                            }
                            string prefix=Path.Combine(directory,"frame"+frame.ToString("00"));
                            using(var options=new ImageExportOptions {
                                FilePath=prefix,ExportRange=ExportRange.SetOfViews,ZoomType=ZoomFitType.FitToPage,
                                FitDirection=FitDirectionType.Horizontal,PixelSize=800,HLRandWFViewsFileType=ImageFileType.PNG,
                                ShadowViewsFileType=ImageFileType.PNG,ImageResolution=ImageResolution.DPI_72
                            }) {
                                options.SetViewsAndSheets(new List<ElementId>{view.Id});document.ExportImage(options);
                            }
                            string file=Directory.GetFiles(directory,"frame"+frame.ToString("00")+"*.png").Single();
                            result.Frames.Add("data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(file)));
                        }
                    }
                    finally {if(group.GetStatus()==TransactionStatus.Started) group.RollBack();}
                }
            }
            catch(Exception ex) {result.Frames.Clear();result.Error=ex.Message;}
            finally {
                try {foreach(string file in Directory.GetFiles(directory)) File.Delete(file);Directory.Delete(directory);}
                catch(Exception ex) {result.Error=(result.Error == null ? "" : result.Error+"; ")+"Dọn ảnh tạm: "+ex.Message;}
            }
            return result;
        }
    }
}
