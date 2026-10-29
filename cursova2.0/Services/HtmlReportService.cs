using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MediaVault.Models;

namespace MediaVault.Services
{
    public class HtmlReportService
    {
        public string GenerateReport(List<MediaItem> items, string outputPath)
        {
            var sb = new StringBuilder();
            string generatedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string machine = Environment.MachineName;
            string user = Environment.UserName;

            int totalFiles = items.Count;
            long totalBytes = items.Sum(x => x.FileSize);
            string totalSize = FormatSize(totalBytes);

            var byType = items.GroupBy(x => x.Type).OrderBy(g => g.Key).ToList();
            int missing = items.Count(x => !File.Exists(x.FilePath));
            int identified = items.Count(x => x.IsIdentified && !string.IsNullOrEmpty(x.PosterUrl));

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='uk'>");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset='UTF-8'>");
            sb.AppendLine("<title>MediaVault — інвентарний звіт</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(GetCss());
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            sb.AppendLine("<button class='print-btn no-print' onclick='window.print()'>🖨 Друкувати / PDF</button>");

            sb.AppendLine("<header class='sheet-header'>");
            sb.AppendLine("<div>");
            sb.AppendLine("<h1>Інвентарний звіт колекції</h1>");
            sb.AppendLine("<p class='meta'>MediaVault · облік медіафайлів для системного адміністратора</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='meta-box'>");
            sb.AppendLine($"<div><b>Дата:</b> {generatedAt}</div>");
            sb.AppendLine($"<div><b>ПК:</b> {Escape(machine)}</div>");
            sb.AppendLine($"<div><b>Користувач:</b> {Escape(user)}</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</header>");

            sb.AppendLine("<h2>1. Зведення</h2>");
            sb.AppendLine("<table class='summary'>");
            sb.AppendLine("<tr><th>Показник</th><th>Значення</th></tr>");
            sb.AppendLine($"<tr><td>Усього файлів у каталозі</td><td>{totalFiles}</td></tr>");
            sb.AppendLine($"<tr><td>Сумарний обсяг</td><td>{totalSize}</td></tr>");
            sb.AppendLine($"<tr><td>Файлів з метаданими / обкладинкою</td><td>{identified}</td></tr>");
            sb.AppendLine($"<tr><td>Відсутні на диску</td><td>{missing}</td></tr>");
            sb.AppendLine($"<tr><td>Улюблені</td><td>{items.Count(x => x.IsFavorite)}</td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("<h2>2. Розподіл за категоріями</h2>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Категорія</th><th>Що входить</th><th>К-сть</th><th>Обсяг</th></tr></thead><tbody>");
            foreach (var g in byType)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{Escape(TypeName(g.Key))}</td>");
                sb.AppendLine($"<td class='hint'>{Escape(TypeHint(g.Key))}</td>");
                sb.AppendLine($"<td>{g.Count()}</td>");
                sb.AppendLine($"<td>{FormatSize(g.Sum(x => x.FileSize))}</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<h2>3. Інвентарний список</h2>");
            sb.AppendLine("<table class='inventory'>");
            sb.AppendLine("<thead><tr>");
            sb.AppendLine("<th>№</th><th>Категорія</th><th>Назва</th><th>Автор</th><th>Рік</th><th>Розмір</th><th>Шлях</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int n = 1;
            foreach (var item in items.OrderBy(x => x.Type).ThenBy(x => x.Title))
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{n++}</td>");
                sb.AppendLine($"<td>{Escape(TypeName(item.Type))}</td>");
                sb.AppendLine($"<td class='title'>{Escape(item.Title)}</td>");
                sb.AppendLine($"<td>{Escape(item.Author ?? "—")}</td>");
                sb.AppendLine($"<td>{item.Year?.ToString() ?? "—"}</td>");
                sb.AppendLine($"<td>{item.FormattedSize}</td>");
                sb.AppendLine($"<td class='path'>{Escape(item.FilePath)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");

            sb.AppendLine("<footer>");
            sb.AppendLine("<p>Документ сформовано автоматично. Для друку використайте Ctrl+P або кнопку «Друкувати / PDF».</p>");
            sb.AppendLine("<div class='sign'>Підпис відповідального: ______________________ &nbsp;&nbsp; Дата: __________</div>");
            sb.AppendLine("</footer>");

            sb.AppendLine("</body></html>");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            return outputPath;
        }

        private static string TypeName(MediaType type) => type switch
        {
            MediaType.Film => "Фільми (30+ хв)",
            MediaType.ShortVideo => "Короткі відео",
            MediaType.Book => "Книги",
            MediaType.Music => "Музика",
            MediaType.Image => "Зображення",
            MediaType.Document => "Документи",
            MediaType.Program => "Програми",
            _ => "Інше"
        };

        private static string TypeHint(MediaType type) => type switch
        {
            MediaType.Film => "відео від 30 хв",
            MediaType.ShortVideo => "відео до 30 хв",
            MediaType.Book => "pdf, epub, fb2, mobi, djvu",
            MediaType.Music => "mp3, flac, wav, aac, ogg",
            MediaType.Image => "jpg, png, gif, webp, bmp",
            MediaType.Document => "doc, xls, ppt, txt, csv",
            MediaType.Program => "exe, msi, bat, ps1",
            _ => "—"
        };

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static string GetCss() => @"
@page { size: A4 portrait; margin: 14mm 12mm; }
* { box-sizing: border-box; }
body { font-family: 'Segoe UI', Calibri, Arial, sans-serif; color: #111; font-size: 11pt; line-height: 1.35; margin: 0; padding: 16px; }
h1 { font-size: 18pt; margin: 0 0 4px; }
h2 { font-size: 13pt; margin: 18px 0 8px; border-bottom: 1px solid #333; padding-bottom: 4px; }
.meta { color: #444; margin: 0; font-size: 10pt; }
.sheet-header { display: flex; justify-content: space-between; gap: 16px; margin-bottom: 16px; }
.meta-box { font-size: 10pt; border: 1px solid #999; padding: 8px 12px; min-width: 220px; }
table { width: 100%; border-collapse: collapse; font-size: 9.5pt; }
th, td { border: 1px solid #bbb; padding: 5px 7px; vertical-align: top; }
th { background: #eee; text-align: left; }
.summary th { width: 55%; }
.hint { color: #444; }
.title { font-weight: 600; }
.path { font-size: 8pt; word-break: break-all; }
footer { margin-top: 20px; font-size: 9pt; color: #333; }
.sign { margin-top: 24px; }
.print-btn { position: fixed; right: 16px; top: 16px; padding: 10px 16px; background: #1DB954; color: #fff; border: 0; border-radius: 8px; cursor: pointer; font-weight: 600; }
@media print {
  body { padding: 0; }
  .no-print, .print-btn { display: none !important; }
  thead { display: table-header-group; }
  tr { page-break-inside: avoid; }
}
";
    }
}
