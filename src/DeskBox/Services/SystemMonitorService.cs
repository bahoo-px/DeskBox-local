using System.Net.Http;
using System.Text.Json;

namespace DeskBox.Services;

/// <summary>
/// Per-graphics-device readings for one monitor sample.
/// </summary>
public sealed record SystemMonitorGpuSnapshot(
    string Name,
    double UtilPercent,
    double VramUsedMib,
    double VramTotalMib,
    double? TempC,
    double? PowerW);

/// <summary>
/// Per-storage-device readings for one monitor sample: sustained read/write
/// throughput (MB/s) and SMART temperature where the sensor exists.
/// </summary>
public sealed record SystemMonitorDriveSnapshot(
    string Name,
    double ReadMBs,
    double WriteMBs,
    double? TempC);

/// <summary>
/// One sample of the local model-runtime monitor surface: Strata server
/// liveness plus host GPU / CPU / memory / process / token-counter readings
/// collected by the companion dashboard process.
/// </summary>
public sealed record SystemMonitorSnapshot(
    bool StrataOnline,
    string? ModelId,
    int? ModelContext,
    IReadOnlyList<SystemMonitorGpuSnapshot> Gpus,
    double? CpuUtilPercent,
    double? CpuTempC,
    double? RamTempC,
    double MemUsedGb,
    double MemTotalGb,
    double MemPercent,
    int? ProcPid,
    double ProcMemMb,
    long? TokDay,
    long? TokTotal,
    double? TokRatePerSecond,
    double? AnswerTokPerSecond,
    bool WorkActive,
    double? CpuPowerW,
    double? GpuPowerW,
    double? TotalPowerW,
    double? EnergyDayKwh,
    double? EnergyTotalKwh,
    double? EnergyDayCost,
    double? EnergyTotalCost,
    double? ElecPricePerKwh,
    bool CpuPowerMeasured,
    double? PowerBaseW,
    IReadOnlyList<SystemMonitorDriveSnapshot> Drives);

/// <summary>
/// Loopback HTTP client for the local Strata monitor dashboard
/// (H:\Strata\monitor\dashboard.py). The base URL is resolved from the
/// STRATA_DASH_URL environment variable, else DASH_PORT, else the
/// dashboard default port 8899.
/// </summary>
public sealed class SystemMonitorService
{
    private const string DefaultBaseUrl = "http://127.0.0.1:8899";

    private static readonly HttpClient SharedClient = CreateClient();

    public string BaseUrl { get; }

    public SystemMonitorService()
    {
        BaseUrl = ResolveBaseUrl();
    }

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null
        };
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
    }

    private static string ResolveBaseUrl()
    {
        string? configured = Environment.GetEnvironmentVariable("STRATA_DASH_URL");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        string? port = Environment.GetEnvironmentVariable("DASH_PORT");
        if (int.TryParse(port, out int portValue) && portValue is > 0 and <= 65535)
        {
            return "http://127.0.0.1:" + portValue;
        }

        return DefaultBaseUrl;
    }

    public async Task<SystemMonitorSnapshot?> TrySnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SharedClient.GetAsync(BaseUrl + "/api/snap", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            return ParseSnapshot(document.RootElement);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task<(bool Accepted, string? Message)> LaunchAsync(CancellationToken cancellationToken)
    {
        return PostActionAsync("/api/launch", cancellationToken);
    }

    public Task<(bool Accepted, string? Message)> StopAsync(CancellationToken cancellationToken)
    {
        return PostActionAsync("/api/stop", cancellationToken);
    }

    private async Task<(bool Accepted, string? Message)> PostActionAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SharedClient.PostAsync(BaseUrl + path, content: null, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            string? message = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    JsonElement root = document.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (string key in new[] { "msg", "message", "detail" })
                        {
                            if (root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                            {
                                message = value.GetString();
                                break;
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                }
            }

            return (response.IsSuccessStatusCode, message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, null);
        }
    }

    private static SystemMonitorSnapshot? ParseSnapshot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        bool online = root.TryGetProperty("online", out var onlineElement) &&
                      onlineElement.ValueKind == JsonValueKind.True;

        string? modelId = null;
        int? modelContext = null;
        if (root.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object)
        {
            modelId = GetString(model, "id");
            modelContext = (int?)GetDouble(model, "context");
        }

        double? answerTokPerSecond = null;
        bool workActive = false;
        if (root.TryGetProperty("flat", out var flat) && flat.ValueKind == JsonValueKind.Object)
        {
            answerTokPerSecond = GetDouble(flat, "telemetry/rates/answer_tok_s");
            if (flat.TryGetProperty("telemetry/work/active", out var activeElement))
            {
                workActive = activeElement.ValueKind == JsonValueKind.True;
            }

            modelId ??= GetString(flat, "telemetry/model/id");
            modelContext ??= (int?)GetDouble(flat, "telemetry/model/context");
        }

        var gpus = new List<SystemMonitorGpuSnapshot>();
        double? cpuUtil = null;
        double? cpuTemp = null;
        double? ramTemp = null;
        double memUsed = 0, memTotal = 0, memPercent = 0;
        int? procPid = null;
        double procMemMb = 0;
        var drives = new List<SystemMonitorDriveSnapshot>();

        if (root.TryGetProperty("sys", out var sys) && sys.ValueKind == JsonValueKind.Object)
        {
            if (sys.TryGetProperty("gpu", out var gpuArray) && gpuArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement gpu in gpuArray.EnumerateArray())
                {
                    if (gpu.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    gpus.Add(new SystemMonitorGpuSnapshot(
                        GetString(gpu, "name") ?? string.Empty,
                        GetDouble(gpu, "util") ?? 0,
                        GetDouble(gpu, "used_mib") ?? 0,
                        GetDouble(gpu, "total_mib") ?? 0,
                        GetDouble(gpu, "temp"),
                        GetDouble(gpu, "power")));
                }
            }

            if (sys.TryGetProperty("cpu", out var cpu) && cpu.ValueKind == JsonValueKind.Object)
            {
                cpuUtil = GetDouble(cpu, "util");
            }

            if (sys.TryGetProperty("temps", out var temps) && temps.ValueKind == JsonValueKind.Object)
            {
                cpuTemp = GetDouble(temps, "cpu_c");
                ramTemp = GetDouble(temps, "ram_c");
            }

            if (sys.TryGetProperty("mem", out var mem) && mem.ValueKind == JsonValueKind.Object)
            {
                memUsed = GetDouble(mem, "used_gb") ?? 0;
                memTotal = GetDouble(mem, "total_gb") ?? 0;
                memPercent = GetDouble(mem, "pct") ?? 0;
            }

            if (sys.TryGetProperty("proc", out var proc) && proc.ValueKind == JsonValueKind.Object)
            {
                int? pid = (int?)GetDouble(proc, "pid");
                procPid = pid;
                procMemMb = GetDouble(proc, "mem_mb") ?? 0;
            }

            if (sys.TryGetProperty("drives", out var drivesArray) && drivesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement drive in drivesArray.EnumerateArray())
                {
                    if (drive.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    drives.Add(new SystemMonitorDriveSnapshot(
                        GetString(drive, "name") ?? string.Empty,
                        GetDouble(drive, "read_mbs") ?? 0,
                        GetDouble(drive, "write_mbs") ?? 0,
                        GetDouble(drive, "temp_c")));
                }
            }
        }

        long? tokDay = null;
        long? tokTotal = null;
        double? tokRate = null;
        if (root.TryGetProperty("tok", out var tok) && tok.ValueKind == JsonValueKind.Object)
        {
            tokDay = (long?)GetDouble(tok, "day_tokens");
            tokTotal = (long?)GetDouble(tok, "total_tokens");
            tokRate = GetDouble(tok, "rate_tok_s");
        }

        double? cpuPowerW = null;
        double? gpuPowerW = null;
        double? totalPowerW = null;
        double? basePowerW = null;
        bool cpuMeasured = false;
        if (root.TryGetProperty("sys", out var sysOuter) && sysOuter.ValueKind == JsonValueKind.Object &&
            sysOuter.TryGetProperty("power", out var power) && power.ValueKind == JsonValueKind.Object)
        {
            cpuPowerW = GetDouble(power, "cpu_w");
            gpuPowerW = GetDouble(power, "gpu_w");
            totalPowerW = GetDouble(power, "total_w");
            basePowerW = GetDouble(power, "base_w");
            cpuMeasured = power.TryGetProperty("cpu_est", out var cpuEstElement) &&
                          cpuEstElement.ValueKind == JsonValueKind.False;
        }

        double? energyDayKwh = null;
        double? energyTotalKwh = null;
        double? energyDayCost = null;
        double? energyTotalCost = null;
        double? elecPrice = null;
        if (root.TryGetProperty("energy", out var energy) && energy.ValueKind == JsonValueKind.Object)
        {
            energyDayKwh = GetDouble(energy, "day_kwh");
            energyTotalKwh = GetDouble(energy, "total_kwh");
            energyDayCost = GetDouble(energy, "day_cost");
            energyTotalCost = GetDouble(energy, "total_cost");
            elecPrice = GetDouble(energy, "price");
        }

        return new SystemMonitorSnapshot(
            online,
            modelId,
            modelContext,
            gpus,
            cpuUtil,
            cpuTemp,
            ramTemp,
            memUsed,
            memTotal,
            memPercent,
            procPid,
            procMemMb,
            tokDay,
            tokTotal,
            tokRate,
            answerTokPerSecond,
            workActive,
            cpuPowerW,
            gpuPowerW,
            totalPowerW,
            energyDayKwh,
            energyTotalKwh,
            energyDayCost,
            energyTotalCost,
            elecPrice,
            cpuMeasured,
            basePowerW,
            drives);
    }

    private static string? GetString(JsonElement owner, string property)
    {
        return owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static double? GetDouble(JsonElement owner, string property)
    {
        return owner.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }
}
