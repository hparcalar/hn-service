using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GenCode128;
using AspNetCore.Reporting;

namespace HnService.Printing{
    public class PrintManager {
        // public bool PrintLabel(PrintDataModel model, string designPath)
        // {
        //     try
        //     {
        //         // GENERATE BARCODE IMAGE
        //         QRCodeGenerator qrGenerator = new QRCodeGenerator();
        //         QRCodeData qrCodeData = qrGenerator.CreateQrCode(model.Barcode, QRCodeGenerator.ECCLevel.Q);
        //         QRCode qrCode = new QRCode(qrCodeData);
        //         Bitmap qrCodeImage = qrCode.GetGraphic(100);

        //         ImageConverter converter = new ImageConverter();
        //         var imgBytes = (byte[])converter.ConvertTo(qrCodeImage, typeof(byte[]));

        //         List<dynamic> dataList = new List<dynamic>() {
        //             new
        //             {
        //                 Explanation = model.ModelCode,
        //                 BarcodeContent = model.Barcode,
        //                 EntryDateText = string.Format("{0:dd.MM.yyyy HH:mm}", model.ProductionDate),
        //                 BarcodeImage = imgBytes
        //             }
        //         };

        //         LocalReport report = new LocalReport();
        //         report.ReportPath = System.AppDomain.CurrentDomain.BaseDirectory + "ReportDesign\\ProdLabel.rdlc";

        //         report.DataSources.Add(new ReportDataSource("DS1", dataList));
        //         Export(report, 3.7, 1.7);
        //         Print("HekaPrinter");

        //         return true;
        //     }
        //     catch (Exception ex)
        //     {
        //         return false;
        //     }
        // }

        // #region YAZDIRMAK İÇİN GEREKLİ STANDART METOTLAR
        // private int m_currentPageIndex;
        // private IList<Stream> m_streams;

        // private Stream CreateStream(string name,
        //     string fileNameExtension, Encoding encoding,
        //     string mimeType, bool willSeek)
        // {
        //     Stream stream = new MemoryStream();
        //     m_streams.Add(stream);
        //     return stream;
        // }
        // // Export the given report as an EMF (Enhanced Metafile) file.
        // private void Export(LocalReport report, decimal pageWidth, decimal pageHeight)
        // {
        //     string deviceInfo =
        //   @"<DeviceInfo>
        //         <OutputFormat>EMF</OutputFormat>
        //         <PageWidth>{PageWidth}</PageWidth>
        //         <PageHeight>{PageHeight}</PageHeight>
        //         <MarginTop>{MarginTop}</MarginTop>
        //         <MarginLeft>{MarginLeft}</MarginLeft>
        //         <MarginRight>{MarginRight}</MarginRight>
        //         <MarginBottom>{MarginBottom}</MarginBottom>
        //      </DeviceInfo>"
        //     .Replace("{PageWidth}", string.Format("{0:N2}", pageWidth).Replace(",",".") + "cm")
        //     .Replace("{PageHeight}", string.Format("{0:N2}", pageHeight).Replace(",", ".") + "cm")
        //     .Replace("{MarginTop}", "0.0cm")
        //     .Replace("{MarginLeft}", "0.0cm")
        //     .Replace("{MarginRight}", "0.0cm")
        //     .Replace("{MarginBottom}", "0.0cm");

        //     Warning[] warnings;
        //     m_streams = new List<Stream>();
        //     report.Render("Image", deviceInfo, CreateStream,
        //        out warnings);
        //     foreach (Stream stream in m_streams)
        //         stream.Position = 0;
        // }
        // // Handler for PrintPageEvents
        // private void PrintPage(object sender, PrintPageEventArgs ev)
        // {
        //     try
        //     {
        //         Metafile pageImage = new
        //        Metafile(m_streams[m_currentPageIndex]);

        //         // Adjust rectangular area with printer margins.
        //         System.Drawing.Rectangle adjustedRect = new System.Drawing.Rectangle(
        //             ev.PageBounds.Left - (int)ev.PageSettings.HardMarginX,
        //             ev.PageBounds.Top - (int)ev.PageSettings.HardMarginY,
        //             ev.PageBounds.Width,
        //             ev.PageBounds.Height);

        //         // Draw a white background for the report
        //         ev.Graphics.FillRectangle(System.Drawing.Brushes.White, adjustedRect);

        //         // Draw the report content
        //         ev.Graphics.DrawImage(pageImage, adjustedRect);

        //         // Prepare for the next page. Make sure we haven't hit the end.
        //         m_currentPageIndex++;
        //         ev.HasMorePages = (m_currentPageIndex < m_streams.Count);
        //     }
        //     catch (Exception ex)
        //     {

        //     }
        // }

        // private void Print(string printerName)
        // {
        //     if (m_streams == null || m_streams.Count == 0)
        //         throw new Exception("Error: no stream to print.");
        //     PrintDocument printDoc = new PrintDocument();
        //     // YAZICI ADI PARAMETRİK BELİRTİLEBİLİR
        //     //printDoc.PrinterSettings.PrinterName = "Microsoft Print to PDF";
        //     if (!printDoc.PrinterSettings.IsValid)
        //     {
        //         throw new Exception("Error: cannot find the default printer.");
        //     }
        //     else
        //     {
        //         printDoc.PrintPage += new PrintPageEventHandler(PrintPage);
        //         printDoc.PrinterSettings.PrinterName = printerName;
        //         m_currentPageIndex = 0;
        //         try
        //         {
        //             printDoc.Print();
        //         }
        //         catch (Exception ex)
        //         {

        //         }
                
        //     }
        // }
        // #endregion
    }
}