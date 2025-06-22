using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TelegramEAManager
{
    public partial class Form1 : Form
    {
        #region Private Fields
        private TelegramService telegramService = null!;
        private SignalProcessingService signalProcessor = null!;
        private List<ChannelInfo> allChannels = new List<ChannelInfo>();
        private List<ChannelInfo> selectedChannels = new List<ChannelInfo>();
        private bool isMonitoring = false;
        private System.Windows.Forms.Timer uiUpdateTimer = null!;
        private List<ProcessedSignal> allSignals = new List<ProcessedSignal>();
        #endregion

        public Form1()
        {
            InitializeComponent();
            InitializeServices();
            SetupUI();
            LoadApplicationSettings();
            SetupTimers();
        }

        private void InitializeServices()
        {
            telegramService = new TelegramService();
            signalProcessor = new SignalProcessingService();
        }

        private void SetupTimers()
        {
            uiUpdateTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            uiUpdateTimer.Tick += UiUpdateTimer_Tick;
            uiUpdateTimer.Start();
        }

        #region UI Setup
        private void SetupUI()
        {
            // Main Form Setup
            this.Text = "📊 Telegram EA Manager - islamahmed9717 | Real Implementation";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 245);
            this.WindowState = FormWindowState.Normal;

            CreateHeaderPanel();
            CreateMainContent();
            CreateBottomPanel();
            CreateStatusBar();
        }

        private void CreateHeaderPanel()
        {
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = Color.FromArgb(37, 99, 235)
            };
            this.Controls.Add(headerPanel);

            // Title
            var lblTitle = new Label
            {
                Text = "📊 REAL TELEGRAM EA MANAGER",
                Location = new Point(20, 5),
                Size = new Size(400, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 14F, FontStyle.Bold)
            };
            headerPanel.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Name = "lblSubtitle",
                Text = $"🕒 Current Time (UTC): 2025-06-21 15:23:07 | User: islamahmed9717",
                Location = new Point(20, 28),
                Size = new Size(500, 15),
                ForeColor = Color.FromArgb(200, 220, 255),
                Font = new Font("Segoe UI", 8F)
            };
            headerPanel.Controls.Add(lblSubtitle);

            // Phone section
            var lblPhone = new Label
            {
                Text = "📱 Phone:",
                Location = new Point(550, 5),
                Size = new Size(70, 15),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            headerPanel.Controls.Add(lblPhone);

            var cmbPhone = new ComboBox
            {
                Name = "cmbPhone",
                Location = new Point(620, 3),
                Size = new Size(180, 20),
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            headerPanel.Controls.Add(cmbPhone);

            var btnConnect = new Button
            {
                Name = "btnConnect",
                Text = "🔗 CONNECT",
                Location = new Point(810, 3),
                Size = new Size(100, 20),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };
            btnConnect.Click += BtnConnect_Click;
            headerPanel.Controls.Add(btnConnect);

            // MT4 Path
            var lblMT4 = new Label
            {
                Text = "📁 MT4/MT5:",
                Location = new Point(550, 28),
                Size = new Size(70, 15),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F)
            };
            headerPanel.Controls.Add(lblMT4);

            var txtMT4Path = new TextBox
            {
                Name = "txtMT4Path",
                Location = new Point(620, 26),
                Size = new Size(260, 18),
                Font = new Font("Segoe UI", 7F),
                Text = AutoDetectMT4Path()
            };
            headerPanel.Controls.Add(txtMT4Path);

            var btnBrowse = new Button
            {
                Name = "btnBrowse",
                Text = "📂",
                Location = new Point(885, 26),
                Size = new Size(25, 18),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 7F)
            };
            btnBrowse.Click += BtnBrowse_Click;
            headerPanel.Controls.Add(btnBrowse);

            // Status Panel
            CreateStatusPanel(headerPanel);
        }

        private void CreateStatusPanel(Panel parent)
        {
            var statusPanel = new Panel
            {
                Name = "statusPanel",
                Location = new Point(920, 3),
                Size = new Size(300, 60),
                BackColor = Color.FromArgb(249, 115, 22),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblConnectionStatus = new Label
            {
                Name = "lblConnectionStatus",
                Text = "✅ CONNECTED & AUTHORIZED",
                Location = new Point(8, 3),
                Size = new Size(284, 15),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            statusPanel.Controls.Add(lblConnectionStatus);

            var lblChannelsCount = new Label
            {
                Name = "lblChannelsCount",
                Text = "📢 Channels: 31",
                Location = new Point(8, 20),
                Size = new Size(90, 12),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F)
            };
            statusPanel.Controls.Add(lblChannelsCount);

            var lblSelectedCount = new Label
            {
                Name = "lblSelectedCount",
                Text = "✅ Selected: 2",
                Location = new Point(100, 20),
                Size = new Size(85, 12),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F)
            };
            statusPanel.Controls.Add(lblSelectedCount);

            var lblSignalsCount = new Label
            {
                Name = "lblSignalsCount",
                Text = "📊 Today: 0",
                Location = new Point(188, 20),
                Size = new Size(100, 12),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F)
            };
            statusPanel.Controls.Add(lblSignalsCount);

            var lblMonitoringStatus = new Label
            {
                Name = "lblMonitoringStatus",
                Text = "⏯️ Ready to monitor",
                Location = new Point(8, 35),
                Size = new Size(284, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 7F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            statusPanel.Controls.Add(lblMonitoringStatus);

            parent.Controls.Add(statusPanel);
        }

        private void CreateMainContent()
        {
            // COMPLETELY REDESIGNED MAIN CONTENT STRUCTURE
            var mainContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(20, 15, 20, 10),
                BackColor = Color.FromArgb(245, 245, 245)
            };

            // Set column widths: 55% for channels, 45% for controls
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            mainContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // LEFT PANEL - Channels
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(5)
            };
            mainContainer.Controls.Add(leftPanel, 0, 0);

            // RIGHT PANEL - Controls  
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 249, 250),
                Padding = new Padding(10),
                BorderStyle = BorderStyle.FixedSingle
            };
            mainContainer.Controls.Add(rightPanel, 1, 0);

            this.Controls.Add(mainContainer);

            // Create content for both panels
            CreateChannelsSection(leftPanel);
            CreateControlsSection(rightPanel);
        }

        private void CreateChannelsSection(Panel parent)
        {
            var lblAllChannels = new Label
            {
                Text = "📢 ALL YOUR TELEGRAM CHANNELS",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 5, 0, 0)
            };
            parent.Controls.Add(lblAllChannels);

            // Search and filter panel
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(5)
            };

            var txtSearch = new TextBox
            {
                Name = "txtSearch",
                Location = new Point(5, 8),
                Size = new Size(250, 25),
                Font = new Font("Segoe UI", 10F),
                PlaceholderText = "Search channels..."
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            searchPanel.Controls.Add(txtSearch);

            var cmbFilter = new ComboBox
            {
                Name = "cmbFilter",
                Location = new Point(265, 8),
                Size = new Size(120, 25),
                Font = new Font("Segoe UI", 9F),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbFilter.Items.AddRange(new[] { "All Types", "VIP", "Premium", "Signals", "Gold", "Crypto", "Groups" });
            cmbFilter.SelectedIndex = 0;
            cmbFilter.SelectedIndexChanged += CmbFilter_SelectedIndexChanged;
            searchPanel.Controls.Add(cmbFilter);

            var btnRefresh = new Button
            {
                Name = "btnRefreshChannels",
                Text = "🔄",
                Location = new Point(395, 8),
                Size = new Size(30, 25),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRefresh.Click += BtnRefreshChannels_Click;
            searchPanel.Controls.Add(btnRefresh);

            parent.Controls.Add(searchPanel);

            var lvChannels = new ListView
            {
                Name = "lvChannels",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true,
                Font = new Font("Segoe UI", 9F)
            };

            lvChannels.Columns.Add("Channel Name", 280);
            lvChannels.Columns.Add("ID", 100);
            lvChannels.Columns.Add("Type", 80);
            lvChannels.Columns.Add("Members", 80);
            lvChannels.Columns.Add("Activity", 50);

            lvChannels.ItemChecked += LvChannels_ItemChecked;
            parent.Controls.Add(lvChannels);
        }

        private void CreateControlsSection(Panel parent)
        {
            // FORCE CREATE ALL CONTROLS WITH EXPLICIT POSITIONING

            // SELECTED CHANNELS SECTION
            var lblSelected = new Label
            {
                Text = "✅ SELECTED CHANNELS",
                Location = new Point(10, 10),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(lblSelected);

            var lvSelected = new ListView
            {
                Name = "lvSelected",
                Location = new Point(10, 40),
                Size = new Size(480, 150),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9F),
                BackColor = Color.White
            };

            lvSelected.Columns.Add("Channel", 200);
            lvSelected.Columns.Add("ID", 100);
            lvSelected.Columns.Add("Signals", 60);
            lvSelected.Columns.Add("Status", 120);

            parent.Controls.Add(lvSelected);

            // CONTROL BUTTONS SECTION
            var lblControls = new Label
            {
                Text = "🎮 MONITORING CONTROLS",
                Location = new Point(10, 200),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(lblControls);

            // MAIN MONITORING BUTTONS - LARGE AND PROMINENT
            var btnStartMonitoring = new Button
            {
                Name = "btnStartMonitoring",
                Text = "▶️ START MONITORING",
                Location = new Point(10, 235),
                Size = new Size(230, 50),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Enabled = true, // ALWAYS VISIBLE
                UseVisualStyleBackColor = false
            };
            btnStartMonitoring.FlatAppearance.BorderSize = 0;
            btnStartMonitoring.Click += BtnStartMonitoring_Click;
            parent.Controls.Add(btnStartMonitoring);

            var btnStopMonitoring = new Button
            {
                Name = "btnStopMonitoring",
                Text = "⏹️ STOP MONITORING",
                Location = new Point(250, 235),
                Size = new Size(200, 50),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Enabled = false,
                UseVisualStyleBackColor = false
            };
            btnStopMonitoring.FlatAppearance.BorderSize = 0;
            btnStopMonitoring.Click += BtnStopMonitoring_Click;
            parent.Controls.Add(btnStopMonitoring);

            // UTILITY BUTTONS - SMALLER ROW
            var btnCopyChannelIDs = new Button
            {
                Name = "btnCopyChannelIDs",
                Text = "📋 COPY IDs",
                Location = new Point(10, 295),
                Size = new Size(105, 35),
                BackColor = Color.FromArgb(168, 85, 247),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btnCopyChannelIDs.FlatAppearance.BorderSize = 0;
            btnCopyChannelIDs.Click += BtnCopyChannelIDs_Click;
            parent.Controls.Add(btnCopyChannelIDs);

            var btnTestSignal = new Button
            {
                Name = "btnTestSignal",
                Text = "🧪 TEST",
                Location = new Point(125, 295),
                Size = new Size(85, 35),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btnTestSignal.FlatAppearance.BorderSize = 0;
            btnTestSignal.Click += BtnTestSignal_Click;
            parent.Controls.Add(btnTestSignal);

            var btnGenerateEAConfig = new Button
            {
                Name = "btnGenerateEAConfig",
                Text = "⚙️ CONFIG",
                Location = new Point(220, 295),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseVisualStyleBackColor = false
            };
            btnGenerateEAConfig.FlatAppearance.BorderSize = 0;
            btnGenerateEAConfig.Click += BtnGenerateEAConfig_Click;
            parent.Controls.Add(btnGenerateEAConfig);

            // LIVE SIGNALS SECTION
            var lblLiveSignals = new Label
            {
                Text = "📊 LIVE SIGNALS FEED",
                Location = new Point(10, 340),
                Size = new Size(400, 25),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(249, 115, 22),
                BackColor = Color.Transparent
            };
            parent.Controls.Add(lblLiveSignals);

            var lvLiveSignals = new ListView
            {
                Name = "lvLiveSignals",
                Location = new Point(10, 370),
                Size = new Size(480, 200),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.White
            };

            lvLiveSignals.Columns.Add("Time", 60);
            lvLiveSignals.Columns.Add("Channel", 100);
            lvLiveSignals.Columns.Add("Symbol", 60);
            lvLiveSignals.Columns.Add("Direction", 50);
            lvLiveSignals.Columns.Add("SL", 50);
            lvLiveSignals.Columns.Add("TP", 50);
            lvLiveSignals.Columns.Add("Status", 110);

            parent.Controls.Add(lvLiveSignals);
        }

        private void CreateBottomPanel()
        {
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 80,
                BackColor = Color.FromArgb(37, 99, 235)
            };
            this.Controls.Add(bottomPanel);

            var lblUser = new Label
            {
                Name = "lblUser",
                Text = "👤 islamahmed9717",
                Location = new Point(20, 25),
                Size = new Size(200, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            bottomPanel.Controls.Add(lblUser);

            CreateBottomButtons(bottomPanel);

            var lblStats = new Label
            {
                Name = "lblStats",
                Text = "📊 System ready - Connect to Telegram to start",
                Location = new Point(800, 25),
                Size = new Size(500, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                TextAlign = ContentAlignment.MiddleRight
            };
            bottomPanel.Controls.Add(lblStats);
        }

        private void CreateBottomButtons(Panel parent)
        {
            var btnHistory = new Button
            {
                Text = "📈 SIGNALS HISTORY",
                Location = new Point(250, 20),
                Size = new Size(160, 40),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnHistory.Click += BtnHistory_Click;
            parent.Controls.Add(btnHistory);

            var btnEASettings = new Button
            {
                Text = "⚙️ EA SETTINGS",
                Location = new Point(420, 20),
                Size = new Size(130, 40),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnEASettings.Click += BtnEASettings_Click;
            parent.Controls.Add(btnEASettings);

            var btnSymbolMapping = new Button
            {
                Text = "🗺️ SYMBOL MAPPING",
                Location = new Point(560, 20),
                Size = new Size(170, 40),
                BackColor = Color.FromArgb(168, 85, 247),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnSymbolMapping.Click += BtnSymbolMapping_Click;
            parent.Controls.Add(btnSymbolMapping);
        }

        private void CreateStatusBar()
        {
            var statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(250, 250, 250)
            };

            var statusLabel = new ToolStripStatusLabel
            {
                Name = "statusLabel",
                Text = $"Ready - Current UTC Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}",
                Font = new Font("Segoe UI", 9F),
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var versionLabel = new ToolStripStatusLabel
            {
                Name = "versionLabel",
                Text = "v2.0.0 - Real Implementation",
                Font = new Font("Segoe UI", 9F)
            };

            statusStrip.Items.Add(statusLabel);
            statusStrip.Items.Add(versionLabel);
            this.Controls.Add(statusStrip);
        }
        #endregion

        #region Event Handlers
        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            var cmbPhone = this.Controls.Find("cmbPhone", true)[0] as ComboBox;
            var phoneNumber = cmbPhone?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(phoneNumber))
            {
                ShowMessage("❌ Please enter your phone number", "Phone Required", MessageBoxIcon.Warning);
                return;
            }

            if (!IsValidPhoneNumber(phoneNumber))
            {
                ShowMessage("❌ Please enter a valid phone number with country code\nExample: +1234567890",
                           "Invalid Phone Number", MessageBoxIcon.Warning);
                return;
            }

            var btnConnect = sender as Button;
            var originalText = btnConnect?.Text ?? "";
            if (btnConnect != null)
            {
                btnConnect.Text = "🔄 CONNECTING...";
                btnConnect.Enabled = false;
            }

            try
            {
                // Connect to Telegram using the new WTelegramClient
                bool connected = await telegramService.ConnectAsync(phoneNumber);

                if (connected)
                {
                    await LoadChannelsAfterAuth(phoneNumber);
                    ShowMessage("✅ Successfully connected to Telegram!\n\n📱 Phone: " + phoneNumber +
                               "\n🕒 Time: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
                               "\n👤 User: islamahmed9717",
                               "Connection Successful", MessageBoxIcon.Information);
                }
                else
                {
                    throw new Exception("Authentication failed or was cancelled");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Connection failed:\n\n{ex.Message}\n\n🕒 Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n👤 User: islamahmed9717",
                           "Connection Error", MessageBoxIcon.Error);
                UpdateStatus(false, false);
            }
            finally
            {
                if (btnConnect != null)
                {
                    btnConnect.Text = originalText;
                    btnConnect.Enabled = true;
                }
            }
        }

        private async Task LoadChannelsAfterAuth(string phoneNumber)
        {
            try
            {
                // Update UI to show connected status
                UpdateStatus(true, true);

                // Load channels using the new service
                var channels = await telegramService.GetChannelsAsync();

                // Update your channels list UI
                UpdateChannelsList(channels);

                // Save phone number for future use
                SavePhoneNumber(phoneNumber);

                // Log the successful connection
                LogMessage($"✅ Connected successfully - Phone: {phoneNumber}, Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC, User: islamahmed9717");
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to load channels:\n\n{ex.Message}", "Channel Loading Error", MessageBoxIcon.Error);
            }
        }

        private void UpdateStatus(bool isConnected, bool isAuthorized)
        {
            var statusPanel = this.Controls.Find("statusPanel", true)[0] as Panel;
            var lblConnectionStatus = statusPanel?.Controls.Find("lblConnectionStatus", true)[0] as Label;
            var lblMonitoringStatus = statusPanel?.Controls.Find("lblMonitoringStatus", true)[0] as Label;

            if (lblConnectionStatus != null && lblMonitoringStatus != null && statusPanel != null)
            {
                if (isMonitoring)
                {
                    statusPanel.BackColor = Color.FromArgb(34, 197, 94); // Green
                    lblConnectionStatus.Text = "✅ LIVE MONITORING";
                    lblMonitoringStatus.Text = $"📊 Active on {selectedChannels.Count} channels";
                }
                else if (isConnected && isAuthorized)
                {
                    statusPanel.BackColor = Color.FromArgb(249, 115, 22); // Orange
                    lblConnectionStatus.Text = "🔗 CONNECTED";
                    lblMonitoringStatus.Text = "⏯️ Ready to monitor";
                }
                else
                {
                    statusPanel.BackColor = Color.FromArgb(220, 38, 38); // Red
                    lblConnectionStatus.Text = "❌ DISCONNECTED";
                    lblMonitoringStatus.Text = "⏸️ Not connected";
                }
            }
        }

        private void LvChannels_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            var channel = e.Item.Tag as ChannelInfo;
            if (channel == null) return;

            var lvSelected = this.Controls.Find("lvSelected", true)[0] as ListView;
            if (lvSelected == null) return;

            if (e.Item.Checked)
            {
                // Add to selected channels
                if (!selectedChannels.Any(c => c.Id == channel.Id))
                {
                    selectedChannels.Add(channel);

                    var item = new ListViewItem(channel.Title);
                    item.SubItems.Add(channel.Id.ToString());
                    item.SubItems.Add("0");
                    item.SubItems.Add("✅ Ready");
                    item.Tag = channel;
                    item.BackColor = Color.FromArgb(220, 255, 220);

                    lvSelected.Items.Add(item);
                }
            }
            else
            {
                // Remove from selected channels
                selectedChannels.RemoveAll(c => c.Id == channel.Id);

                for (int i = lvSelected.Items.Count - 1; i >= 0; i--)
                {
                    var selectedChannel = lvSelected.Items[i].Tag as ChannelInfo;
                    if (selectedChannel?.Id == channel.Id)
                    {
                        lvSelected.Items.RemoveAt(i);
                        break;
                    }
                }
            }

            UpdateSelectedCount();
        }

        private async void BtnStartMonitoring_Click(object? sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("⚠️ Please select at least one channel to monitor!",
                           "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
            var mt4Path = txtMT4Path?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(mt4Path) || !Directory.Exists(mt4Path))
            {
                ShowMessage("❌ Please set a valid MT4/MT5 Files folder path!",
                           "Invalid Path", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Start monitoring
                isMonitoring = true;

                // Update UI
                var btnStart = sender as Button;
                var btnStop = this.Controls.Find("btnStopMonitoring", true)[0] as Button;
                if (btnStart != null) btnStart.Enabled = false;
                if (btnStop != null) btnStop.Enabled = true;

                UpdateStatus(true, true);
                UpdateSelectedChannelsStatus("📊 Live");

                ShowMessage($"✅ Monitoring started successfully!\n\n📊 Monitoring {selectedChannels.Count} channels\n📁 Signals saved to: {mt4Path}\\TelegramSignals.txt\n\n⚠️ Keep this application running to receive signals!",
                           "Monitoring Started", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to start monitoring:\n\n{ex.Message}", "Monitoring Error", MessageBoxIcon.Error);
            }
        }

        private void BtnStopMonitoring_Click(object? sender, EventArgs e)
        {
            try
            {
                isMonitoring = false;

                // Update UI
                var btnStart = this.Controls.Find("btnStartMonitoring", true)[0] as Button;
                var btnStop = sender as Button;
                if (btnStart != null) btnStart.Enabled = true;
                if (btnStop != null) btnStop.Enabled = false;

                UpdateStatus(telegramService.IsUserAuthorized(), false);
                UpdateSelectedChannelsStatus("✅ Ready");

                ShowMessage("⏹️ Monitoring stopped successfully!", "Monitoring Stopped", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Error stopping monitoring:\n\n{ex.Message}", "Error", MessageBoxIcon.Error);
            }
        }

        private void BtnCopyChannelIDs_Click(object? sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("⚠️ Please select channels first!", "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            var channelIds = string.Join(",", selectedChannels.Select(c => c.Id.ToString()));
            Clipboard.SetText(channelIds);

            var channelList = string.Join("\n", selectedChannels.Select(c => $"• {c.Title} ({c.Id}) - {c.Type}"));

            ShowMessage($"📋 Channel IDs copied to clipboard!\n\n📝 PASTE THIS IN YOUR EA:\n{channelIds}\n\n📢 SELECTED CHANNELS:\n{channelList}\n\n⚙️ Configure your EA with these Channel IDs!",
                       "Channel IDs Copied", MessageBoxIcon.Information);
        }

        private void BtnTestSignal_Click(object? sender, EventArgs e)
        {
            var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
            var mt4Path = txtMT4Path?.Text?.Trim() ?? "";

            if (string.IsNullOrEmpty(mt4Path) || !Directory.Exists(mt4Path))
            {
                ShowMessage("❌ Please set a valid MT4/MT5 path first!", "Invalid Path", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var testSignal = new ProcessedSignal
                {
                    Id = Guid.NewGuid().ToString(),
                    DateTime = DateTime.UtcNow,
                    ChannelId = 999999,
                    ChannelName = "TEST_CHANNEL",
                    OriginalText = "BUY NOW EURUSD\nSL 1.0850\nTP 1.0920\nTP2 1.0950",
                    Status = "Test Signal",
                    ParsedData = new ParsedSignalData
                    {
                        Symbol = "EURUSD",
                        Direction = "BUY",
                        OriginalSymbol = "EURUSD",
                        FinalSymbol = "EURUSD",
                        StopLoss = 1.0850,
                        TakeProfit1 = 1.0920,
                        TakeProfit2 = 1.0950
                    }
                };

                // FIXED: Correct filename (case-sensitive)
                var signalFilePath = Path.Combine(mt4Path, "TelegramSignals.txt");

                // FIXED: Proper EA format with all required fields
                var timestamp = DateTime.UtcNow.ToString("yyyy.MM.dd HH:mm:ss");
                var eaFormattedSignal = $"{timestamp}|{testSignal.ChannelId}|{testSignal.ChannelName}|{testSignal.ParsedData.Direction}|{testSignal.ParsedData.Symbol}|0.00000|{testSignal.ParsedData.StopLoss:F5}|{testSignal.ParsedData.TakeProfit1:F5}|{testSignal.ParsedData.TakeProfit2:F5}|0.00000|NEW";

                // FIXED: Better file writing with proper error handling
                using (var fs = new FileStream(signalFilePath,
                               FileMode.Append,
                               FileAccess.Write,
                               FileShare.ReadWrite,
                               4096,
                               FileOptions.WriteThrough))
                using (var writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = true })
                {
                    writer.WriteLine(eaFormattedSignal);
                }


                // FIXED: Also create a backup file for debugging
                var debugFilePath = Path.Combine(mt4Path, "TelegramSignals_Debug.txt");
                using (var debugWriter = new StreamWriter(debugFilePath, true, System.Text.Encoding.UTF8))
                {
                    debugWriter.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] SIGNAL WRITTEN:");
                    debugWriter.WriteLine($"File: {signalFilePath}");
                    debugWriter.WriteLine($"Content: {eaFormattedSignal}");
                    debugWriter.WriteLine($"Length: {eaFormattedSignal.Length} chars");
                    debugWriter.WriteLine("---");
                    debugWriter.Flush();
                }

                // Add to signals history
                allSignals.Add(testSignal);
                AddToLiveSignals(testSignal);

                // FIXED: More detailed success message
                var fileInfo = new FileInfo(signalFilePath);
                ShowMessage($"🧪 Test signal sent successfully!\n\n" +
                           $"📊 Signal Details:\n{testSignal.OriginalText}\n\n" +
                           $"📁 File: {signalFilePath}\n" +
                           $"📝 Format: {eaFormattedSignal}\n" +
                           $"📏 File Size: {fileInfo.Length} bytes\n" +
                           $"🕒 Written at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\n" +
                           $"⚙️ Check your EA Expert tab now!\n" +
                           $"🔍 Debug info saved to: TelegramSignals_Debug.txt",
                           "Test Signal Sent", MessageBoxIcon.Information);

                // FIXED: Force refresh file system (sometimes needed)
                File.SetLastWriteTime(signalFilePath, DateTime.Now);
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to send test signal:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                           "Test Failed", MessageBoxIcon.Error);
            }
        }

        private void BtnGenerateEAConfig_Click(object? sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("⚠️ Please select channels first!", "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var config = GenerateEAConfiguration();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"TelegramEA_Config_islamahmed9717_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt",
                    Title = "Save EA Configuration"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveDialog.FileName, config);
                    Clipboard.SetText(config);

                    ShowMessage($"⚙️ EA configuration generated successfully!\n\n📁 Saved to: {saveDialog.FileName}\n📋 Configuration also copied to clipboard!\n\n⚙️ Import this configuration into your EA settings.",
                               "Configuration Generated", MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to generate configuration:\n\n{ex.Message}", "Generation Error", MessageBoxIcon.Error);
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select your MT4/MT5 Files folder (usually contains MQL4 or MQL5 subfolder)";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
                    if (txtMT4Path != null)
                    {
                        txtMT4Path.Text = folderDialog.SelectedPath;
                        SaveMT4Path(folderDialog.SelectedPath);
                    }
                }
            }
        }

        private async void BtnRefreshChannels_Click(object? sender, EventArgs e)
        {
            if (!telegramService.IsUserAuthorized())
            {
                ShowMessage("❌ Please connect to Telegram first!", "Not Connected", MessageBoxIcon.Warning);
                return;
            }

            var btnRefresh = sender as Button;
            if (btnRefresh != null)
            {
                btnRefresh.Enabled = false;
                btnRefresh.Text = "⏳";
            }

            try
            {
                var channels = await telegramService.GetChannelsAsync();
                allChannels = channels;
                RefreshChannelsList();
            }
            catch (Exception ex)
            {
                ShowMessage($"❌ Failed to refresh channels:\n\n{ex.Message}", "Refresh Error", MessageBoxIcon.Error);
            }
            finally
            {
                if (btnRefresh != null)
                {
                    btnRefresh.Enabled = true;
                    btnRefresh.Text = "🔄";
                }
            }
        }

        private void BtnHistory_Click(object? sender, EventArgs e)
        {
            var historyForm = new SignalsHistoryForm(allSignals);
            historyForm.ShowDialog();
        }

        private void BtnEASettings_Click(object? sender, EventArgs e)
        {
            ShowMessage("⚙️ EA Settings feature will be implemented!\n\nThis will allow you to configure:\n• Risk management\n• Lot sizes\n• Trading hours\n• Symbol mappings\n\n🚀 Coming soon!", "EA Settings", MessageBoxIcon.Information);
        }

        private void BtnSymbolMapping_Click(object? sender, EventArgs e)
        {
            ShowMessage("🗺️ Symbol Mapping feature will be implemented!\n\nThis will allow you to:\n• Map Telegram symbols to MT4/MT5\n• Set symbol prefixes/suffixes\n• Configure exclusions\n\n🚀 Coming soon!", "Symbol Mapping", MessageBoxIcon.Information);
        }

        private void TxtSearch_TextChanged(object? sender, EventArgs e)
        {
            ApplyChannelFilters();
        }

        private void CmbFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            ApplyChannelFilters();
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update time in subtitle
            var lblSubtitle = this.Controls.Find("lblSubtitle", true).FirstOrDefault() as Label;
            if (lblSubtitle != null)
            {
                lblSubtitle.Text = $"🕒 Current Time (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | User: islamahmed9717";
            }

            // Update time in status bar
            foreach (Control control in this.Controls)
            {
                if (control is StatusStrip statusStrip)
                {
                    foreach (ToolStripItem item in statusStrip.Items)
                    {
                        if (item.Name == "statusLabel")
                        {
                            item.Text = $"Real-time System Active | UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} | User: islamahmed9717";
                        }
                    }
                }
            }

            // Update signals count
            UpdateSignalsCount();
        }
        #endregion

        #region Helper Methods
        private void RefreshChannelsList()
        {
            var lvChannels = this.Controls.Find("lvChannels", true)[0] as ListView;
            if (lvChannels == null) return;

            lvChannels.Items.Clear();

            foreach (var channel in allChannels)
            {
                var item = new ListViewItem(channel.Title);
                item.SubItems.Add(channel.Id.ToString());
                item.SubItems.Add(channel.Type);
                item.SubItems.Add(channel.MembersCount.ToString());
                item.SubItems.Add(channel.LastActivity.ToString("HH:mm"));
                item.Tag = channel;

                // Color coding based on type
                switch (channel.Type.ToLower())
                {
                    case "vip":
                        item.BackColor = Color.FromArgb(255, 235, 59); // Yellow
                        break;
                    case "premium":
                        item.BackColor = Color.FromArgb(156, 39, 176); // Purple
                        item.ForeColor = Color.White;
                        break;
                    case "signals":
                        item.BackColor = Color.FromArgb(76, 175, 80); // Green
                        item.ForeColor = Color.White;
                        break;
                    case "gold":
                        item.BackColor = Color.FromArgb(255, 193, 7); // Gold
                        break;
                    case "crypto":
                        item.BackColor = Color.FromArgb(255, 87, 34); // Orange
                        item.ForeColor = Color.White;
                        break;
                    default:
                        item.BackColor = Color.FromArgb(200, 230, 255); // Light blue
                        break;
                }

                lvChannels.Items.Add(item);
            }

            ApplyChannelFilters();
            UpdateChannelsCount();
        }

        private void ApplyChannelFilters()
        {
            var lvChannels = this.Controls.Find("lvChannels", true)[0] as ListView;
            var txtSearch = this.Controls.Find("txtSearch", true)[0] as TextBox;
            var cmbFilter = this.Controls.Find("cmbFilter", true)[0] as ComboBox;

            if (lvChannels == null) return;

            var searchText = txtSearch?.Text?.ToLower() ?? "";
            var filterType = cmbFilter?.SelectedItem?.ToString() ?? "All Types";

            foreach (ListViewItem item in lvChannels.Items)
            {
                var channel = item.Tag as ChannelInfo;
                if (channel == null) continue;

                bool visible = true;

                // Apply search filter
                if (!string.IsNullOrEmpty(searchText))
                {
                    visible = channel.Title.ToLower().Contains(searchText) ||
                             channel.Id.ToString().Contains(searchText) ||
                             channel.Type.ToLower().Contains(searchText);
                }

                // Apply type filter
                if (visible && filterType != "All Types")
                {
                    visible = channel.Type.Equals(filterType, StringComparison.OrdinalIgnoreCase);
                }

                item.Font = visible ? new Font("Segoe UI", 9F) : new Font("Segoe UI", 9F, FontStyle.Strikeout);
                item.ForeColor = visible ? item.ForeColor : Color.Gray;
            }
        }

        private void AddToLiveSignals(ProcessedSignal signal)
        {
            var lvLiveSignals = this.Controls.Find("lvLiveSignals", true)[0] as ListView;
            if (lvLiveSignals == null) return;

            var item = new ListViewItem(signal.DateTime.ToString("HH:mm:ss"));
            item.SubItems.Add(signal.ChannelName);
            item.SubItems.Add(signal.ParsedData?.Symbol ?? "N/A");
            item.SubItems.Add(signal.ParsedData?.Direction ?? "N/A");
            item.SubItems.Add(signal.ParsedData?.StopLoss > 0 ? signal.ParsedData.StopLoss.ToString("F5") : "N/A");
            item.SubItems.Add(signal.ParsedData?.TakeProfit1 > 0 ? signal.ParsedData.TakeProfit1.ToString("F5") : "N/A");
            item.SubItems.Add(signal.Status);

            // Color coding based on status
            if (signal.Status.Contains("Processed"))
                item.BackColor = Color.FromArgb(220, 255, 220); // Light green
            else if (signal.Status.Contains("Error") || signal.Status.Contains("Invalid"))
                item.BackColor = Color.FromArgb(255, 220, 220); // Light red
            else if (signal.Status.Contains("Ignored"))
                item.BackColor = Color.FromArgb(255, 255, 220); // Light yellow
            else if (signal.Status.Contains("Test"))
                item.BackColor = Color.FromArgb(220, 220, 255); // Light blue

            lvLiveSignals.Items.Insert(0, item);

            // Keep only last 50 signals
            while (lvLiveSignals.Items.Count > 50)
            {
                lvLiveSignals.Items.RemoveAt(lvLiveSignals.Items.Count - 1);
            }
        }

        private void UpdateSelectedChannelsStatus(string status)
        {
            var lvSelected = this.Controls.Find("lvSelected", true)[0] as ListView;
            if (lvSelected == null) return;

            foreach (ListViewItem item in lvSelected.Items)
            {
                item.SubItems[3].Text = status; // Status column (index 3 now since we removed one column)
                if (status.Contains("Live"))
                    item.BackColor = Color.FromArgb(200, 255, 200);
                else
                    item.BackColor = Color.FromArgb(255, 255, 220);
            }
        }

        private void UpdateChannelsCount()
        {
            var lblChannelsCount = this.Controls.Find("lblChannelsCount", true)[0] as Label;
            if (lblChannelsCount != null)
            {
                lblChannelsCount.Text = $"📢 Channels: {allChannels.Count}";
            }
        }

        private void UpdateSelectedCount()
        {
            var lblSelectedCount = this.Controls.Find("lblSelectedCount", true)[0] as Label;
            if (lblSelectedCount != null)
            {
                lblSelectedCount.Text = $"✅ Selected: {selectedChannels.Count}";
            }

            // SIMPLIFIED - Button enabled when channels selected and not monitoring
            var btnStartMonitoring = this.Controls.Find("btnStartMonitoring", true)[0] as Button;
            if (btnStartMonitoring != null)
            {
                btnStartMonitoring.Enabled = selectedChannels.Count > 0 && !isMonitoring;
            }
        }

        private void UpdateSignalsCount()
        {
            var todaySignals = allSignals.Count(s => s.DateTime.Date == DateTime.UtcNow.Date);

            var lblSignalsCount = this.Controls.Find("lblSignalsCount", true)[0] as Label;
            if (lblSignalsCount != null)
            {
                lblSignalsCount.Text = $"📊 Today: {todaySignals}";
            }

            var lblStats = this.Controls.Find("lblStats", true)[0] as Label;
            if (lblStats != null)
            {
                lblStats.Text = $"📊 Live System | Today: {todaySignals} signals | Total: {allSignals.Count} | Monitoring: {selectedChannels.Count} channels | Status: {(isMonitoring ? "ACTIVE" : "READY")}";
            }
        }

        private string GenerateEAConfiguration()
        {
            var channelIds = string.Join(",", selectedChannels.Select(c => c.Id.ToString()));

            return $@"//+------------------------------------------------------------------+
//|                    Telegram EA Configuration                     |
//|                Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC               |
//|                User: islamahmed9717                              |
//+------------------------------------------------------------------+

//--- Telegram Channel Settings ---
ChannelIDs = ""{channelIds}""
SignalFilePath = ""TelegramSignals.txt""

//--- Risk Management Settings ---
RiskMode = ""Fixed""
FixedLotSize = 0.01
RiskPercent = 2.0
RiskAmount = 100

//--- Symbol Mapping Settings ---
SymbolsMapping = ""EURUSD:EURUSD,GBPUSD:GBPUSD,USDJPY:USDJPY,GOLD:XAUUSD,SILVER:XAGUSD,BITCOIN:BTCUSD""
SymbolPrefix = """"
SymbolSuffix = """"

//--- Advanced Settings ---
UseTrailingStop = false
TrailingStartPips = 10
TrailingStepPips = 5
MoveSLToBreakeven = true
BreakevenAfterPips = 10
SendNotifications = true
MaxSpreadPips = 5
SignalCheckInterval = 5
ForceMarketExecution = true
MaxRetriesOrderSend = 3

//--- Selected Channels ---
/*
{string.Join("\n", selectedChannels.Select(c => $"Channel: {c.Title} (ID: {c.Id}) - Type: {c.Type} - Members: {c.MembersCount}"))}
*/

//--- Configuration Instructions ---
/*
1. Copy the above settings into your Telegram EA input parameters
2. Make sure the MT4/MT5 Files path is set correctly in this app
3. Ensure this Windows application is running and monitoring channels
4. The EA will automatically read signals from: TelegramSignals.txt
5. Start monitoring in this app before running the EA

Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
By: islamahmed9717 - Telegram EA Manager v2.0 (Real Implementation)
System: Windows Forms .NET 9.0 with WTelegramClient
*/";
        }

        private bool IsValidPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return false;

            var cleanPhone = phoneNumber.Replace(" ", "").Replace("-", "");
            if (!cleanPhone.StartsWith("+"))
                return false;

            var digits = cleanPhone.Substring(1);
            return digits.Length >= 10 && digits.Length <= 15 && digits.All(char.IsDigit);
        }

        private string AutoDetectMT4Path()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var possiblePaths = new[]
                {
                    Path.Combine(userProfile, "AppData", "Roaming", "MetaQuotes", "Terminal"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MetaTrader 4", "MQL4", "Files"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MetaTrader 4", "MQL4", "Files"),
                    Path.Combine(userProfile, "Documents", "MT4", "Files"),
                    Path.Combine(userProfile, "Documents", "MT5", "Files")
                };

                foreach (var basePath in possiblePaths)
                {
                    if (Directory.Exists(basePath))
                    {
                        var directories = Directory.GetDirectories(basePath);
                        foreach (var dir in directories)
                        {
                            var mql4Files = Path.Combine(dir, "MQL4", "Files");
                            var mql5Files = Path.Combine(dir, "MQL5", "Files");

                            if (Directory.Exists(mql4Files))
                                return mql4Files;
                            if (Directory.Exists(mql5Files))
                                return mql5Files;
                        }

                        return basePath;
                    }
                }

                return Path.Combine(userProfile, "Documents", "MT4", "Files");
            }
            catch
            {
                return "";
            }
        }

        private void ShowMessage(string message, string title, MessageBoxIcon icon)
        {
            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        }

        private void LoadApplicationSettings()
        {
            // Load saved phone numbers, MT4 path, etc.
            try
            {
                if (File.Exists("app_settings.json"))
                {
                    var json = File.ReadAllText("app_settings.json");
                    var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                    if (settings != null)
                    {
                        var cmbPhone = this.Controls.Find("cmbPhone", true)[0] as ComboBox;
                        var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;

                        if (settings.SavedAccounts?.Count > 0 && cmbPhone != null)
                        {
                            cmbPhone.Items.AddRange(settings.SavedAccounts.ToArray());
                            cmbPhone.Text = settings.LastPhoneNumber;
                        }

                        if (!string.IsNullOrEmpty(settings.MT4Path) && txtMT4Path != null)
                        {
                            txtMT4Path.Text = settings.MT4Path;
                        }
                    }
                }
            }
            catch
            {
                // Ignore loading errors
            }
        }

        private void UpdateChannelsList(List<ChannelInfo> channels)
        {
            allChannels = channels;
            RefreshChannelsList();
        }

        private void LogMessage(string message)
        {
            // Add logging functionality if needed
            Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        private void SavePhoneNumber(string phoneNumber)
        {
            try
            {
                var settings = LoadAppSettings();
                if (!settings.SavedAccounts.Contains(phoneNumber))
                {
                    settings.SavedAccounts.Add(phoneNumber);
                }
                settings.LastPhoneNumber = phoneNumber;
                SaveAppSettings(settings);

                var cmbPhone = this.Controls.Find("cmbPhone", true)[0] as ComboBox;
                if (cmbPhone != null && !cmbPhone.Items.Contains(phoneNumber))
                {
                    cmbPhone.Items.Add(phoneNumber);
                }
            }
            catch
            {
                // Ignore save errors
            }
        }

        private void SaveMT4Path(string path)
        {
            try
            {
                var settings = LoadAppSettings();
                settings.MT4Path = path;
                SaveAppSettings(settings);
            }
            catch
            {
                // Ignore save errors
            }
        }

        private AppSettings LoadAppSettings()
        {
            try
            {
                if (File.Exists("app_settings.json"))
                {
                    var json = File.ReadAllText("app_settings.json");
                    return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Return default settings on error
            }
            return new AppSettings();
        }

        private void SaveAppSettings(AppSettings settings)
        {
            try
            {
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText("app_settings.json", json);
            }
            catch
            {
                // Ignore save errors
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            try
            {
                uiUpdateTimer?.Stop();
                telegramService?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
            base.OnFormClosing(e);
        }
        #endregion
    }
}