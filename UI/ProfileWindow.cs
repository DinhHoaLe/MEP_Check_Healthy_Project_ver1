using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MEP_Check_Healthy_Project_ver1.Models;

namespace MEP_Check_Healthy_Project_ver1.UI
{
    public sealed class ProfileWindow : Window
    {
        private readonly TextBox editor;
        private readonly TextBlock status;
        public RuleProfile Profile { get; private set; }
        public ProfileWindow(RuleProfile profile)
        {
            Title="Tiêu chuẩn dự án";Width=900;Height=700;MinWidth=650;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=new SolidColorBrush(Color.FromRgb(245,242,238));FontFamily=new FontFamily("Segoe UI");
            var root=new DockPanel {Margin=new Thickness(22)};Content=root;
            var help=new TextBlock {Text="Cấu hình naming bằng regex cho toàn bộ tên. Pattern trống = chưa cấu hình. Tọa độ dùng mét, góc dùng độ.\nrequiredWorksets là danh sách tên workset bắt buộc. Các tolerance mặc định là 1 mm và 0.001°. Profile chỉ có hiệu lực sau khi chạy lại kiểm tra.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12)};
            DockPanel.SetDock(help,Dock.Top);root.Children.Add(help);
            var footer=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            AddButton(footer,"Nạp JSON",()=>Load());AddButton(footer,"Lưu profile",()=>Save());AddButton(footer,"Áp dụng",()=>Apply());
            status=new TextBlock {TextWrapping=TextWrapping.Wrap,Margin=new Thickness(12,0,0,0),MaxWidth=400};footer.Children.Add(status);
            editor=new TextBox {Text=profile.ToJson(),AcceptsReturn=true,AcceptsTab=true,FontFamily=new FontFamily("Consolas"),FontSize=13,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(12)};
            root.Children.Add(editor);
        }
        private static void AddButton(Panel parent,string text,Action action)
        {
            var button=new Button {Content=text,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,0)};
            button.Click+=(s,e)=>action();parent.Children.Add(button);
        }
        private void Load()
        {
            var dialog=new OpenFileDialog {Filter="Rule profile (*.json)|*.json"};if(dialog.ShowDialog(this)!=true)return;
            Try(()=>{var text=File.ReadAllText(dialog.FileName);RuleProfile.FromJson(text);editor.Text=text;status.Text="Đã nạp profile.";});
        }
        private void Save()
        {
            Try(()=>{var p=RuleProfile.FromJson(editor.Text);var dialog=new SaveFileDialog {Filter="Rule profile (*.json)|*.json",FileName="project-health-profile.json"};
                if(dialog.ShowDialog(this)==true){File.WriteAllText(dialog.FileName,p.ToJson(),new System.Text.UTF8Encoding(false));status.Text="Đã lưu profile.";}});
        }
        private void Apply() => Try(()=>{Profile=RuleProfile.FromJson(editor.Text);DialogResult=true;});
        private void Try(Action action) {try{action();}catch(Exception ex){status.Text=ex.Message;}}
    }
}
