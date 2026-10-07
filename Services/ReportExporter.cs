using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using MEP_Check_Healthy_Project_ver1.Models;

namespace MEP_Check_Healthy_Project_ver1.Services
{
    public static class ReportExporter
    {
        public static string ToJson(HealthReport report)
        {
            // Serialize completely before opening the destination.
            byte[] data;
            using (var buffer = new MemoryStream())
            {
                using (var writer = JsonReaderWriterFactory.CreateJsonWriter(buffer, Encoding.UTF8, false, true, "  "))
                    new DataContractJsonSerializer(typeof(HealthReport)).WriteObject(writer, report);
                data = buffer.ToArray();
            }
            return Encoding.UTF8.GetString(data);
        }
        public static void Export(HealthReport report, string path) => File.WriteAllText(path, ToJson(report), new UTF8Encoding(false));
        public static void ExportHtml(HealthReport report, string path)
        {
            string template;
            using (var stream = typeof(ReportExporter).Assembly.GetManifestResourceStream("HealthDashboard.html"))
            using (var reader = new StreamReader(stream, Encoding.UTF8)) template = reader.ReadToEnd();
            File.WriteAllText(path, BuildHtml(report, template), new UTF8Encoding(false));
        }
        public static string BuildHtml(HealthReport report, string template)
        {
            // Prevent model text from terminating the inert JSON script element.
            string json = ToJson(report).Replace("&", "\\u0026").Replace("<", "\\u003c").Replace(">", "\\u003e")
                .Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
            return template.Replace("__REPORT_JSON__", json);
        }
    }
}
