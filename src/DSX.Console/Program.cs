using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DSX.Core.Constants;
using DSX.Core.Models;

namespace DSX.Console;

class Program
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    static async Task<int> Main(string[] args)
    {
        try
        {
            var config = ParseArguments(args);

            if (config.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            if (config.ListenMode)
            {
                return await ListenForResponses(config.Port, config.OutputFile);
            }

            var json = ReadInput(config.InputFile);
            if (string.IsNullOrWhiteSpace(json))
            {
                WriteError("No input provided. Use --input <file> or pipe JSON via stdin.");
                return 1;
            }

            if (!ValidateJson(json))
            {
                WriteError("Invalid JSON format. Expected DSXPacket format: {\"Instructions\":[{\"type\":0,\"parameters\":[...]}]}");
                return 1;
            }

            var formatted = FormatPacket(json);
            if (formatted == null)
            {
                WriteError("Failed to parse DSXPacket from input.");
                return 1;
            }

            WriteStatus($"Sending to {config.Host}:{config.Port}...");
            var success = SendUDP(formatted, config.Host, config.Port);

            if (success)
            {
                WriteSuccess("Packet sent successfully.");

                if (!string.IsNullOrEmpty(config.OutputFile))
                {
                    WriteOutput(formatted, config.OutputFile);
                    WriteSuccess($"Output written to: {config.OutputFile}");
                }
                else
                {
                    System.Console.WriteLine(formatted);
                }
            }
            else
            {
                WriteError("Failed to send packet.");
                return 2;
            }

            return 0;
        }
        catch (FileNotFoundException ex)
        {
            WriteError($"File not found: {ex.FileName ?? ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteError($"Access denied: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            WriteError($"Unexpected error: {ex.Message}");
            return 3;
        }
    }

    static ConsoleConfig ParseArguments(string[] args)
    {
        var config = new ConsoleConfig();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--input" or "-i":
                    if (i + 1 < args.Length)
                        config.InputFile = args[++i];
                    break;
                case "--output" or "-o":
                    if (i + 1 < args.Length)
                        config.OutputFile = args[++i];
                    break;
                case "--port" or "-p":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int port))
                        config.Port = port;
                    break;
                case "--host" or "-h" when (i + 1 < args.Length && args[i + 1] != "elp"):
                    config.Host = args[++i];
                    break;
                case "--listen" or "-l":
                    config.ListenMode = true;
                    break;
                case "--help":
                    config.ShowHelp = true;
                    break;
            }
        }

        return config;
    }

    static string ReadInput(string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(null, filePath);

            return File.ReadAllText(filePath);
        }

        if (!System.Console.IsInputRedirected)
        {
            WriteError("No input file specified and stdin is not redirected.");
            WriteStatus("Usage: dsx-console --input commands.json");
            WriteStatus("   or: echo '{\"Instructions\":[...]}' | dsx-console");
            return string.Empty;
        }

        var sb = new StringBuilder();
        string? line;
        while ((line = System.Console.ReadLine()) != null)
        {
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    static bool SendUDP(string json, string host, int port)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(json);
            using var client = new UdpClient();
            client.Client.SendTimeout = 5000;
            client.Client.ReceiveTimeout = 5000;
            client.Send(data, data.Length, host, port);
            return true;
        }
        catch (SocketException ex)
        {
            WriteError($"Socket error: {ex.SocketErrorCode} - {ex.Message}");
            return false;
        }
    }

    static async Task<int> ListenForResponses(int port, string? outputFile)
    {
        WriteStatus($"Listening for UDP packets on port {port}...");
        WriteStatus("Press Ctrl+C to stop.");
        System.Console.WriteLine();

        using var cts = new CancellationTokenSource();
        System.Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            WriteStatus("Stopping listener...");
        };

        using var udpClient = new UdpClient(port);

        try
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await udpClient.ReceiveAsync().ConfigureAwait(false);
                var json = Encoding.UTF8.GetString(result.Buffer, 0, result.Buffer.Length);
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

                if (ValidateJson(json))
                {
                    var packet = JsonSerializer.Deserialize<DSXPacket>(json, s_jsonOptions);
                    var instructionCount = packet?.Instructions.Length ?? 0;

                    WriteColored($"[{timestamp}] ", ConsoleColor.DarkGray);
                    WriteColored($"Received from {result.RemoteEndPoint}", ConsoleColor.Cyan);
                    System.Console.WriteLine($" ({instructionCount} instruction(s))");

                    var formatted = JsonSerializer.Serialize(packet, s_jsonOptions);

                    if (!string.IsNullOrEmpty(outputFile))
                    {
                        WriteOutput(formatted, outputFile);
                        WriteStatus($"  -> Appended to: {outputFile}");
                    }
                    else
                    {
                        System.Console.WriteLine(formatted);
                    }

                    System.Console.WriteLine();
                }
                else
                {
                    WriteColored($"[{timestamp}] ", ConsoleColor.DarkGray);
                    WriteColored($"Received non-DSX data from {result.RemoteEndPoint}", ConsoleColor.Yellow);
                    System.Console.WriteLine();
                    System.Console.WriteLine(json);
                    System.Console.WriteLine();
                }
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted
            || ex.SocketErrorCode == SocketError.Interrupted)
        {
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            udpClient.Close();
        }

        WriteSuccess("Listener stopped.");
        return 0;
    }

    static void WriteOutput(string result, string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.AppendAllText(filePath, result + Environment.NewLine);
        }
        catch (Exception ex)
        {
            WriteError($"Failed to write output file: {ex.Message}");
        }
    }

    static void ShowHelp()
    {
        WriteColored("DSX.Console", ConsoleColor.Cyan);
        WriteColored(" - DualSenseX Companion CLI", ConsoleColor.DarkGray);
        System.Console.WriteLine();
        System.Console.WriteLine();
        WriteColored("USAGE:", ConsoleColor.Yellow);
        System.Console.WriteLine("  dsx-console [OPTIONS]");
        System.Console.WriteLine();
        WriteColored("OPTIONS:", ConsoleColor.Yellow);
        System.Console.WriteLine("  -i, --input <file>    Read JSON commands from file");
        System.Console.WriteLine("  -o, --output <file>   Write result/output to file");
        System.Console.WriteLine("  -p, --port <number>   UDP port (default: 6969)");
        System.Console.WriteLine("  -h, --host <address>  Target host (default: 127.0.0.1)");
        System.Console.WriteLine("  -l, --listen          Start listen mode for responses");
        System.Console.WriteLine("      --help             Show this help message");
        System.Console.WriteLine();
        WriteColored("EXAMPLES:", ConsoleColor.Yellow);
        System.Console.WriteLine("  dsx-console --input triggers.json");
        System.Console.WriteLine("  echo '{\"Instructions\":[{\"type\":0,\"parameters\":[2,1,0,0,0,0,0]}]}' | dsx-console");
        System.Console.WriteLine("  dsx-console -i commands.json -o result.json");
        System.Console.WriteLine("  dsx-console --listen --port 6969 --output log.json");
        System.Console.WriteLine();
        WriteColored("INSTRUCTION TYPES:", ConsoleColor.Yellow);
        System.Console.WriteLine("  0  = Trigger L2/R2 Adaptive Trigger");
        System.Console.WriteLine("  1  = Right Trigger");
        System.Console.WriteLine("  2  = Left Trigger");
        System.Console.WriteLine("  3  = Player LED");
        System.Console.WriteLine("  4  = Lightbar (RGB)");
        System.Console.WriteLine("  5  = Lightbar (Pulse)");
        System.Console.WriteLine("  6  = Lightbar (Rainbow)");
        System.Console.WriteLine("  7  = Vibration");
        System.Console.WriteLine("  8  = Custom Lightbar (mic LED)");
        System.Console.WriteLine("  9  = Custom Lightbar (pad)");
        System.Console.WriteLine("  10 = Custom Trigger Effect");
        System.Console.WriteLine();
        WriteColored("INPUT FORMAT:", ConsoleColor.Yellow);
        System.Console.WriteLine("  {\"Instructions\":[{\"type\":0,\"parameters\":[2,1,0,0,0,0,0]}]}");
        System.Console.WriteLine();
        WriteColored("EXIT CODES:", ConsoleColor.Yellow);
        System.Console.WriteLine("  0  Success");
        System.Console.WriteLine("  1  Input/validation error");
        System.Console.WriteLine("  2  Send error");
        System.Console.WriteLine("  3  Unexpected error");
    }

    static bool ValidateJson(string json)
    {
        try
        {
            var packet = JsonSerializer.Deserialize<DSXPacket>(json, s_jsonOptions);
            return packet != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static string? FormatPacket(string json)
    {
        try
        {
            var packet = JsonSerializer.Deserialize<DSXPacket>(json, s_jsonOptions);
            if (packet == null)
                return null;

            return JsonSerializer.Serialize(packet, s_jsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static void WriteError(string message)
    {
        WriteColored("  ERROR: ", ConsoleColor.Red);
        System.Console.WriteLine(message);
    }

    static void WriteSuccess(string message)
    {
        WriteColored("  OK: ", ConsoleColor.Green);
        System.Console.WriteLine(message);
    }

    static void WriteStatus(string message)
    {
        WriteColored("  >> ", ConsoleColor.DarkCyan);
        System.Console.WriteLine(message);
    }

    static void WriteColored(string text, ConsoleColor color)
    {
        var prev = System.Console.ForegroundColor;
        System.Console.ForegroundColor = color;
        System.Console.Write(text);
        System.Console.ForegroundColor = prev;
    }
}

class ConsoleConfig
{
    public string? InputFile { get; set; }
    public string? OutputFile { get; set; }
    public int Port { get; set; } = AppConstants.DefaultUDPPort;
    public string Host { get; set; } = "127.0.0.1";
    public bool ListenMode { get; set; }
    public bool ShowHelp { get; set; }
}
