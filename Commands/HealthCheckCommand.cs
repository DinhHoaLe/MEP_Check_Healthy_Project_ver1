using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MEP_Check_Healthy_Project_ver1.UI;

namespace MEP_Check_Healthy_Project_ver1.Commands
{
    // Match Add-in Manager's Manual mode; analysis does not open any transaction.
    [Transaction(TransactionMode.Manual)]
    public sealed class HealthCheckCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var document = data.Application.ActiveUIDocument?.Document;
            if (document == null || document.IsFamilyDocument) {
                TaskDialog.Show("Revit Health Studio", "Hãy mở một Revit Project (.rvt) trước khi chạy kiểm tra.");
                return Result.Cancelled;
            }
            try {
                var window = new HealthWindow(document);
                new WindowInteropHelper(window).Owner = data.Application.MainWindowHandle;
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.ToString(); return Result.Failed; }
        }
    }
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            var panel = application.CreateRibbonPanel("MEP Health");
            var button = (PushButton)panel.AddItem(new PushButtonData("MEPHealthCheck", "Project\nHealth", typeof(App).Assembly.Location,
                typeof(HealthCheckCommand).FullName));
            button.ToolTip = "Kiểm tra sức khỏe Revit Project và xuất báo cáo JSON.";
            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}
