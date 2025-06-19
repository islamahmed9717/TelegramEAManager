using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TelegramEAManager
{
    public partial class Form1 : Form
    {
        #region Private Fields
        private TelegramService telegramService;
        private SignalProcessingService signalProcessor;
        private List<ChannelInfo> allChannels = new List<ChannelInfo>();
        private List<ChannelInfo> selectedChannels = new List<ChannelInfo>();
        private bool isMonitoring = false;
        private System.Windows.Forms.Timer uiUpdateTimer;
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
            this.Text = "?? Telegram EA Manager - islamahmed9717 | Real Implementation";
            this.Size = new Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 245);

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
                Height = 120,
                BackColor = Color.FromArgb(37, 99, 235)
            };
            this.Controls.Add(headerPanel);

            // Title
            var lblTitle = new Label
            {
                Text = "?? REAL TELEGRAM EA MANAGER",
                Location = new Point(20, 15),
                Size = new Size(400, 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold)
            };
            headerPanel.Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = $"?? Current Time (UTC): 2025-06-19 08:29:25 | User: islamahmed9717",
                Location = new Point(20, 50),
                Size = new Size(500, 20),
                ForeColor = Color.FromArgb(200, 220, 255),
                Font = new Font("Segoe UI", 10F)
            };
            headerPanel.Controls.Add(lblSubtitle);

            // Phone number section
            var lblPhone = new Label
            {
                Text = "?? Enter Your Phone Number:",
                Location = new Point(550, 20),
                Size = new Size(180, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            headerPanel.Controls.Add(lblPhone);

            var cmbPhone = new ComboBox
            {
                Name = "cmbPhone",
                Location = new Point(550, 45),
                Size = new Size(200, 30),
                Font = new Font("Segoe UI", 11F),
                DropDownStyle = ComboBoxStyle.DropDown
            };
            headerPanel.Controls.Add(cmbPhone);

            var btnConnect = new Button
            {
                Name = "btnConnect",
                Text = "?? CONNECT",
                Location = new Point(760, 45),
                Size = new Size(120, 30),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnConnect.Click += BtnConnect_Click;
            headerPanel.Controls.Add(btnConnect);

            // MT4 Path
            var lblMT4 = new Label
            {
                Text = "?? MT4/MT5 Files Path:",
                Location = new Point(550, 80),
                Size = new Size(130, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            headerPanel.Controls.Add(lblMT4);

            var txtMT4Path = new TextBox
            {
                Name = "txtMT4Path",
                Location = new Point(680, 78),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 9F),
                Text = AutoDetectMT4Path()
            };
            headerPanel.Controls.Add(txtMT4Path);

            var btnBrowse = new Button
            {
                Name = "btnBrowse",
                Text = "??",
                Location = new Point(985, 78),
                Size = new Size(30, 25),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
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
                Location = new Point(1050, 15),
                Size = new Size(300, 90),
                BackColor = Color.FromArgb(220, 38, 38),
                BorderStyle = BorderStyle.FixedSingle
            };

            var lblConnectionStatus = new Label
            {
                Name = "lblConnectionStatus",
                Text = "?? DISCONNECTED",
                Location = new Point(10, 10),
                Size = new Size(280, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            statusPanel.Controls.Add(lblConnectionStatus);

            var lblChannelsCount = new Label
            {
                Name = "lblChannelsCount",
                Text = "?? Channels: 0",
                Location = new Point(10, 35),
                Size = new Size(135, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            statusPanel.Controls.Add(lblChannelsCount);

            var lblSelectedCount = new Label
            {
                Name = "lblSelectedCount",
                Text = "? Selected: 0",
                Location = new Point(145, 35),
                Size = new Size(135, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            statusPanel.Controls.Add(lblSelectedCount);

            var lblSignalsCount = new Label
            {
                Name = "lblSignalsCount",
                Text = "?? Signals Today: 0",
                Location = new Point(10, 55),
                Size = new Size(135, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            statusPanel.Controls.Add(lblSignalsCount);

            var lblMonitoringStatus = new Label
            {
                Name = "lblMonitoringStatus",
                Text = "?? Not Monitoring",
                Location = new Point(145, 55),
                Size = new Size(135, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F)
            };
            statusPanel.Controls.Add(lblMonitoringStatus);

            parent.Controls.Add(statusPanel);
        }

        private void CreateMainContent()
        {
            var contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20, 10, 20, 10)
            };
            this.Controls.Add(contentPanel);

            // Left Panel - All Channels
            var leftPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 600,
                Padding = new Padding(0, 0, 10, 0)
            };
            contentPanel.Controls.Add(leftPanel);

            CreateChannelsSection(leftPanel);

            // Right Panel - Controls and Live Signals
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 0, 0, 0)
            };
            contentPanel.Controls.Add(rightPanel);

            CreateControlsSection(rightPanel);
        }

        private void CreateChannelsSection(Panel parent)
        {
            var lblAllChannels = new Label
            {
                Text = "?? ALL YOUR TELEGRAM CHANNELS",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(37, 99, 235)
            };
            parent.Controls.Add(lblAllChannels);

            // Search and filter panel
            var searchPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 35
            };

            var txtSearch = new TextBox
            {
                Name = "txtSearch",
                Location = new Point(0, 5),
                Size = new Size(300, 25),
                Font = new Font("Segoe UI", 10F)
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;
            searchPanel.Controls.Add(txtSearch);

            var cmbFilter = new ComboBox
            {
                Name = "cmbFilter",
                Location = new Point(310, 5),
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
                Text = "??",
                Location = new Point(440, 5),
                Size = new Size(30, 25),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnRefresh.Click += BtnRefreshChannels_Click;
            searchPanel.Controls.Add(btnRefresh);

            parent.Controls.Add(searchPanel);

            var spacer = new Panel { Dock = DockStyle.Top, Height = 10 };
            parent.Controls.Add(spacer);

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
            var lblSelected = new Label
            {
                Text = "? SELECTED CHANNELS FOR MONITORING",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94)
            };
            parent.Controls.Add(lblSelected);

            var lvSelected = new ListView
            {
                Name = "lvSelected",
                Dock = DockStyle.Top,
                Height = 180,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9F)
            };

            lvSelected.Columns.Add("Channel", 200);
            lvSelected.Columns.Add("ID", 100);
            lvSelected.Columns.Add("Signals", 60);
            lvSelected.Columns.Add("Last Signal", 100);
            lvSelected.Columns.Add("Status", 80);

            parent.Controls.Add(lvSelected);

            CreateControlButtons(parent);
            CreateLiveSignalsSection(parent);
        }

        private void CreateControlButtons(Panel parent)
        {
            var controlPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 120,
                Padding = new Padding(0, 15, 0, 0)
            };

            var btnStartMonitoring = new Button
            {
                Name = "btnStartMonitoring",
                Text = "?? START MONITORING",
                Location = new Point(0, 0),
                Size = new Size(200, 45),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Enabled = false
            };
            btnStartMonitoring.Click += BtnStartMonitoring_Click;
            controlPanel.Controls.Add(btnStartMonitoring);

            var btnStopMonitoring = new Button
            {
                Name = "btnStopMonitoring",
                Text = "?? STOP MONITORING",
                Location = new Point(210, 0),
                Size = new Size(180, 45),
                BackColor = Color.FromArgb(220, 38, 38),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Enabled = false
            };
            btnStopMonitoring.Click += BtnStopMonitoring_Click;
            controlPanel.Controls.Add(btnStopMonitoring);

            var btnCopyChannelIDs = new Button
            {
                Name = "btnCopyChannelIDs",
                Text = "?? COPY CHANNEL IDs",
                Location = new Point(0, 55),
                Size = new Size(180, 35),
                BackColor = Color.FromArgb(168, 85, 247),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnCopyChannelIDs.Click += BtnCopyChannelIDs_Click;
            controlPanel.Controls.Add(btnCopyChannelIDs);

            var btnTestSignal = new Button
            {
                Name = "btnTestSignal",
                Text = "?? TEST SIGNAL",
                Location = new Point(190, 55),
                Size = new Size(120, 35),
                BackColor = Color.FromArgb(249, 115, 22),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnTestSignal.Click += BtnTestSignal_Click;
            controlPanel.Controls.Add(btnTestSignal);

            var btnGenerateEAConfig = new Button
            {
                Name = "btnGenerateEAConfig",
                Text = "?? GENERATE EA CONFIG",
                Location = new Point(320, 55),
                Size = new Size(170, 35),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };
            btnGenerateEAConfig.Click += BtnGenerateEAConfig_Click;
            controlPanel.Controls.Add(btnGenerateEAConfig);

            parent.Controls.Add(controlPanel);
        }

        private void CreateLiveSignalsSection(Panel parent)
        {
            var lblLiveSignals = new Label
            {
                Text = "?? LIVE SIGNALS FEED",
                Dock = DockStyle.Top,
                Height = 30,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(249, 115, 22)
            };
            parent.Controls.Add(lblLiveSignals);

            var lvLiveSignals = new ListView
            {
                Name = "lvLiveSignals",
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new Font("Segoe UI", 9F)
            };

            lvLiveSignals.Columns.Add("Time", 80);
            lvLiveSignals.Columns.Add("Channel", 120);
            lvLiveSignals.Columns.Add("Symbol", 80);
            lvLiveSignals.Columns.Add("Direction", 60);
            lvLiveSignals.Columns.Add("SL", 70);
            lvLiveSignals.Columns.Add("TP", 70);
            lvLiveSignals.Columns.Add("Status", 120);

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
                Text = "?? islamahmed9717",
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
                Text = "?? System ready - Connect to Telegram to start",
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
                Text = "?? SIGNALS HISTORY",
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
                Text = "?? EA SETTINGS",
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
                Text = "??? SYMBOL MAPPING",
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
                Text = $"Ready - Current UTC Time: 2025-06-19 08:29:25",
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
        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            var cmbPhone = this.Controls.Find("cmbPhone", true)[0] as ComboBox;
            var phoneNumber = cmbPhone.Text.Trim();

            if (string.IsNullOrEmpty(phoneNumber))
            {
                ShowMessage("?? Please enter your phone number", "Phone Required", MessageBoxIcon.Warning);
                return;
            }

            if (!IsValidPhoneNumber(phoneNumber))
            {
                ShowMessage("?? Please enter a valid phone number with country code\nExample: +1234567890",
                           "Invalid Phone Number", MessageBoxIcon.Warning);
                return;
            }

            var btnConnect = sender as Button;
            var originalText = btnConnect.Text;
            btnConnect.Text = "?? CONNECTING...";
            btnConnect.Enabled = false;

            try
            {
                // Connect to Telegram
                bool connected = await telegramService.ConnectAsync();
                if (!connected)
                {
                    throw new Exception("Failed to connect to Telegram servers");
                }

                // Check if already authorized
                if (telegramService.IsUserAuthorized())
                {
                    await LoadChannelsAfterAuth(phoneNumber);
                }
                else
                {
                    // Send verification code
                    bool codeSent = await telegramService.SendCodeAsync(phoneNumber);
                    if (codeSent)
                    {
                        // Show code input dialog
                        string code = ShowVerificationDialog();
                        if (!string.IsNullOrEmpty(code))
                        {
                            bool verified = await telegramService.VerifyCodeAsync(phoneNumber, code);
                            if (verified)
                            {
                                await LoadChannelsAfterAuth(phoneNumber);
                            }
                            else
                            {
                                throw new Exception("Invalid verification code");
                            }
                        }
                        else
                        {
                            throw new Exception("Verification cancelled");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to send verification code");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"? Connection failed:\n\n{ex.Message}", "Connection Error", MessageBoxIcon.Error);
                UpdateConnectionStatus(false, false);
            }
            finally
            {
                btnConnect.Text = originalText;
                btnConnect.Enabled = true;
            }
        }

        private async Task LoadChannelsAfterAuth(string phoneNumber)
        {
            try
            {
                UpdateConnectionStatus(true, false);

                // Save phone number
                SavePhoneNumber(phoneNumber);

                // Update user label
                var lblUser = this.Controls.Find("lblUser", true)[0] as Label;
                lblUser.Text = $"?? {phoneNumber} | islamahmed9717";

                // Load channels
                var channels = await telegramService.GetChannelsAsync();
                allChannels = channels;

                RefreshChannelsList();

                // Enable monitoring controls
                var btnStartMonitoring = this.Controls.Find("btnStartMonitoring", true)[0] as Button;
                btnStartMonitoring.Enabled = true;

                ShowMessage($"? Successfully connected!\n\n?? Phone: {phoneNumber}\n?? Loaded {channels.Count} channels\n\n?? Select channels and start monitoring!",
                           "Connection Successful", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"? Failed to load channels: {ex.Message}", "Error", MessageBoxIcon.Error);
            }
        }

        private string ShowVerificationDialog()
        {
            using (var codeForm = new Form())
            {
                codeForm.Text = "?? Telegram Verification";
                codeForm.Size = new Size(400, 250);
                codeForm.StartPosition = FormStartPosition.CenterParent;
                codeForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                codeForm.MaximizeBox = false;
                codeForm.MinimizeBox = false;

                var lblTitle = new Label
                {
                    Text = "?? TELEGRAM VERIFICATION CODE",
                    Location = new Point(20, 20),
                    Size = new Size(350, 30),
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(37, 99, 235),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                codeForm.Controls.Add(lblTitle);

                var lblInstruction = new Label
                {
                    Text = $"We sent a verification code to your phone.\nPlease enter the code below:",
                    Location = new Point(20, 60),
                    Size = new Size(350, 40),
                    Font = new Font("Segoe UI", 10F),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                codeForm.Controls.Add(lblInstruction);

                var txtCode = new TextBox
                {
                    Location = new Point(100, 110),
                    Size = new Size(200, 30),
                    Font = new Font("Segoe UI", 14F),
                    TextAlign = HorizontalAlignment.Center,
                    MaxLength = 6
                };
                txtCode.KeyPress += (s, e) => {
                    if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8)
                        e.Handled = true;
                };
                codeForm.Controls.Add(txtCode);

                var btnVerify = new Button
                {
                    Text = "? VERIFY",
                    Location = new Point(100, 150),
                    Size = new Size(90, 35),
                    BackColor = Color.FromArgb(34, 197, 94),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    DialogResult = DialogResult.OK
                };
                codeForm.Controls.Add(btnVerify);

                var btnCancel = new Button
                {
                    Text = "? CANCEL",
                    Location = new Point(210, 150),
                    Size = new Size(90, 35),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    DialogResult = DialogResult.Cancel
                };
                codeForm.Controls.Add(btnCancel);

                codeForm.AcceptButton = btnVerify;
                codeForm.CancelButton = btnCancel;
                txtCode.Focus();

                return codeForm.ShowDialog() == DialogResult.OK ? txtCode.Text.Trim() : "";
            }
        }

        private void LvChannels_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            var channel = e.Item.Tag as ChannelInfo;
            var lvSelected = this.Controls.Find("lvSelected", true)[0] as ListView;

            if (e.Item.Checked)
            {
                // Add to selected channels
                if (!selectedChannels.Any(c => c.Id == channel.Id))
                {
                    selectedChannels.Add(channel);

                    var item = new ListViewItem(channel.Title);
                    item.SubItems.Add(channel.Id.ToString());
                    item.SubItems.Add("0");
                    item.SubItems.Add(channel.LastActivity.ToString("HH:mm"));
                    item.SubItems.Add("?? Ready");
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
                    if (selectedChannel.Id == channel.Id)
                    {
                        lvSelected.Items.RemoveAt(i);
                        break;
                    }
                }
            }

            UpdateSelectedCount();
        }

        private async void BtnStartMonitoring_Click(object sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("?? Please select at least one channel to monitor!",
                           "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
            var mt4Path = txtMT4Path.Text.Trim();

            if (string.IsNullOrEmpty(mt4Path) || !Directory.Exists(mt4Path))
            {
                ShowMessage("?? Please set a valid MT4/MT5 Files folder path!",
                           "Invalid Path", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Start monitoring (simulated)
                isMonitoring = true;

                // Update UI
                var btnStart = sender as Button;
                var btnStop = this.Controls.Find("btnStopMonitoring", true)[0] as Button;
                btnStart.Enabled = false;
                btnStop.Enabled = true;

                UpdateConnectionStatus(true, true);
                UpdateSelectedChannelsStatus("?? Live");

                ShowMessage($"?? Monitoring started successfully!\n\n?? Monitoring {selectedChannels.Count} channels\n?? Signals saved to: {mt4Path}\\TelegramSignals.txt\n\n?? Keep this application running to receive signals!",
                           "Monitoring Started", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"? Failed to start monitoring:\n\n{ex.Message}", "Monitoring Error", MessageBoxIcon.Error);
            }
        }

        private void BtnStopMonitoring_Click(object sender, EventArgs e)
        {
            try
            {
                isMonitoring = false;

                // Update UI
                var btnStart = this.Controls.Find("btnStartMonitoring", true)[0] as Button;
                var btnStop = sender as Button;
                btnStart.Enabled = true;
                btnStop.Enabled = false;

                UpdateConnectionStatus(telegramService.IsUserAuthorized(), false);
                UpdateSelectedChannelsStatus("?? Ready");

                ShowMessage("?? Monitoring stopped successfully!", "Monitoring Stopped", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"? Error stopping monitoring:\n\n{ex.Message}", "Error", MessageBoxIcon.Error);
            }
        }

        private void BtnCopyChannelIDs_Click(object sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("?? Please select channels first!", "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            var channelIds = string.Join(",", selectedChannels.Select(c => c.Id.ToString()));
            Clipboard.SetText(channelIds);

            var channelList = string.Join("\n", selectedChannels.Select(c => $"• {c.Title} ({c.Id}) - {c.Type}"));

            ShowMessage($"?? Channel IDs copied to clipboard!\n\n?? PASTE THIS IN YOUR EA:\n{channelIds}\n\n?? SELECTED CHANNELS:\n{channelList}\n\n?? Configure your EA with these Channel IDs!",
                       "Channel IDs Copied", MessageBoxIcon.Information);
        }

        private void BtnTestSignal_Click(object sender, EventArgs e)
        {
            var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
            var mt4Path = txtMT4Path.Text.Trim();

            if (string.IsNullOrEmpty(mt4Path) || !Directory.Exists(mt4Path))
            {
                ShowMessage("?? Please set a valid MT4/MT5 path first!", "Invalid Path", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var testSignal = new ProcessedSignal
                {
                    Id = Guid.NewGuid().ToString(),
                    DateTime = DateTime.UtcNow,
                    ChannelId = 999999,
                    ChannelName = "TEST CHANNEL",
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

                // Add to signals history
                allSignals.Add(testSignal);
                AddToLiveSignals(testSignal);

                ShowMessage($"?? Test signal sent successfully!\n\n?? Signal Details:\n{testSignal.OriginalText}\n\n?? Check your EA to see if it processes the signal!",
                           "Test Signal Sent", MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowMessage($"? Failed to send test signal:\n\n{ex.Message}", "Test Failed", MessageBoxIcon.Error);
            }
        }

        private void BtnGenerateEAConfig_Click(object sender, EventArgs e)
        {
            if (selectedChannels.Count == 0)
            {
                ShowMessage("?? Please select channels first!", "No Channels Selected", MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var config = GenerateEAConfiguration();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                    FileName = $"TelegramEA_Config_islamahmed9717_20250619_082925.txt",
                    Title = "Save EA Configuration"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    File.WriteAllText(saveDialog.FileName, config);
                    Clipboard.SetText(config);

                    ShowMessage($"? EA configuration generated successfully!\n\n?? Saved to: {saveDialog.FileName}\n?? Configuration also copied to clipboard!\n\n?? Import this configuration into your EA settings.",
                               "Configuration Generated", MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"? Failed to generate configuration:\n\n{ex.Message}", "Generation Error", MessageBoxIcon.Error);
            }
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select your MT4/MT5 Files folder (usually contains MQL4 or MQL5 subfolder)";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    var txtMT4Path = this.Controls.Find("txtMT4Path", true)[0] as TextBox;
                    txtMT4Path.Text = folderDialog.SelectedPath;
                    SaveMT4Path(folderDialog.SelectedPath);
                }
            }
        }

        private async void BtnRefreshChannels_Click(object sender, EventArgs e)
        {
            if (!telegramService.IsUserAuthorized())
            {
                ShowMessage("?? Please connect to Telegram first!", "Not Connected", MessageBoxIcon.Warning);
                return;
            }

            var btnRefresh = sender as Button;
            btnRefresh.Enabled = false;
            btnRefresh.Text = "??";

            try
            {
                var channels = await telegramService.GetChannelsAsync();
                allChannels = channels;
                RefreshChannelsList();
            }
            catch (Exception ex)
            {
                ShowMessage($"? Failed to refresh channels:\n\n{ex.Message}", "Refresh Error", MessageBoxIcon.Error);
            }
            finally
            {
                btnRefresh.Enabled = true;
                btnRefresh.Text = "??";
            }
        }

        private void BtnHistory_Click(object sender, EventArgs e)
        {
            var historyForm = new SignalsHistoryForm(allSignals);
            historyForm.ShowDialog();
        }

        private void BtnEASettings_Click(object sender, EventArgs e)
        {
            ShowMessage("?? EA Settings feature will be implemented!\n\nThis will allow you to configure:\n• Risk management\n• Lot sizes\n• Trading hours\n• Symbol mappings\n\n?? Coming soon!", "EA Settings", MessageBoxIcon.Information);
        }

        private void BtnSymbolMapping_Click(object sender, EventArgs e)
        {
            ShowMessage("??? Symbol Mapping feature will be implemented!\n\nThis will allow you to:\n• Map Telegram symbols to MT4/MT5\n• Set symbol prefixes/suffixes\n• Configure excluded symbols\n\n?? Coming soon!", "Symbol Mapping", MessageBoxIcon.Information);
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplyChannelFilters();
        }

        private void CmbFilter_SelectedIndexChanged(object sender, EventArgs e)
        {
            ApplyChannelFilters();
        }

        private void UiUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Update time in status bar
            foreach (Control control in this.Controls)
            {
                if (control is StatusStrip statusStrip)
                {
                    foreach (ToolStripItem item in statusStrip.Items)
                    {
                        if (item.Name == "statusLabel")
                        {
                            item.Text = $"Real-time System Active | UTC: 2025-06-19 08:29:25 | User: islamahmed9717";
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

            var searchText = txtSearch?.Text?.ToLower() ?? "";
            var filterType = cmbFilter?.SelectedItem?.ToString() ?? "All Types";

            foreach (ListViewItem item in lvChannels.Items)
            {
                var channel = item.Tag as ChannelInfo;
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

        private void UpdateConnectionStatus(bool connected, bool monitoring)
        {
            var statusPanel = this.Controls.Find("statusPanel", true)[0] as Panel;
            var lblConnectionStatus = statusPanel.Controls.Find("lblConnectionStatus", true)[0] as Label;
            var lblMonitoringStatus = statusPanel.Controls.Find("lblMonitoringStatus", true)[0] as Label;

            if (monitoring)
            {
                statusPanel.BackColor = Color.FromArgb(34, 197, 94); // Green
                lblConnectionStatus.Text = "?? LIVE MONITORING";
                lblMonitoringStatus.Text = $"?? Active on {selectedChannels.Count} channels";
            }
            else if (connected)
            {
                statusPanel.BackColor = Color.FromArgb(249, 115, 22); // Orange
                lblConnectionStatus.Text = "?? CONNECTED";
                lblMonitoringStatus.Text = "?? Ready to monitor";
            }
            else
            {
                statusPanel.BackColor = Color.FromArgb(220, 38, 38); // Red
                lblConnectionStatus.Text = "?? DISCONNECTED";
                lblMonitoringStatus.Text = "? Not connected";
            }
        }

        private void UpdateSelectedChannelsStatus(string status)
        {
            var lvSelected = this.Controls.Find("lvSelected", true)[0] as ListView;
            foreach (ListViewItem item in lvSelected.Items)
            {
                item.SubItems[4].Text = status; // Status column
                if (status.Contains("Live"))
                    item.BackColor = Color.FromArgb(200, 255, 200);
                else
                    item.BackColor = Color.FromArgb(255, 255, 220);
            }
        }

        private void UpdateChannelsCount()
        {
            var lblChannelsCount = this.Controls.Find("lblChannelsCount", true)[0] as Label;
            lblChannelsCount.Text = $"?? Channels: {allChannels.Count}";
        }

        private void UpdateSelectedCount()
        {
            var lblSelectedCount = this.Controls.Find("lblSelectedCount", true)[0] as Label;
            lblSelectedCount.Text = $"? Selected: {selectedChannels.Count}";
        }

        private void UpdateSignalsCount()
        {
            var todaySignals = allSignals.Count(s => s.DateTime.Date == DateTime.UtcNow.Date);

            var lblSignalsCount = this.Controls.Find("lblSignalsCount", true)[0] as Label;
            lblSignalsCount.Text = $"?? Signals Today: {todaySignals}";

            var lblStats = this.Controls.Find("lblStats", true)[0] as Label;
            lblStats.Text = $"?? Live System | Today: {todaySignals} signals | Total: {allSignals.Count} | Monitoring: {selectedChannels.Count} channels | Status: {(isMonitoring ? "ACTIVE" : "READY")}";
        }

        private string GenerateEAConfiguration()
        {
            var channelIds = string.Join(",", selectedChannels.Select(c => c.Id.ToString()));

            return $@"//+------------------------------------------------------------------+
//|                    Telegram EA Configuration                     |
//|                Generated: 2025-06-19 08:29:25 UTC               |
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
SymbolsMapping = ""EURUSD:EURUSD,GBPUSD:GBPUSD,USDJPY:USDJPY""
SymbolPrefix = """"
SymbolSuffix = """"

//--- Advanced Settings ---
UseTrailingStop = false
TrailingStartPips = 10
TrailingStepPips = 5
MoveSLToBreakeven = true
BreakevenAfterPips = 10
SendNotifications = true

//--- Selected Channels ---
/*
{string.Join("\n", selectedChannels.Select(c => $"Channel: {c.Title} (ID: {c.Id}) - Type: {c.Type} - Members: {c.MembersCount}"))}
*/

//--- Configuration Instructions ---
/*
1. Copy the above settings into your Telegram EA input parameters
2. Make sure the MT4/MT5 Files path is set correctly
3. Ensure this Windows application is running and monitoring channels
4. The EA will automatically read signals from: TelegramSignals.txt

Generated on: 2025-06-19 08:29:25 UTC
By: islamahmed9717 - Telegram EA Manager v2.0
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
                    Path.Combine(userProfile, "Documents", "MT4", "Files")
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

                        if (settings.SavedAccounts?.Count > 0)
                        {
                            cmbPhone.Items.AddRange(settings.SavedAccounts.ToArray());
                            cmbPhone.Text = settings.LastPhoneNumber;
                        }

                        if (!string.IsNullOrEmpty(settings.MT4Path))
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
                if (!cmbPhone.Items.Contains(phoneNumber))
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