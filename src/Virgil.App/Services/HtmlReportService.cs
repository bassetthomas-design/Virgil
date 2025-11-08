using System;
using System.IO;

namespace Virgil.App.Services;

public class HtmlReportService : IReportService
{
    public string WriteTodayHtml()
    {
        var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(app, "Virgil", "logs");
        Directory.CreateDirectory(dir);
        var json = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".json");
        var html = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd") + ".html");
        var lines = File.Exists(json) ? File.ReadAllLines(json) : Array.Empty<string>();
        using var sw = new StreamWriter(html, false);
        sw.WriteLine("<html><head><meta charset=\"utf-8\"><title>Virgil - Rapport</title></head><body>");
        sw.WriteLine("<h2>Rapport maintenance - " + DateTime.Now.ToString("yyyy-MM-dd") + "</h2>");
        sw.WriteLine("<pre>");
        foreach (var l in lines) sw.WriteLine(System.Net.WebUtility.HtmlEncode(l));
        sw.WriteLine("</pre></body></html>");
        return html;
    }
}
