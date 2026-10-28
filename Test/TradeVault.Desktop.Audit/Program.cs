using System.Diagnostics;
using System.IO;
using System.Text.Json;
using TradeVault.Desktop;

// Run current isolated checks instead of obsolete probes that expected already-fixed defects.
internal static class Program
{
    static int Main(string[] args)
    {
        if(args.Length!=1){Console.Error.WriteLine("Usage: TradeVault.Desktop.Audit <evidence folder>");return 2;}
        var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        var results=new List<object>();var passed=true;
        foreach(var command in new[]{new[]{"--self-test"},new[]{"--qa-report",output}})
        {
            var start=new ProcessStartInfo("dotnet"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false,CreateNoWindow=true};
            start.ArgumentList.Add(typeof(MainWindow).Assembly.Location);foreach(var argument in command)start.ArgumentList.Add(argument);
            using var process=Process.Start(start)??throw new InvalidOperationException("Unable to start desktop audit.");
            var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(120000)){process.Kill(true);Console.Error.WriteLine("Audit timed out.");return 1;}
            var text=stdout.GetAwaiter().GetResult();var error=stderr.GetAwaiter().GetResult();Console.Write(text);Console.Error.Write(error);
            results.Add(new{Check=command[0],process.ExitCode,Output=text,Error=error});passed&=process.ExitCode==0;
        }
        File.WriteAllText(Path.Combine(output,"audit-runner-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));return passed?0:1;
    }
}
