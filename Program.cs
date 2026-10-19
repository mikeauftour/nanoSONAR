using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Win32;
using System.Text.Json.Nodes;
using NAudio.Midi;
using System.Drawing;
using System.Windows.Forms;

namespace NanoSonarController;

internal static class Program
{
    [STAThread] static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MixerForm(Environment.GetCommandLineArgs().Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase)))); }
}

internal sealed class SonarApi : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly HttpClient ggHttp = new(new HttpClientHandler { ServerCertificateCustomValidationCallback = (request, cert, chain, errors) => request.RequestUri?.IsLoopback == true }) { Timeout = TimeSpan.FromSeconds(3) };
    private string? endpoint;
    private readonly SonarLiveFeedback liveFeedback = new();
    public void Dispose() { liveFeedback.Dispose(); http.Dispose(); ggHttp.Dispose(); }
    public async Task Connect()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SteelSeries", "GG", "coreProps.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(path))!;
        var address = root["ggEncryptedAddress"]?.GetValue<string>() ?? throw new Exception("ggEncryptedAddress fehlt.");
        if (!Uri.TryCreate("https://" + address, UriKind.Absolute, out var uri) || !uri.IsLoopback)
            throw new Exception("GG-Adresse ist keine lokale Loopback-Adresse.");
        using var apps = await ggHttp.GetAsync(new Uri(uri, "/subApps"));
        apps.EnsureSuccessStatusCode();
        var node = JsonNode.Parse(await apps.Content.ReadAsStringAsync())!;
        var sonar = node["subApps"]?["sonar"]?["metadata"]?["webServerAddress"]?.ToString();
        if (string.IsNullOrWhiteSpace(sonar)) throw new Exception("Sonar-Webserver nicht in subApps.sonar.metadata.webServerAddress gefunden.");
        var candidate = sonar.TrimEnd('/');
        if (!candidate.StartsWith("http://") && !candidate.StartsWith("https://")) candidate = "http://" + candidate;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var apiUri) || !apiUri.IsLoopback)
            throw new Exception("Sonar-API ist keine lokale Loopback-Adresse.");
        endpoint = candidate;
        liveFeedback.Configure(address);
        _ = await Volumes("classic");
    }
    private static string? FindSonar(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            
            if ((obj["name"]?.ToString().Contains("sonar", StringComparison.OrdinalIgnoreCase) == true || obj["id"]?.ToString().Contains("sonar", StringComparison.OrdinalIgnoreCase) == true) && obj["webServerAddress"] is not null)
                return obj["webServerAddress"]!.ToString();
            foreach (var pair in obj) { var found = FindSonar(pair.Value); if (found is not null) return found; }
        }
        if (node is JsonArray arr) foreach (var item in arr) { var found = FindSonar(item); if (found is not null) return found; }
        return null;
    }
    private string Url(string path) => (endpoint ?? throw new Exception("Sonar nicht verbunden.")) + "/" + path.TrimStart('/');
    public async Task<string> Mode() => (await http.GetStringAsync(Url("mode"))).Trim('"', '\r', '\n', ' ');
    public async Task<JsonNode> Volumes(string mode) => JsonNode.Parse(await http.GetStringAsync(Url(mode == "classic" ? "volumeSettings/classic" : "volumeSettings/streamer")))!;
    public async Task Put(string path) { using var response = await http.PutAsync(Url(path), null); response.EnsureSuccessStatusCode(); }
    public async Task SetVolume(string mode, string mix, string key, double value)
    {
        var v = Math.Clamp(value, 0, 1).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
        await Put(mode == "classic" ? $"volumeSettings/classic/{key}/volume/{v}" : $"volumeSettings/streamer/{mix}/{key}/volume/{v}");
        if (mode != "classic") await liveFeedback.Volume(mix, key, value);
    }
    public async Task SetMute(string mode, string mix, string key, bool muted)
    {
        await Put(mode == "classic" ? $"volumeSettings/classic/{key}/isMuted/{muted.ToString().ToLowerInvariant()}" : $"volumeSettings/streamer/{mix}/{key}/isMuted/{muted.ToString().ToLowerInvariant()}");
        if (mode != "classic") await liveFeedback.Mute(mix, key, muted);
    }
}

internal sealed class ControllerProfile
{
    public string Name { get; set; } = "Standard";
    // MIDI CC -> Sonar channel. Supported: master, game, chatRender, chatCapture, media, aux, none.
    public Dictionary<int, string> Faders { get; set; } = new();
    public Dictionary<int, string> Knobs { get; set; } = new();
    public Dictionary<int, string> Mutes { get; set; } = new();
    public Dictionary<int, string> Solos { get; set; } = new();
    public Dictionary<int, string> Records { get; set; } = new();
}

internal static class ProfileStore
{
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "nanoSONAR");
    public static readonly string FilePath = Path.Combine(Folder, "profiles-v072.json");
    public static List<ControllerProfile> Load()
    {
        Directory.CreateDirectory(Folder);
        if (!File.Exists(FilePath))
        {
            var defaults = new List<ControllerProfile> {
                new() { Name = "Standard", Faders = Map(0, "streaming"), Knobs = Map(16, "monitoring"), Mutes = Map(48, "monitoring"), Solos = Map(32, "streaming"), Records = Map(64, "none") },
                new() { Name = "Classic", Faders = Map(0, "classic"), Knobs = Map(16, "classic"), Mutes = Map(48, "classic"), Solos = Map(32, "classic"), Records = Map(64, "none") }
            };
            Save(defaults);
            return defaults;
        }
        var loaded = JsonSerializer.Deserialize<List<ControllerProfile>>(System.IO.File.ReadAllText(FilePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return loaded is { Count: > 0 } ? loaded : new List<ControllerProfile> { new() { Name = "Standard", Faders = Map(0, "streaming"), Knobs = Map(16, "monitoring"), Mutes = Map(48, "monitoring"), Solos = Map(32, "streaming") } };
    }
    private static Dictionary<int, string> Map(int start, string mix) => new() {
        [start] = "master@" + mix, [start + 1] = "game@" + mix, [start + 2] = "chatRender@" + mix,
        [start + 3] = "media@" + mix, [start + 4] = "aux@" + mix, [start + 5] = "chatCapture@" + mix,
        [start + 6] = "none", [start + 7] = "none"
    };
    public static void Save(List<ControllerProfile> profiles) => System.IO.File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true }));
}

internal sealed class RotaryDisplay : Control
{
    private int level = 50;
    private bool muted;
    public bool Muted { get => muted; set { muted = value; Invalidate(); } }
    public int Level { get => level; set { level = Math.Clamp(value, 0, 100); Invalidate(); } }
    public RotaryDisplay() { DoubleBuffered = true; MinimumSize = new Size(72, 72); BackColor = Color.FromArgb(31, 36, 48); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int d = Math.Min(Width - 12, Height - 12); d = Math.Min(d, 82);
        if (d < 12) return;
        var rect = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
        using var basePen = new Pen(Color.FromArgb(75, 84, 103), 7) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var activePen = new Pen(muted ? Color.FromArgb(240, 94, 105) : Color.FromArgb(66, 190, 242), 7) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        e.Graphics.DrawArc(basePen, rect, 135, 270);
        if (level > 0) e.Graphics.DrawArc(activePen, rect, 135, 270f * level / 100);
        using var font = new Font("Segoe UI", 11, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, muted ? "MUTE" : level + "%", font, rect, muted ? Color.FromArgb(255, 133, 141) : Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

internal sealed class MixerForm : Form
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "nanoSONAR";
    private static readonly string StartupSettingsPath = Path.Combine(ProfileStore.Folder, "startup.json");
    private sealed class StartupSettings
    {
        public bool AutoStart { get; set; } = true;
        public bool StartInTray { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool AutoConnect { get; set; } = true;
        public bool OsdEnabled { get; set; } = true;
        public bool OsdTopRight { get; set; } = true; // legacy v1.1 setting
        public string? OsdPosition { get; set; } // null migrates the previous left/right choice
        public int OsdDurationSeconds { get; set; } = 3;
        public bool OsdOnlyInTray { get; set; } = true;
    }
    private StartupSettings startup = new();
    private readonly VolumeOsdForm osd = new();
    private void ShowMidiOsd(string channel, string mix, int percent, bool muted)
    {
        if (!startup.OsdEnabled || (startup.OsdOnlyInTray && Visible && WindowState != FormWindowState.Minimized)) return;
        var label = channel switch { "master" => "Master", "game" => "Game", "chatRender" => "Chat", "chatCapture" => "Mic", "media" => "Media", "aux" => "Aux", _ => channel };
        osd.Display(label, mix == "monitoring" ? "Monitoring" : mix == "streaming" ? "Streaming" : "Classic", percent, muted, startup.OsdPosition ?? (startup.OsdTopRight ? "top-right" : "top-left"), startup.OsdDurationSeconds);
    }
    private readonly bool startedWithTrayArgument;
    private bool initialVisibilityHandled;
    private void LoadStartupSettings()
    {
        try { if (File.Exists(StartupSettingsPath)) startup = JsonSerializer.Deserialize<StartupSettings>(File.ReadAllText(StartupSettingsPath)) ?? new(); }
        catch { startup = new(); }
    }
    private void SaveStartupSettings()
    {
        try { Directory.CreateDirectory(ProfileStore.Folder); File.WriteAllText(StartupSettingsPath, JsonSerializer.Serialize(startup, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { MessageBox.Show(this, "Einstellungen konnten nicht gespeichert werden: " + ex.Message); }
    }
    private void ApplyWindowsAutostart(bool showError)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) throw new InvalidOperationException("Autostart-Registrierung nicht verfügbar.");
            if (startup.AutoStart)
            {
                string exe = Environment.ProcessPath ?? Application.ExecutablePath;
                // dotnet run uses dotnet.exe as host; do not register an invalid developer launch.
                if (Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) ||
                    (exe.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) || exe.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)) && !exe.Contains(@"\publish\", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Autostart erst mit der veröffentlichten nanoSONAR.exe aktivieren (dotnet publish).");
                key.SetValue(RunValue, $"\"{exe}\"" + (startup.StartInTray ? " --tray" : ""), RegistryValueKind.String);
            }
            else key.DeleteValue(RunValue, throwOnMissingValue: false);
        }
        catch (Exception ex) { if (showError) MessageBox.Show(this, "Windows-Autostart konnte nicht gesetzt werden:\n" + ex.Message, "nanoSONAR", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
    private void OpenMixer() { Show(); WindowState = FormWindowState.Normal; Activate(); }

    private readonly SonarApi api = new();
    private MidiIn? midi;
    private MidiOut? midiOut;
    private readonly System.Windows.Forms.Timer midiReconnectTimer = new() { Interval = 5000 };
    private bool midiInConnecting;
    private bool midiOutConnecting;
    private string preferredMidiIn = "nanoKONTROL2";
    private string preferredMidiOut = "nanoKONTROL2";
    private static readonly string DeviceSettingsPath = Path.Combine(ProfileStore.Folder, "devices.json");
    private sealed class DeviceSettings { public string Input { get; set; } = "nanoKONTROL2"; public string Output { get; set; } = "nanoKONTROL2"; }
    private void LoadDeviceSettings()
    {
        try { if (File.Exists(DeviceSettingsPath)) { var settings = JsonSerializer.Deserialize<DeviceSettings>(File.ReadAllText(DeviceSettingsPath)); if (settings != null) { preferredMidiIn = settings.Input; preferredMidiOut = settings.Output; } } } catch { }
    }
    private void SaveDeviceSettings()
    {
        try { Directory.CreateDirectory(ProfileStore.Folder); File.WriteAllText(DeviceSettingsPath, JsonSerializer.Serialize(new DeviceSettings { Input = preferredMidiIn, Output = preferredMidiOut })); } catch { }
    }
    private static string DeviceName(object? selected) => selected?.ToString()?.Split(':', 2).Last().Trim() ?? "";
    private static void SelectPreferred(ComboBox box, string preferred)
    {
        for (int i = 0; i < box.Items.Count; i++) if (string.Equals(DeviceName(box.Items[i]), preferred, StringComparison.OrdinalIgnoreCase)) { box.SelectedIndex = i; return; }
        for (int i = 0; i < box.Items.Count; i++) if (DeviceName(box.Items[i]).Contains("nanoKONTROL", StringComparison.OrdinalIgnoreCase)) { box.SelectedIndex = i; return; }
        box.SelectedIndex = -1;
    }
    private void AutoConnectMidi()
    {
        if (midi is null && !midiInConnecting && midiDevices.SelectedIndex >= 0) OpenMidi(false);
        if (midiOut is null && !midiOutConnecting && midiOutputs.SelectedIndex >= 0) OpenMidiOut(false);
    }
    private void RetryMidi()
    {
        if (midi is not null && midiOut is not null) return;
        ScanMidi();
        AutoConnectMidi();
    }
    private readonly Panel settingsControlHost = new() { Visible = false };
    private readonly ComboBox midiOutputs = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 185 };
    private readonly CheckBox invertLeds = new() { Text = "LED invertieren", AutoSize = true, ForeColor = Color.White };
    private readonly RotaryDisplay[] knobDisplays = new RotaryDisplay[8];
    private readonly Dictionary<string, (double Volume, bool Muted)> observed = new();
    private readonly Dictionary<string, int> pendingByTarget = new();
    private readonly ComboBox midiDevices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250 };
    private readonly ComboBox profileSelect = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly List<ControllerProfile> profiles = ProfileStore.Load();
    private ControllerProfile ActiveProfile => profileSelect.SelectedIndex >= 0 && profileSelect.SelectedIndex < profiles.Count ? profiles[profileSelect.SelectedIndex] : profiles.First();
    private readonly ComboBox mixSelect = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 135 };
    private readonly Label status = new() { AutoSize = true, ForeColor = Color.LightGray };
    private readonly Label modeLabel = new() { AutoSize = true, ForeColor = Color.White };
    private readonly TrackBar[] faders = new TrackBar[8];
    private readonly Label[] faderReadouts = new Label[8];
    private readonly Button[] streamMuteButtons = new Button[8];
    private readonly Button[] monitorMuteButtons = new Button[8];
    private readonly string[] keys = ["master", "game", "chatRender", "media", "aux", "chatCapture", "", ""];
    private readonly string[] names = ["Master", "Game", "Chat", "Media", "Aux", "Mic", "ChatMix*", "Frei"];
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 800 };
    private string mode = "classic";
    private bool connected;
    private bool refreshing;
    private readonly SemaphoreSlim gate = new(1, 1);
    
    private readonly System.Windows.Forms.Timer flushTimer = new() { Interval = 90 };
    private readonly NotifyIcon tray = new() { Text = "nanoSONAR", Visible = true };
    private Icon? appIcon;
    private void LoadAppIcon()
    {
        try
        {
            using var resource = typeof(MixerForm).Assembly.GetManifestResourceStream("nanoSONAR.AppIcon.ico");
            if (resource is not null) appIcon = new Icon(resource);
        }
        catch { }
        if (appIcon is not null) { Icon = appIcon; tray.Icon = appIcon; }
        else tray.Icon = SystemIcons.Application;
    }
    private bool closing;
    public MixerForm(bool startInTray = false)
    {
        startedWithTrayArgument = startInTray;
        LoadStartupSettings();
        LoadAppIcon();
        InitializeMixer();
    }
    private static readonly string WindowSettingsPath = Path.Combine(ProfileStore.Folder, "window.json");
    private sealed class WindowSettings { public int X { get; set; } public int Y { get; set; } public int Width { get; set; } = 1450; public int Height { get; set; } = 650; public bool Maximized { get; set; } }
    private void RestoreWindow()
    {
        try
        {
            if (!File.Exists(WindowSettingsPath)) return;
            var saved = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(WindowSettingsPath));
            if (saved is null) return;
            var size = new Size(Math.Max(MinimumSize.Width, saved.Width), 650);
            var bounds = new Rectangle(saved.X, saved.Y, size.Width, size.Height);
            if (!Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds))) return;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            // Height stays fixed; legacy maximized state is intentionally ignored.
        }
        catch { }
    }
    private void SaveWindow()
    {
        try
        {
            var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            if (bounds.Width < 1 || bounds.Height < 1) return;
            Directory.CreateDirectory(ProfileStore.Folder);
            File.WriteAllText(WindowSettingsPath, JsonSerializer.Serialize(new WindowSettings { X = bounds.X, Y = bounds.Y, Width = bounds.Width, Height = bounds.Height, Maximized = false }));
        }
        catch { }
    }
    private static void StyleMute(Button button, bool muted, bool available, string normalText, string mutedText)
    {
        button.Enabled = available;
        button.Text = muted ? mutedText : normalText;
        button.UseVisualStyleBackColor = false;
        button.BackColor = muted ? Color.FromArgb(168, 58, 69) : Color.FromArgb(43, 52, 67);
        button.ForeColor = available ? Color.White : Color.FromArgb(175, 186, 203);
        button.FlatAppearance.BorderColor = muted ? Color.FromArgb(255, 125, 137) : Color.FromArgb(96, 116, 143);
        button.FlatAppearance.BorderSize = 1;
    }
    private DateTime nextReconnect = DateTime.MinValue;

    private void InitializeMixer()
    {
        Text = "nanoSONAR v1.1 – KORG nanoKONTROL2"; Size = new Size(1450, 650); MinimumSize = new Size(1150, 650); MaximumSize = new Size(10000, 650); MaximizeBox = false;
        BackColor = Color.FromArgb(23, 26, 34); ForeColor = Color.White;
        // Compact toolbar: configuration controls live in a separate settings window.
        var toolbar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 4, RowCount = 1,
            Padding = new Padding(12, 8, 12, 6), BackColor = Color.FromArgb(31, 36, 48) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titleLabel = new Label { Text = "nanoSONAR", AutoSize = true, ForeColor = Color.White,
            Font = new Font("Segoe UI", 13, FontStyle.Bold), Anchor = AnchorStyles.Left, Margin = new Padding(0, 5, 20, 0) };
        var connectionLabel = new Label { Text = "● Verbinde …", AutoSize = true, ForeColor = Color.Gold,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 9, 20, 0) };
        var settingsButton = new Button { Text = "⚙ Einstellungen", Width = 145, Height = 32,
            FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(43, 52, 67) };
        settingsButton.FlatAppearance.BorderColor = Color.FromArgb(100, 120, 145);
        toolbar.Controls.Add(titleLabel, 0, 0);
        toolbar.Controls.Add(connectionLabel, 1, 0);
        var statusPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false,
            FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 7, 0, 0) };
        statusPanel.Controls.Add(modeLabel);
        statusPanel.Controls.Add(new Label { Width = 12 });
        statusPanel.Controls.Add(status);
        toolbar.Controls.Add(statusPanel, 2, 0);
        toolbar.Controls.Add(settingsButton, 3, 0);
        Controls.Add(toolbar);

        var scan = new Button { Text = "MIDI suchen", Width = 140, Height = 32 };
        var connectMidi = new Button { Text = "MIDI verbinden", Width = 140, Height = 32 };
        var connectSonar = new Button { Text = "Sonar verbinden", Width = 140, Height = 32 };
        var connectOut = new Button { Text = "MIDI OUT verbinden", Width = 150, Height = 32 };
        var editProfiles = new Button { Text = "Profile bearbeiten", Width = 150, Height = 32 };
        mixSelect.Items.AddRange(["Streaming", "Monitoring"]); mixSelect.SelectedIndex = 0;
        mixSelect.SelectedIndexChanged += async (_, _) => await RefreshVolumes();
        foreach (var profile in profiles) profileSelect.Items.Add(profile.Name);
        if (profileSelect.Items.Count > 0) profileSelect.SelectedIndex = 0;
        editProfiles.Click += (_, _) => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ProfileStore.FilePath) { UseShellExecute = true }); status.Text = "Profile nach Bearbeitung: App neu starten"; };
        connectOut.Click += (_, _) => OpenMidiOut();
        invertLeds.CheckedChanged += (_, _) => UpdateLeds();
        scan.Click += (_, _) => ScanMidi();
        connectMidi.Click += (_, _) => OpenMidi();
        connectSonar.Click += async (_, _) => await ConnectSonar();

        void OpenSettings()
        {
            using var dialog = new Form { Text = "nanoSONAR – Einstellungen", Size = new Size(620, 770),
                MinimumSize = new Size(620, 770), MaximumSize = new Size(620, 770),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent, BackColor = Color.FromArgb(31, 36, 48), ForeColor = Color.White };
            var settingsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 19,
                Padding = new Padding(20, 16, 20, 16) };
            settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            settingsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int r = 0; r < 18; r++) settingsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            settingsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            void AddSetting(int row, string label, Control control)
            {
                settingsGrid.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.LightSkyBlue }, 0, row);
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(0, 2, 0, 2);
                settingsGrid.Controls.Add(control, 1, row);
            }
            AddSetting(0, "MIDI-Eingang", midiDevices);
            AddSetting(1, "Geräte suchen", scan);
            AddSetting(2, "MIDI IN", connectMidi);
            AddSetting(3, "Sonar API", connectSonar);
            AddSetting(4, "MIDI-Ausgang", midiOutputs);
            AddSetting(5, "MIDI OUT", connectOut);
            AddSetting(6, "LEDs", invertLeds);
            AddSetting(7, "Profil", profileSelect);
            AddSetting(8, "Audiomix", mixSelect);
            AddSetting(9, "Profilverwaltung", editProfiles);
            var autoStartBox = new CheckBox { Text = "Mit Windows starten", Checked = startup.AutoStart, ForeColor = Color.White };
            var startInTrayBox = new CheckBox { Text = "Beim Autostart im Tray", Checked = startup.StartInTray, ForeColor = Color.White };
            var closeToTrayBox = new CheckBox { Text = "Schließen minimiert in Tray", Checked = startup.CloseToTray, ForeColor = Color.White };
            var autoConnectBox = new CheckBox { Text = "Automatisch verbinden", Checked = startup.AutoConnect, ForeColor = Color.White };
            AddSetting(10, "Windows-Autostart", autoStartBox);
            AddSetting(11, "Startmodus", startInTrayBox);
            AddSetting(12, "Schließen", closeToTrayBox);
            AddSetting(13, "Verbindungen", autoConnectBox);
            var osdEnabledBox = new CheckBox { Text = "Overlay bei MIDI-Bedienung", Checked = startup.OsdEnabled, ForeColor = Color.White };
            var osdPositionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            osdPositionBox.Items.AddRange(new object[] { "Oben links", "Oben rechts", "Oben Mitte", "Unten links", "Unten rechts", "Unten Mitte" });
            string[] osdPositions = { "top-left", "top-right", "top-center", "bottom-left", "bottom-right", "bottom-center" };
            string savedPosition = startup.OsdPosition ?? (startup.OsdTopRight ? "top-right" : "top-left");
            int savedIndex = Array.IndexOf(osdPositions, savedPosition);
            osdPositionBox.SelectedIndex = savedIndex >= 0 ? savedIndex : 1;
            var osdDurationBox = new NumericUpDown { Minimum = 1, Maximum = 10, Value = Math.Clamp(startup.OsdDurationSeconds, 1, 10) };
            var osdTrayBox = new CheckBox { Text = "Nur bei ausgeblendetem Mixer", Checked = startup.OsdOnlyInTray, ForeColor = Color.White };
            AddSetting(14, "OSD aktivieren", osdEnabledBox);
            AddSetting(15, "OSD-Position", osdPositionBox);
            AddSetting(16, "Anzeigedauer (Sek.)", osdDurationBox);
            AddSetting(17, "OSD-Sichtbarkeit", osdTrayBox);
            void SaveOptions()
            {
                startup.AutoStart = autoStartBox.Checked;
                startup.StartInTray = startInTrayBox.Checked;
                startup.CloseToTray = closeToTrayBox.Checked;
                startup.AutoConnect = autoConnectBox.Checked;
                startup.OsdEnabled = osdEnabledBox.Checked;
                startup.OsdPosition = osdPositions[Math.Max(0, osdPositionBox.SelectedIndex)];
                startup.OsdTopRight = startup.OsdPosition == "top-right";
                startup.OsdDurationSeconds = (int)osdDurationBox.Value;
                startup.OsdOnlyInTray = osdTrayBox.Checked;
                if (!startup.OsdEnabled) osd.Hide();
                SaveStartupSettings();
                ApplyWindowsAutostart(true);
                if (startup.AutoConnect) { AutoConnectMidi(); _ = ConnectSonar(); }
            }
            var closeButton = new Button { Text = "Speichern & Schließen", Width = 120, Height = 30, Anchor = AnchorStyles.Right };
            closeButton.Click += (_, _) => { SaveOptions(); dialog.Close(); };
            settingsGrid.Controls.Add(closeButton, 1, 18);
            dialog.Controls.Add(settingsGrid);
            dialog.ShowDialog(this);
            // WinForms removes the controls from the disposed dialog; they are reparented
            // to a persistent hidden host so they can be shown again next time.
            foreach (Control control in new Control[] { midiDevices, scan, connectMidi, connectSonar,
                midiOutputs, connectOut, invertLeds, profileSelect, mixSelect, editProfiles })
                control.Parent = settingsControlHost;
        }
        settingsButton.Click += (_, _) => OpenSettings();
        Controls.Add(settingsControlHost);
        var mixerViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var mixer = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = false, Height = 525, Width = 8 * 144 + 20, ColumnCount = 8, RowCount = 1, Padding = new Padding(10, 12, 10, 8) };
        for (int i = 0; i < 8; i++)
        {
            mixer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5f));
            int index = i;
            var strip = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 8, ColumnCount = 1, Padding = new Padding(5), BackColor = Color.FromArgb(31, 36, 48), Margin = new Padding(4) };
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            strip.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            strip.Controls.Add(new Label { Text = names[i], TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold) }, 0, 0);
            strip.Controls.Add(new Label { Text = "FADER · STREAM", TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill, ForeColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 8) }, 0, 1);
            var slider = new TrackBar { Orientation = Orientation.Vertical, Minimum = 0, Maximum = 100, TickFrequency = 10, Value = 50, Dock = DockStyle.Fill, Enabled = i < 6 };
            faders[i] = slider;
            slider.Scroll += async (_, _) => await ChangeVolume(index, slider.Value / 100.0);
            strip.Controls.Add(slider, 0, 2);
            var pct = new Label { Text = "50%", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGray };
            faderReadouts[i] = pct;
            slider.ValueChanged += (_, _) => { if (pct.Tag is not true) pct.Text = slider.Value + "%"; };
            strip.Controls.Add(pct, 0, 3);
            strip.Controls.Add(new Label { Text = "REGLER · MONITOR", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 8) }, 0, 4);
            var knob = new RotaryDisplay { Dock = DockStyle.Fill, Level = 50 };
            knobDisplays[i] = knob;
            strip.Controls.Add(knob, 0, 5);
            var streamMute = new Button { Text = "S · Stream", Dock = DockStyle.Fill, Enabled = i < 6, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            StyleMute(streamMute, false, i < 6, "S · Stream", "S · STUMM");
            streamMuteButtons[i] = streamMute;
            streamMute.Click += async (_, _) => await ToggleAssignedMute(index, false);
            strip.Controls.Add(streamMute, 0, 6);
            var monitorMute = new Button { Text = "M · Monitor", Dock = DockStyle.Fill, Enabled = i < 6, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
            StyleMute(monitorMute, false, i < 6, "M · Monitor", "M · STUMM");
            monitorMuteButtons[i] = monitorMute;
            monitorMute.Click += async (_, _) => await ToggleAssignedMute(index, true);
            strip.Controls.Add(monitorMute, 0, 7);
            mixer.Controls.Add(strip, i, 0);
        }
        // Keep the mixer readable: resize proportionally down to 140 px per channel.
        // The form has a minimum width and a fixed height.
        void AdaptMixer()
        {
            int available = Math.Max(0, mixerViewport.ClientSize.Width - 4);
            int stripWidth = Math.Max(140, available / 8);
            mixer.Width = stripWidth * 8;
            bool compact = stripWidth < 155;
            for (int j = 0; j < 8; j++)
            {
                if (mixer.GetControlFromPosition(j, 0) is not TableLayoutPanel column) continue;
                if (column.GetControlFromPosition(0, 1) is Label faderLabel)
                    faderLabel.Text = compact ? "STREAM" : "FADER · STREAM";
                if (column.GetControlFromPosition(0, 4) is Label knobLabel)
                    knobLabel.Text = compact ? "MONITOR" : "REGLER · MONITOR";
                if (streamMuteButtons[j] is Button sb)
                    sb.Font = new Font("Segoe UI", compact ? 8f : 9f, FontStyle.Bold);
                if (monitorMuteButtons[j] is Button mb)
                    mb.Font = new Font("Segoe UI", compact ? 8f : 9f, FontStyle.Bold);
            }
        }
        mixerViewport.Controls.Add(mixer); Controls.Add(mixerViewport); mixerViewport.BringToFront();
        mixerViewport.ClientSizeChanged += (_, _) => AdaptMixer();
        AdaptMixer();
        var footer = new Label { Dock = DockStyle.Bottom, Height = 28, ForeColor = Color.Silver, Text = "Fader: Streaming · Drehregler: Monitoring · S: Stream-Mute · M: Monitor-Mute · R: frei · Zuordnungen über Profile", TextAlign = ContentAlignment.MiddleCenter };
        Controls.Add(footer);
        timer.Tick += async (_, _) =>
        {
            await RefreshVolumes();
            bool ready = connected && midi is not null && midiOut is not null;
            connectionLabel.Text = ready ? "● Verbunden" : connected ? "● MIDI prüfen" : "● Sonar getrennt";
            connectionLabel.ForeColor = ready ? Color.LightGreen : Color.Gold;
        }; timer.Start();
        flushTimer.Tick += async (_, _) => await FlushPending(); flushTimer.Start();
        LoadDeviceSettings(); RestoreWindow(); ScanMidi();
        midiReconnectTimer.Tick += (_, _) => { if (startup.AutoConnect) RetryMidi(); }; midiReconnectTimer.Start();
        Shown += async (_, _) =>
        {
            if (!initialVisibilityHandled)
            {
                initialVisibilityHandled = true;
                if (startedWithTrayArgument) BeginInvoke(new Action(() => Hide()));
            }
            if (startup.AutoConnect) { AutoConnectMidi(); await ConnectSonar(); }
        };
        // Apply default autostart once, then keep it in sync with the installed EXE path.
        ApplyWindowsAutostart(false);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Anzeigen", null, (_, _) => { OpenMixer(); });
        menu.Items.Add("Einstellungen", null, (_, _) => { OpenMixer(); settingsButton.PerformClick(); });
        menu.Items.Add("Beenden", null, (_, _) => { closing = true; Close(); });
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => { OpenMixer(); };
        Resize += (_, _) => { if (WindowState == FormWindowState.Minimized) Hide(); };
        FormClosing += (_, e) => { if (!closing && startup.CloseToTray && e.CloseReason == CloseReason.UserClosing) { SaveWindow(); e.Cancel = true; Hide(); } else SaveWindow(); };
        FormClosed += (_, _) => { timer.Stop(); flushTimer.Stop(); midiReconnectTimer.Stop(); midi?.Dispose(); midiOut?.Dispose(); api.Dispose(); tray.Visible = false; tray.Dispose(); osd.Dispose(); appIcon?.Dispose(); gate.Dispose(); };
    }
    private void ScanMidi()
    {
        if (midi is null)
        {
            midiDevices.Items.Clear();
            for (int i = 0; i < MidiIn.NumberOfDevices; i++) midiDevices.Items.Add($"{i}: {MidiIn.DeviceInfo(i).ProductName}");
            SelectPreferred(midiDevices, preferredMidiIn);
        }
        if (midiOut is null)
        {
            midiOutputs.Items.Clear();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++) midiOutputs.Items.Add($"{i}: {MidiOut.DeviceInfo(i).ProductName}");
            SelectPreferred(midiOutputs, preferredMidiOut);
        }
    }
    private void OpenMidi(bool manual = true)
    {
        if (midiInConnecting || (midi != null && !manual)) return;
        midiInConnecting = true;
        try
        {
            midi?.Stop(); midi?.Dispose(); midi = null;
            if (midiDevices.SelectedIndex < 0) throw new Exception("Kein MIDI-Gerät gewählt.");
            var id = int.Parse(midiDevices.SelectedItem!.ToString()!.Split(':')[0]);
            var newMidi = new MidiIn(id);
            newMidi.MessageReceived += (_, e) =>
            {
                if (e.MidiEvent is not ControlChangeEvent cc) return;
                int number = (int)cc.Controller;
                int value = cc.ControllerValue;
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(new Action(() => HandleMidi(number, value))); }
                catch (InvalidOperationException) { }
            };
            newMidi.Start(); midi = newMidi;
            preferredMidiIn = DeviceName(midiDevices.SelectedItem); SaveDeviceSettings();
            status.Text = "MIDI IN automatisch verbunden";
        }
        catch (Exception ex) { if (manual) status.Text = "MIDI IN: " + ex.Message; }
        finally { midiInConnecting = false; }
    }
    private void OpenMidiOut(bool manual = true)
    {
        if (midiOutConnecting || (midiOut != null && !manual)) return;
        midiOutConnecting = true;
        try
        {
            midiOut?.Dispose(); midiOut = null;
            if (midiOutputs.SelectedIndex < 0) throw new Exception("Kein MIDI-Ausgang gewählt.");
            int id = int.Parse(midiOutputs.SelectedItem!.ToString()!.Split(':')[0]);
            midiOut = new MidiOut(id);
            preferredMidiOut = DeviceName(midiOutputs.SelectedItem); SaveDeviceSettings();
            status.Text = "MIDI OUT automatisch verbunden (LED: External)";
            UpdateLeds();
        }
        catch (Exception ex) { if (manual) status.Text = "MIDI OUT: " + ex.Message; }
        finally { midiOutConnecting = false; }
    }
    private static readonly HashSet<string> AllowedKeys = new(StringComparer.OrdinalIgnoreCase) { "master", "game", "chatRender", "chatCapture", "media", "aux" };
    private static (string Channel, string Mix) ParseTarget(string mapping)
    {
        var parts = mapping.Split('@', 2);
        return (parts[0], parts.Length == 2 ? parts[1].ToLowerInvariant() : "auto");
    }
    private string EffectiveMix(string requested)
    {
        if (mode == "classic") return "classic";
        return requested is "streaming" or "monitoring" or "classic" ? requested : (mixSelect.SelectedIndex == 0 ? "streaming" : "monitoring");
    }
    private static string TargetId(string channel, string mix) => channel + "@" + mix;
    private void HandleMidi(int cc, int value)
    {
        if (profiles.Count == 0) return;
        var profile = ActiveProfile;
        // S row controls streaming mute; M row controls monitoring mute.
        if (profile.Solos.TryGetValue(cc, out var muteMapping) || profile.Mutes.TryGetValue(cc, out muteMapping))
        {
            if (value > 0)
            {
                var (channel, requested) = ParseTarget(muteMapping);
                if (AllowedKeys.Contains(channel)) _ = ToggleMappedMute(channel, EffectiveMix(requested));
            }
            return;
        }
        // R is reserved.
        if (profile.Records.ContainsKey(cc)) return;
        string? target = null;
        bool knob = false;
        if (profile.Faders.TryGetValue(cc, out var fader)) target = fader;
        else if (profile.Knobs.TryGetValue(cc, out var rotary)) { target = rotary; knob = true; }
        if (target is null) return;
        var (key, mixRequest) = ParseTarget(target);
        if (!AllowedKeys.Contains(key)) return;
        var mix = EffectiveMix(mixRequest);
        pendingByTarget[TargetId(key, mix)] = value;
        var priorMuted = observed.TryGetValue(TargetId(key, mix), out var osdState) && osdState.Muted;
        ShowMidiOsd(key, mix, (int)Math.Round(value * 100.0 / 127), priorMuted);
        int i = Array.FindIndex(keys, k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            if (knob) knobDisplays[i].Level = (int)Math.Round(value * 100.0 / 127);
            else faders[i].Value = Math.Clamp((int)Math.Round(value * 100.0 / 127), 0, 100);
        }
    }
    private void UpdateLeds()
    {
        if (midiOut is null || profiles.Count == 0) return;
        // R LEDs remain off. S and M LEDs independently reflect their assigned mix.
        foreach (var cc in ActiveProfile.Records.Keys)
        {
            try { midiOut.Send(MidiMessage.ChangeControl(cc, 0, 1).RawData); }
            catch (Exception ex) { status.Text = "LED-Ausgabe: " + ex.Message; return; }
        }
        foreach (var entry in ActiveProfile.Solos.Concat(ActiveProfile.Mutes))
        {
            var (channel, requested) = ParseTarget(entry.Value);
            var mix = EffectiveMix(requested);
            bool valid = AllowedKeys.Contains(channel);
            bool muted = valid && observed.TryGetValue(TargetId(channel, mix), out var state) && state.Muted;
            int value = valid && (muted ^ invertLeds.Checked) ? 127 : 0;
            try { midiOut.Send(MidiMessage.ChangeControl(entry.Key, value, 1).RawData); }
            catch (Exception ex) { status.Text = "LED-Ausgabe: " + ex.Message; return; }
        }
    }

    private async Task ConnectSonar()
    {
        try { await api.Connect(); connected = true; status.Text = "Sonar verbunden"; await RefreshVolumes(); }
        catch (Exception ex) { connected = false; status.Text = "Sonar: " + ex.Message; }
    }
    private async Task RefreshVolumes()
    {
        if (!connected)
        {
            if (startup.AutoConnect && DateTime.UtcNow >= nextReconnect) { nextReconnect = DateTime.UtcNow.AddSeconds(5); await ConnectSonar(); }
            return;
        }
        if (!await gate.WaitAsync(0)) return;
        try
        {
            mode = await api.Mode();
            modeLabel.Text = "Sonar: " + (mode == "classic" ? "Classic" : "Streamer");
            mixSelect.Enabled = mode != "classic";
            var root = await api.Volumes(mode);
            refreshing = true;
            observed.Clear();
            foreach (var key in keys.Take(6))
            {
                var node = key == "master" ? root["masters"] : root["devices"]?[key];
                foreach (var mix in new[] { "classic", "streaming", "monitoring" })
                {
                    JsonNode? state = mix == "classic" ? node?["classic"] : node?["stream"]?[mix];
                    if (state?["volume"] is JsonNode vol && state?["muted"] is JsonNode mute)
                        observed[TargetId(key, mix)] = (vol.GetValue<double>(), mute.GetValue<bool>());
                }
            }
            for (int i = 0; i < 6; i++)
            {
                var key = keys[i];
                var faderMapping = ActiveProfile.Faders.TryGetValue(i, out var f) ? f : key + "@auto";
                var knobMapping = ActiveProfile.Knobs.TryGetValue(16 + i, out var k) ? k : key + "@auto";
                var (fc, fm) = ParseTarget(faderMapping);
                var (kc, km) = ParseTarget(knobMapping);
                var fMix = EffectiveMix(fm);
                var kMix = EffectiveMix(km);
                if (observed.TryGetValue(TargetId(fc, fMix), out var fs))
                {
                    if (!pendingByTarget.ContainsKey(TargetId(fc, fMix))) faders[i].Value = Math.Clamp((int)Math.Round(fs.Volume * 100), 0, 100);
                }
                var streamStateKnown = observed.TryGetValue(TargetId(fc, fMix), out var streamState);
                StyleMute(streamMuteButtons[i], streamStateKnown && streamState.Muted, streamStateKnown, "S · Stream", "S · STUMM");
                // Visual feedback for the STREAM fader, independent of the monitoring rotary display.
                var streamMuted = streamStateKnown && streamState.Muted;
                faderReadouts[i].Tag = streamMuted;
                faderReadouts[i].Text = streamMuted ? "STREAM STUMM" : faders[i].Value + "%";
                faderReadouts[i].ForeColor = streamMuted ? Color.FromArgb(255, 133, 141) : Color.LightGray;
                faderReadouts[i].BackColor = streamMuted ? Color.FromArgb(85, 37, 47) : Color.FromArgb(31, 36, 48);
                faderReadouts[i].Font = new Font("Segoe UI", streamMuted ? 8f : 9f, streamMuted ? FontStyle.Bold : FontStyle.Regular);
                faders[i].BackColor = streamMuted ? Color.FromArgb(85, 37, 47) : Color.FromArgb(31, 36, 48);
                var monitorStateKnown = observed.TryGetValue(TargetId(kc, kMix), out var monitorState);
                StyleMute(monitorMuteButtons[i], monitorStateKnown && monitorState.Muted, monitorStateKnown, "M · Monitor", "M · STUMM");
                knobDisplays[i].Muted = monitorStateKnown && monitorState.Muted;
                if (monitorStateKnown && !pendingByTarget.ContainsKey(TargetId(kc, kMix)))
                    knobDisplays[i].Level = (int)Math.Round(monitorState.Volume * 100);
            }
            UpdateLeds();
        }
        catch (Exception ex) { connected = false; nextReconnect = DateTime.UtcNow.AddSeconds(5); status.Text = "Sonar-Verbindung verloren: " + ex.Message; }
        finally { refreshing = false; gate.Release(); }
    }
    private Task ChangeVolume(int index, double value)
    {
        if (!refreshing && connected && index < 6)
        {
            var mapping = ActiveProfile.Faders.TryGetValue(index, out var m) ? m : keys[index] + "@auto";
            var (key, requested) = ParseTarget(mapping);
            if (AllowedKeys.Contains(key)) pendingByTarget[TargetId(key, EffectiveMix(requested))] = (int)Math.Round(value * 127);
        }
        return Task.CompletedTask;
    }
    private async Task FlushPending()
    {
        if (!connected || pendingByTarget.Count == 0 || !await gate.WaitAsync(0)) return;
        try
        {
            var snapshot = pendingByTarget.ToArray(); pendingByTarget.Clear();
            foreach (var item in snapshot)
            {
                var (key, mix) = ParseTarget(item.Key);
                try
                {
                    await api.SetVolume(mix == "classic" ? "classic" : "streamer", mix, key, item.Value / 127.0);
                    status.Text = $"Sonar geschrieben: {key} = {Math.Round(item.Value * 100.0 / 127)}% ({mix})";
                }
                catch (Exception ex) { status.Text = $"Schreibfehler {key}@{mix}: {ex.Message}"; }
            }
        }
        catch (Exception ex) { status.Text = "Schreibfehler: " + ex.Message; }
        finally { gate.Release(); }
        await RefreshVolumes();
    }
    private async Task ToggleMappedMute(string key, string mix)
    {
        if (!connected || !AllowedKeys.Contains(key)) return;
        await gate.WaitAsync();
        try
        {
            if (!observed.TryGetValue(TargetId(key, mix), out var state))
            {
                var root = await api.Volumes(mix == "classic" ? "classic" : "streamer");
                var node = key == "master" ? root["masters"] : root["devices"]?[key];
                var item = mix == "classic" ? node?["classic"] : node?["stream"]?[mix];
                state = (item?["volume"]?.GetValue<double>() ?? 0, item?["muted"]?.GetValue<bool>() ?? false);
            }
            await api.SetMute(mix == "classic" ? "classic" : "streamer", mix, key, !state.Muted);
            observed[TargetId(key, mix)] = (state.Volume, !state.Muted);
            ShowMidiOsd(key, mix, (int)Math.Round(state.Volume * 100), !state.Muted);
            UpdateLeds();
            status.Text = $"Mute {key}@{mix}: {!state.Muted}";
        }
        catch (Exception ex) { status.Text = "Mute-Fehler: " + ex.Message; }
        finally { gate.Release(); }
        await RefreshVolumes();
    }
    private Task ToggleAssignedMute(int index, bool monitoring)
    {
        var mapping = monitoring
            ? (ActiveProfile.Knobs.TryGetValue(16 + index, out var k) ? k : keys[index] + "@monitoring")
            : (ActiveProfile.Faders.TryGetValue(index, out var f) ? f : keys[index] + "@streaming");
        var (key, requested) = ParseTarget(mapping);
        return ToggleMappedMute(key, EffectiveMix(requested));
    }
}
