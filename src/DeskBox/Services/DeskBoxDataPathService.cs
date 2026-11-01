using System.Security.Cryptography;
using System.Text;

namespace DeskBox.Services;

public sealed class DeskBoxDataPathService
{
    public const string DevelopmentRootEnvironmentVariable = "DESKBOX_DEV_DATA_ROOT";
    public const string AotPreviewRootEnvironmentVariable = "DESKBOX_AOT_PREVIEW_DATA_ROOT";
    public const string DataRootOverrideEnvironmentVariable = "DESKBOX_DATA_ROOT";
    private const string ProductionInstanceScope = "7F3A9B2E";

    public static DeskBoxDataPathService Current { get; } = new(ResolveConfiguredRoot());

    public DeskBoxDataPathService(string? rootPath = null)
    {
        string productionRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DeskBox"));
        RootPath = string.IsNullOrWhiteSpace(rootPath)
            ? productionRoot
            : Path.GetFullPath(rootPath.Trim());
        IsDevelopmentRoot = !string.Equals(
            RootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            productionRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
        InstanceScope = IsDevelopmentRoot
            ? CreateInstanceScope(RootPath)
            : ProductionInstanceScope;
    }

    public string RootPath { get; }
    public bool IsDevelopmentRoot { get; }
    public string InstanceScope { get; }
    public string ActivationEventName => $"DeskBox_Activate_Event_{InstanceScope}";
    public string SingleInstanceMutexName => $"DeskBox_SingleInstance_Mutex_{InstanceScope}";
    public string DataDirectory => Path.Combine(RootPath, "data");
    public string UpdatesDirectory => Path.Combine(RootPath, "updates");
    // Recovery snapshots intentionally live beside, rather than inside, the
    // app-data root. Direct install: the directory really is
    // %LOCALAPPDATA%\DeskBox-Recovery and survives a normal uninstall.
    // Store (MSIX): AppData write virtualization redirects the write into
    // Packages\<PFN>\LocalCache, so uninstalling the package deletes the
    // recovery copy with it; an in-place update over the same
    // PackageFamilyName keeps LocalCache and therefore the snapshots.
    // Guaranteeing uninstall survival on the store channel as well would
    // require declaring desktop6:FileSystemWriteVirtualization=disabled
    // (a restricted capability needing store approval) — pending product
    // decision.
    public string RecoveryDirectory => IsDevelopmentRoot
        ? $"{RootPath}-Recovery"
        : Path.Combine(
            Path.GetDirectoryName(RootPath) ?? RootPath,
            "DeskBox-Recovery");
    public string LogFilePath => Path.Combine(RootPath, "DeskBox.log");

    private static string? ResolveConfiguredRoot()
    {
#if DEBUG
        return Environment.GetEnvironmentVariable(DevelopmentRootEnvironmentVariable);
#elif DESKBOX_NATIVE_AOT
        return Environment.GetEnvironmentVariable(AotPreviewRootEnvironmentVariable);
#else
        // 本机环境修复：某些机器上 %LOCALAPPDATA%\DeskBox 对进程不可写
        // （CreateDirectory/WriteAllText 抛 0x80070005），会导致设置无法保存、
        // 小组件无法初始化，并在打开设置页时以 WinRT stowed exception 崩溃。
        // 这里先做一次可写性探测，不可写则自动换用备选目录，避免整机环境问题
        // 让程序彻底不可用。显式覆盖变量始终优先。
        try
        {
            string? overrideRoot = Environment.GetEnvironmentVariable(DataRootOverrideEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideRoot))
            {
                return overrideRoot;
            }

            string profileRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DeskBox");
            if (IsWritableRoot(profileRoot))
            {
                return null;
            }

            foreach (string candidate in EnumerateFallbackRoots())
            {
                if (IsWritableRoot(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
#endif
    }

    /// <summary>备选数据根目录：优先程序自身目录（实测唯一稳定可写），其余作为后备。</summary>
    private static IEnumerable<string> EnumerateFallbackRoots()
    {
        string? selfRoot = null;
        try
        {
            selfRoot = Path.Combine(
                AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                "DeskBoxData");
        }
        catch
        {
            // 路径解析失败时跳过该候选。
        }

        if (!string.IsNullOrWhiteSpace(selfRoot))
        {
            yield return selfRoot;
        }

        string? programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            yield return Path.Combine(programData, "DeskBox");
        }

        string? profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile))
        {
            yield return Path.Combine(profile, "DeskBoxData");
        }
    }

    /// <summary>真实建目录 + 写删探针文件，判断该根目录能否保存状态。</summary>
    private static bool IsWritableRoot(string rootPath)
    {
        string probe = Path.Combine(rootPath, ".write-probe");
        try
        {
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(probe, DateTime.UtcNow.ToString("O"));
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string CreateInstanceScope(string rootPath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rootPath.ToUpperInvariant()));
        return Convert.ToHexString(hash.AsSpan(0, 8));
    }
}
