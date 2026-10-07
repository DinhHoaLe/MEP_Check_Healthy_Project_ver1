using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using MEP_Check_Healthy_Project_ver1.Models;
using MEP_Check_Healthy_Project_ver1.Services;
using Visibility = System.Windows.Visibility;

namespace MEP_Check_Healthy_Project_ver1.UI
{
    public partial class HealthWindow : Window
    {
        private readonly Document document;
        private List<CheckDefinition> catalog;
        private RuleProfile profile = RuleProfile.Empty();
        private HealthReport report;
        private ICollectionView resultsView;
        public HealthWindow(Document document)
        {
            this.document = document;
            catalog = HealthAnalyzer.Catalog(profile);
            InitializeComponent();
            CatalogGrid.ItemsSource = catalog;
            ProjectText.Text = document.Title + "  ·  Revit " + document.Application.VersionNumber + "  ·  Mô hình đang mở";
            Height = Math.Min(Height, SystemParameters.WorkArea.Height - 20);
            Width = Math.Min(Width, SystemParameters.WorkArea.Width - 20);
            CategoryFilter.ItemsSource = new[] { "Tất cả nhóm" };
            CategoryFilter.SelectedIndex = 0;
        }
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var rule in catalog) rule.IsSelected = SelectAll.IsChecked == true;
            CatalogGrid.Items.Refresh();
        }
        private void Run_Click(object sender, RoutedEventArgs e)
        {
            if (!catalog.Any(x => x.IsSelected)) { StatusText.Text = "Hãy chọn ít nhất một mục kiểm tra."; return; }
            RunButton.IsEnabled = false;
            System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
            try {
                // Revit API stays on the command thread inside the modal window.
                profile.Validate();
                report = HealthAnalyzer.Analyze(document, catalog, RuleProfile.FromJson(profile.ToJson()));
                if (CapturePreviewCheck.IsChecked == true) report.ModelPreview = ModelPreviewCapture.Capture(document);
                CategoryFilter.ItemsSource = new[] { "Tất cả nhóm" }.Concat(report.Groups.Select(x => x.Name)).ToList();
                CategoryFilter.SelectedIndex = 0;
                GroupCards.ItemsSource = report.Groups;
                ScoreText.Text = report.Summary.HealthPercent.HasValue ? report.Summary.HealthPercent.Value.ToString("0.#") + "%" : "—";
                ScoredCountText.Text = report.Summary.ScoredChecks + " mục tính điểm";
                PreviewButton.Visibility = report.ModelPreview?.Frames.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ResultsGrid.ItemsSource = report.Checks;
                resultsView = CollectionViewSource.GetDefaultView(ResultsGrid.ItemsSource);
                Filter_Changed(null, null);
                CatalogPanel.Visibility = Visibility.Collapsed;
                ResultsPanel.Visibility = Visibility.Visible;
                BackButton.Visibility = ExportButton.Visibility = HtmlButton.Visibility = Visibility.Visible;
                ProfileButton.Visibility = Visibility.Collapsed;
                RunButton.Visibility = Visibility.Collapsed;
                var s = report.Summary;
                SummaryText.Text = $"{s.Total} mục · {s.Passed} đạt · {s.Failed} không đạt · {s.Review} cần rà soát · {s.Errors} lỗi · {s.NotConfigured} chưa cấu hình · {s.NotApplicable} không áp dụng" +
                    (s.IsComplete ? "" : " · Kết quả chưa đầy đủ.");
                StepText.Text = "01  DANH MỤC     /     02  KẾT QUẢ     /     03  XUẤT JSON";
                StatusText.Text = "Chọn một hạng mục để xem chi tiết. JSON xuất toàn bộ kết quả của lần kiểm tra này.";
                if (report.ModelPreview?.Error != null) StatusText.Text = "Ảnh mô hình: " + report.ModelPreview.Error;
                ResultsGrid.SelectedItem = report.Checks.FirstOrDefault(x => x.Status == "error" || x.Status == "fail" || x.Status == "review") ?? report.Checks.FirstOrDefault();
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể kiểm tra", MessageBoxButton.OK, MessageBoxImage.Error); }
            finally { RunButton.IsEnabled = true; System.Windows.Input.Mouse.OverrideCursor = null; }
        }
        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (resultsView == null) return;
            string search = SearchBox.Text.Trim();
            string[] statuses = { null, "fail", "review", "pass", "error", "notApplicable", "notConfigured" };
            string status = statuses[Math.Max(0, StatusFilter.SelectedIndex)];
            string category = CategoryFilter.SelectedIndex > 0 ? CategoryFilter.SelectedItem as string : null;
            resultsView.Filter = item => {
                var check = (CheckResult)item;
                return (status == null || check.Status == status) && (category == null || check.Category == category) && (search.Length == 0 ||
                    (check.Title + " " + check.Category + " " + check.Id + " " + string.Join(" ", check.Findings.Select(f => f.Message + " " + f.ElementIdText + " " + string.Join(" ", f.Elements.Select(el => el.Name))))).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0);
            };
        }
        private void Result_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (DetailText == null) return;
            var check = ResultsGrid.SelectedItem as CheckResult;
            DetailText.Text = check == null ? "Chọn hạng mục để xem chi tiết." : check.Id + " · " + check.Recommendation +
                (check.Error == null ? "" : "\nLỗi: " + check.Error);
            FindingsGrid.ItemsSource = check?.Findings.SelectMany(f => f.Elements.Count == 0 ? new[] { new FindingRow { Message = f.Message, ElementIdText = f.ElementIdText } } :
                f.Elements.Select(el => new FindingRow { Message = f.Message, ElementIdText = el.Id?.ToString() ?? "N/A", Name = el.Name, Category = el.Category, Type = el.Type })).ToList();
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            CatalogPanel.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = BackButton.Visibility = ExportButton.Visibility = HtmlButton.Visibility = PreviewButton.Visibility = Visibility.Collapsed;
            ProfileButton.Visibility = Visibility.Visible;
            RunButton.Visibility = Visibility.Visible;
            SummaryText.Text = "Chọn danh mục và chạy lại để tạo báo cáo mới.";
            StatusText.Text = "Các mục không được chọn sẽ không xuất hiện trong báo cáo mới.";
        }
        private sealed class FindingRow
        {
            public string ElementIdText { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
            public string Type { get; set; }
            public string Message { get; set; }
        }
        private void Group_Click(object sender, RoutedEventArgs e)
        {
            CategoryFilter.SelectedItem = ((Button)sender).Tag as string;
            StatusFilter.SelectedIndex = 0;
            SearchBox.Text = "";
        }
        private void CopyIds_Click(object sender, RoutedEventArgs e)
        {
            var check = ResultsGrid.SelectedItem as CheckResult;
            var ids = check?.Findings.SelectMany(f => f.ElementIds).Distinct().ToList();
            if (ids == null || ids.Count == 0) { StatusText.Text = "Hạng mục này không có Element ID để sao chép."; return; }
            try { Clipboard.SetText(string.Join(", ", ids)); StatusText.Text = "Đã sao chép " + ids.Count + " Element ID."; }
            catch(Exception ex) { StatusText.Text = "Không thể sao chép: " + ex.Message; }
        }
        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileWindow(profile) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            var selected = catalog.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet();
            profile = dialog.Profile;
            catalog = HealthAnalyzer.Catalog(profile);
            foreach(var rule in catalog) rule.IsSelected = selected.Contains(rule.Id);
            CatalogGrid.ItemsSource = catalog;
            StatusText.Text = "Tiêu chuẩn: " + profile.Name + ". Chạy kiểm tra để áp dụng.";
        }
        private void Html_Click(object sender, RoutedEventArgs e)
        {
            if(report == null) return;
            var dialog = new SaveFileDialog { Title = "Xuất dashboard sức khỏe Revit", Filter = "HTML report (*.html)|*.html", DefaultExt = ".html", AddExtension = true,
                FileName = "revit-health-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html" };
            if(dialog.ShowDialog(this) != true) return;
            try { ReportExporter.ExportHtml(report,dialog.FileName); StatusText.Text = "Đã xuất HTML: " + dialog.FileName; }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"Không thể xuất HTML",MessageBoxButton.OK,MessageBoxImage.Error); }
        }
        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            var frames=report?.ModelPreview?.Frames;
            if(frames==null || frames.Count==0) return;
            var viewer=new Window {Title="Ảnh mô hình · 16 góc nhìn",Owner=this,Width=900,Height=650,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            var panel=new DockPanel {Margin=new Thickness(16)};viewer.Content=panel;
            var slider=new Slider {Minimum=0,Maximum=frames.Count-1,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(slider,Dock.Bottom);panel.Children.Add(slider);
            var image=new System.Windows.Controls.Image {Stretch=System.Windows.Media.Stretch.Uniform};panel.Children.Add(image);
            Action<int> show=index=>{var bitmap=new System.Windows.Media.Imaging.BitmapImage();bitmap.BeginInit();bitmap.CacheOption=System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.StreamSource=new System.IO.MemoryStream(Convert.FromBase64String(frames[index].Substring(frames[index].IndexOf(',')+1)));bitmap.EndInit();bitmap.StreamSource.Dispose();image.Source=bitmap;};
            slider.ValueChanged+=(s,args)=>show((int)slider.Value);show(0);viewer.ShowDialog();
        }
        private void Export_Click(object sender, RoutedEventArgs e)
        {
            if (report == null) return;
            string name = string.Concat(report.Project.Title.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var dialog = new SaveFileDialog { Title = "Xuất báo cáo sức khỏe Revit", Filter = "JSON report (*.json)|*.json",
                DefaultExt = ".json", AddExtension = true, FileName = name + "_health_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json" };
            if (dialog.ShowDialog(this) != true) return;
            try { ReportExporter.Export(report, dialog.FileName); StatusText.Text = "Đã xuất JSON: " + dialog.FileName; StepText.Text = "03  XUẤT JSON  ·  HOÀN TẤT"; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không thể xuất JSON", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
