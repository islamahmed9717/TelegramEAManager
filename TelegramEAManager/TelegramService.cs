using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using TeleSharp.TL;
using TeleSharp.TL.Messages;
using TLSharp.Core;

namespace TelegramEAManager
{
    public class TelegramService
    {
        private TelegramClient client;
        private int apiId;
        private string apiHash;
        private string verificationHash = null;
        private int lastMessageId = 0;

        public TelegramService()
        {
            LoadApiCredentials();
        }

        private void LoadApiCredentials()
        {
            // Try to load from settings file first
            if (TryLoadFromSettingsFile())
                return;

            // Try to load from App.config
            if (TryLoadFromAppConfig())
                return;

            // If neither works, show setup dialog
            ShowApiSetupDialog();
        }

        private bool TryLoadFromSettingsFile()
        {
            try
            {
                string settingsFile = "telegram_api.json";
                if (File.Exists(settingsFile))
                {
                    var json = File.ReadAllText(settingsFile);
                    var settings = JsonConvert.DeserializeObject<ApiSettings>(json);

                    if (settings != null && settings.ApiId > 0 && !string.IsNullOrEmpty(settings.ApiHash))
                    {
                        apiId = settings.ApiId;
                        apiHash = settings.ApiHash;
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore errors, try next method
            }
            return false;
        }

        private bool TryLoadFromAppConfig()
        {
            try
            {
                var apiIdStr = ConfigurationManager.AppSettings["TelegramApiId"];
                var apiHashStr = ConfigurationManager.AppSettings["TelegramApiHash"];

                if (!string.IsNullOrEmpty(apiIdStr) && !string.IsNullOrEmpty(apiHashStr))
                {
                    apiId = int.Parse(apiIdStr);
                    apiHash = apiHashStr;
                    return true;
                }
            }
            catch
            {
                // Ignore errors, show setup dialog
            }
            return false;
        }

        private void ShowApiSetupDialog()
        {
            using (var setupForm = new Form())
            {
                setupForm.Text = "🔑 Telegram API Setup - islamahmed9717";
                setupForm.Size = new Size(600, 500);
                setupForm.StartPosition = FormStartPosition.CenterScreen;
                setupForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                setupForm.MaximizeBox = false;
                setupForm.MinimizeBox = false;
                setupForm.BackColor = Color.White;

                // Title
                var lblTitle = new Label
                {
                    Text = "🔑 TELEGRAM API CREDENTIALS SETUP",
                    Location = new Point(20, 20),
                    Size = new Size(550, 30),
                    Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(37, 99, 235),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                setupForm.Controls.Add(lblTitle);

                // Instructions
                var lblInstructions = new Label
                {
                    Text = "📋 FOLLOW THESE STEPS TO GET YOUR API CREDENTIALS:",
                    Location = new Point(20, 60),
                    Size = new Size(550, 25),
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(249, 115, 22)
                };
                setupForm.Controls.Add(lblInstructions);

                // Step-by-step instructions
                var instructions = new TextBox
                {
                    Location = new Point(20, 90),
                    Size = new Size(550, 180),
                    Multiline = true,
                    ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Segoe UI", 10F),
                    Text = @"STEP 1: 🌐 Open your web browser and go to: https://my.telegram.org

STEP 2: 📱 Login with your phone number (same as you'll use in this app)

STEP 3: 🔐 Enter the verification code sent to your Telegram app

STEP 4: 🆕 Click ""API development tools""

STEP 5: 📝 Fill out the form:
   • App title: Telegram EA Manager
   • Short name: ea_manager_" + DateTime.Now.ToString("yyyyMMdd") + @"
   • Description: Trading signal manager for islamahmed9717
   • Platform: Desktop
   • URL: (leave empty)

STEP 6: ✅ Click ""Create application""

STEP 7: 📋 Copy the api_id (numbers) and api_hash (long string) below"
                };
                setupForm.Controls.Add(instructions);

                // API ID input
                var lblApiId = new Label
                {
                    Text = "📋 API ID (numbers only):",
                    Location = new Point(20, 290),
                    Size = new Size(200, 25),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                setupForm.Controls.Add(lblApiId);

                var txtApiId = new TextBox
                {
                    Location = new Point(230, 290),
                    Size = new Size(200, 25),
                    Font = new Font("Segoe UI", 11F),
                    PlaceholderText = "e.g. 1234567"
                };
                txtApiId.KeyPress += (s, e) => {
                    if (!char.IsDigit(e.KeyChar) && e.KeyChar != 8)
                        e.Handled = true;
                };
                setupForm.Controls.Add(txtApiId);

                // API Hash input
                var lblApiHash = new Label
                {
                    Text = "🔑 API Hash (long string):",
                    Location = new Point(20, 330),
                    Size = new Size(200, 25),
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold)
                };
                setupForm.Controls.Add(lblApiHash);

                var txtApiHash = new TextBox
                {
                    Location = new Point(230, 330),
                    Size = new Size(340, 25),
                    Font = new Font("Segoe UI", 11F),
                    PlaceholderText = "e.g. abcd1234efgh5678..."
                };
                setupForm.Controls.Add(txtApiHash);

                // Open website button
                var btnOpenWebsite = new Button
                {
                    Text = "🌐 OPEN TELEGRAM API WEBSITE",
                    Location = new Point(450, 290),
                    Size = new Size(120, 65),
                    BackColor = Color.FromArgb(34, 197, 94),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                };
                btnOpenWebsite.Click += (s, e) => {
                    try
                    {
                        System.Diagnostics.Process.Start("https://my.telegram.org");
                    }
                    catch
                    {
                        MessageBox.Show("Please manually open: https://my.telegram.org", "Open Browser", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                };
                setupForm.Controls.Add(btnOpenWebsite);

                // Save button
                var btnSave = new Button
                {
                    Text = "💾 SAVE & CONTINUE",
                    Location = new Point(200, 380),
                    Size = new Size(150, 40),
                    BackColor = Color.FromArgb(37, 99, 235),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    DialogResult = DialogResult.OK
                };
                setupForm.Controls.Add(btnSave);

                // Exit button
                var btnExit = new Button
                {
                    Text = "❌ EXIT APP",
                    Location = new Point(370, 380),
                    Size = new Size(120, 40),
                    BackColor = Color.FromArgb(220, 38, 38),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    DialogResult = DialogResult.Cancel
                };
                setupForm.Controls.Add(btnExit);

                // Validation
                btnSave.Click += (s, e) => {
                    if (string.IsNullOrEmpty(txtApiId.Text) || string.IsNullOrEmpty(txtApiHash.Text))
                    {
                        MessageBox.Show("❌ Please enter both API ID and API Hash!", "Missing Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!int.TryParse(txtApiId.Text, out int testApiId) || testApiId <= 0)
                    {
                        MessageBox.Show("❌ API ID must be a valid number!", "Invalid API ID", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (txtApiHash.Text.Length < 10)
                    {
                        MessageBox.Show("❌ API Hash seems too short. Please check!", "Invalid API Hash", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Save credentials
                    apiId = testApiId;
                    apiHash = txtApiHash.Text.Trim();
                    SaveApiCredentials();
                    setupForm.DialogResult = DialogResult.OK;
                };

                setupForm.AcceptButton = btnSave;
                setupForm.CancelButton = btnExit;

                var result = setupForm.ShowDialog();
                if (result != DialogResult.OK)
                {
                    // User cancelled, exit application
                    Environment.Exit(0);
                }
            }
        }

        private void SaveApiCredentials()
        {
            try
            {
                var settings = new ApiSettings
                {
                    ApiId = apiId,
                    ApiHash = apiHash,
                    SavedDate = DateTime.UtcNow,
                    Username = "islamahmed9717"
                };

                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText("telegram_api.json", json);

                MessageBox.Show("✅ API credentials saved successfully!\n\n🔐 Your credentials are stored securely in telegram_api.json\n📱 You won't need to enter them again!",
                               "Credentials Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"⚠️ Warning: Could not save credentials to file.\n\n{ex.Message}\n\nYou may need to enter them again next time.",
                               "Save Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Rest of your TelegramService methods remain the same...
        public async Task<bool> ConnectAsync()
        {
            if (client == null)
                client = new TelegramClient(apiId, apiHash);
            if (!client.IsUserAuthorized())
                await client.ConnectAsync();
            return client.IsUserAuthorized();
        }

        public async Task<bool> SendCodeAsync(string phoneNumber)
        {
            verificationHash = await client.SendCodeRequestAsync(phoneNumber);
            return !string.IsNullOrEmpty(verificationHash);
        }

        public async Task<bool> VerifyCodeAsync(string phoneNumber, string code)
        {
            var user = await client.MakeAuthAsync(phoneNumber, verificationHash, code);
            return client.IsUserAuthorized();
        }

        public bool IsUserAuthorized()
        {
            return client != null && client.IsUserAuthorized();
        }

        public async Task<List<ChannelInfo>> GetChannelsAsync()
        {
            var channels = new List<ChannelInfo>();
            var dialogs = await client.GetUserDialogsAsync();
            if (dialogs is TLDialogs fullDialogs)
            {
                foreach (var dialog in fullDialogs.Dialogs)
                {
                    if (dialog.Peer is TLPeerChannel peerChannel)
                    {
                        var channel = fullDialogs.Chats.OfType<TLChannel>()
                            .FirstOrDefault(c => c.Id == peerChannel.ChannelId);
                        if (channel != null)
                        {
                            channels.Add(new ChannelInfo
                            {
                                Id = channel.Id,
                                Title = channel.Title,
                                Username = channel.Username ?? "",
                                Type = channel.Megagroup ? "Supergroup" : "Channel",
                                MembersCount = 0,
                                AccessHash = channel.AccessHash ?? 0,
                                LastActivity = DateTime.UtcNow
                            });
                        }
                    }
                }
            }
            return channels;
        }

        public async Task<List<string>> PollChannelMessagesAsync(int channelId, long accessHash, int limit = 10)
        {
            var result = new List<string>();
            var inputChannel = new TLInputPeerChannel { ChannelId = channelId, AccessHash = accessHash };
            var history = await client.GetHistoryAsync(inputChannel, limit);

            if (history is TLChannelMessages channelHistory)
            {
                foreach (var msg in channelHistory.Messages.OfType<TLMessage>().OrderBy(m => m.Id))
                {
                    if (msg.Id > lastMessageId)
                    {
                        result.Add(msg.Message);
                        lastMessageId = msg.Id;
                    }
                }
            }
            return result;
        }
    }


 
}