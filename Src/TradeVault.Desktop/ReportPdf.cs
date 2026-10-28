using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TradeVault.Application;

namespace TradeVault.Desktop;

// Render the same Unicode-aware page layout directly to PDF without a printer driver.
internal static class ReportPdf
{
    internal static void Save(ReportBundle report,string path)
    {
        var paginator=new ReportPaginator(report,new Size(1122,793));
        var pending=path+".partial";
        try
        {
            using(var file=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None))
            {
                var offsets=new List<long>{0};
                void Write(string text){var bytes=Encoding.ASCII.GetBytes(text);file.Write(bytes);}
                void Object(int id,string content){offsets.Add(file.Position);Write($"{id} 0 obj\n{content}\nendobj\n");}
                Write("%PDF-1.4\n");
                Object(1,"<< /Type /Catalog /Pages 2 0 R >>");
                Object(2,$"<< /Type /Pages /Count {paginator.PageCount} /Kids ["+string.Join(" ",Enumerable.Range(0,paginator.PageCount).Select(i=>$"{3+i*3} 0 R"))+"] >>");
                for(int i=0;i<paginator.PageCount;i++)
                {
                    var page=paginator.GetPage(i);var bitmap=new RenderTargetBitmap(2244,1586,192,192,PixelFormats.Pbgra32);bitmap.Render(page.Visual);
                    var encoder=new JpegBitmapEncoder{QualityLevel=94};encoder.Frames.Add(BitmapFrame.Create(bitmap));using var image=new MemoryStream();encoder.Save(image);
                    int id=3+i*3;
                    Object(id,$"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 841.5 594.75] /Resources << /XObject << /PageImage {id+2} 0 R >> >> /Contents {id+1} 0 R >>");
                    var commands="q 841.5 0 0 594.75 0 0 cm /PageImage Do Q\n";
                    Object(id+1,$"<< /Length {commands.Length} >>\nstream\n{commands}endstream");
                    offsets.Add(file.Position);Write($"{id+2} 0 obj\n<< /Type /XObject /Subtype /Image /Width 2244 /Height 1586 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {image.Length} >>\nstream\n");image.Position=0;image.CopyTo(file);Write("\nendstream\nendobj\n");
                }
                var xref=file.Position;Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
                foreach(var offset in offsets.Skip(1))Write(offset.ToString("D10",CultureInfo.InvariantCulture)+" 00000 n \n");
                Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");file.Flush(true);
            }
            File.Move(pending,path,true);
        }
        finally{if(File.Exists(pending))File.Delete(pending);}
    }
}
