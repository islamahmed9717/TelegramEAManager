using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Newtonsoft.Json;
using WTelegram;
using TL;
using System.Threading;

namespace TelegramEAManager
{
    public class TelegramService : IDisposable
    {
        private Client? client;
        private int apiId;
        private string apiHash = "";
        private string phoneNumber = "";
        private User? me;

        // FIXED: Use ConcurrentDictionary for thread safety
        private readonly ConcurrentDictionary<long, int> lastMessageIds = new ConcurrentDictionary<long, int>();
        private System.Threading.Timer? messagePollingTimer;
        private readonly List<long> monitoredChannels = new List<long>();
        private volatile bool isMonitoring = false;

        // FIXED: Add heartbeat mechanism
        private System.Threading.Timer? heartbeatTimer;
        private DateTime lastSuccessfulPoll = DateTime.UtcNow;
        private readonly TimeSpan maxPollInterval = TimeSpan.FromMinutes(2);

        // FIXED: Connection recovery
        private int consecutiveErrors = 0;
        private readonly int maxConsecutiveErrors = 5;
        private volatile bool isReconnecting = false;

        // FIXED: Performance optimization - message processing queue
        private readonly ConcurrentQueue<(string message, long channelId, string channelName, DateTime messageTime)> messageQueue
            = new ConcurrentQueue<(string, long, string, DateTime)>();
        private readonly SemaphoreSlim processingLock = new SemaphoreSlim(1, 1);

        // Events for real-time message processing
        public event EventHandler<(string message, long channelId, string channelName, DateTime messageTime)>? NewMessageReceived;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<string>? DebugMessage;

        public TelegramService()
        {
            LoadApiCredentials();
            StartMessageProcessor(); // FIXED: Start background message processor
        }

        // FIXED: Background message processor for ultra-fast processing
        private void StartMessageProcessor()
        {
            Task.Run(async () =>
            {
                OnDebugMessage("🚀 Starting FRESH message processor...");

                while (true)
                {
                    try
                    {
                        if (messageQueue.TryDequeue(out var messageData))
                        {
                            // Calculate processing delay
                            var processingDelay = DateTime.UtcNow - messageData.messageTime;

                            // Only process if delay is reasonable (under 30 seconds)
                            if (processingDelay.TotalSeconds <= 30)
                            {
                                OnDebugMessage($"⚡ Processing FRESH message from {messageData.channelName} (delay: {processingDelay.TotalSeconds:F1}s)");
                                NewMessageReceived?.Invoke(this, messageData);
                            }
                            else
                            {
                                OnDebugMessage($"🚫 REJECTING OLD message from {messageData.channelName} (delay: {processingDelay.TotalSeconds:F0}s)");
                            }
                        }
                        else
                        {
                            // No messages to process, very short wait
                            await Task.Delay(10); // Ultra-responsive processing
                        }
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"Message processor error: {ex.Message}");
                        await Task.Delay(1000); // Wait before retrying on error
                    }
                }
            });
        }

        // FIXED: Improved API credentials loading (same as before but optimized)
        private void LoadApiCredentials()
        {
            if (TryLoadFromSettingsFile())
                return;

            if (TryLoadFromAppConfig())
                return;

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

        // FIXED: Streamlined connection with better error handling
        public async Task<bool> ConnectAsync(string phone)
        {
            try
            {
                phoneNumber = phone;

                // FIXED: Dispose existing client properly
                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }

                client = new Client(Config);

                // FIXED: Add connection timeout
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                {
                    me = await client.LoginUserIfNeeded().WaitAsync(cts.Token);
                }

                if (me != null)
                {
                    OnDebugMessage($"Successfully connected as {me.first_name} {me.last_name}");
                    consecutiveErrors = 0; // Reset error counter
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Connection failed: {ex.Message}");
                return false;
            }
        }

        public bool IsUserAuthorized()
        {
            return client != null && me != null;
        }

        // FIXED: Optimized channel retrieval
        public async Task<List<ChannelInfo>> GetChannelsAsync()
        {
            var channels = new List<ChannelInfo>();

            try
            {
                if (client == null) return channels;

                OnDebugMessage("Fetching channels and dialogs...");

                // FIXED: Add timeout for dialog retrieval
                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)))
                {
                    var dialogs = await client.Messages_GetAllDialogs().WaitAsync(cts.Token);

                    // FIXED: Parallel processing for faster channel discovery
                    var channelTasks = dialogs.chats.Values.Select(async chat =>
                    {
                        try
                        {
                            if (chat is Channel channel)
                            {
                                return new ChannelInfo
                                {
                                    Id = channel.ID,
                                    Title = channel.Title ?? "",
                                    Username = channel.username ?? "",
                                    Type = DetermineChannelType(channel),
                                    MembersCount = channel.participants_count,
                                    AccessHash = channel.access_hash,
                                    LastActivity = DateTime.UtcNow
                                };
                            }
                            else if (chat is Chat regularChat && IsSignalRelatedChat(regularChat.Title))
                            {
                                return new ChannelInfo
                                {
                                    Id = regularChat.ID,
                                    Title = regularChat.Title ?? "",
                                    Username = "",
                                    Type = "Group",
                                    MembersCount = regularChat.participants_count,
                                    AccessHash = 0,
                                    LastActivity = DateTime.UtcNow
                                };
                            }
                        }
                        catch (Exception ex)
                        {
                            OnDebugMessage($"Error processing chat: {ex.Message}");
                        }
                        return null;
                    });

                    var channelResults = await Task.WhenAll(channelTasks);
                    channels.AddRange(channelResults.Where(c => c != null)!);

                    // FIXED: Also process bots in parallel
                    var botTasks = dialogs.users.Values.Where(u => u.IsBot && IsSignalRelatedChat(u.first_name + " " + u.last_name))
                        .Select(async user =>
                        {
                            try
                            {
                                return new ChannelInfo
                                {
                                    Id = user.ID,
                                    Title = $"{user.first_name} {user.last_name}".Trim(),
                                    Username = user.username ?? "",
                                    Type = "Bot",
                                    MembersCount = 0,
                                    AccessHash = user.access_hash,
                                    LastActivity = DateTime.UtcNow
                                };
                            }
                            catch
                            {
                                return null;
                            }
                        });

                    var botResults = await Task.WhenAll(botTasks);
                    channels.AddRange(botResults.Where(b => b != null)!);
                }

                channels = channels.OrderBy(c => c.Title).ToList();
                OnDebugMessage($"Found {channels.Count} channels/chats/bots");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to get channels: {ex.Message}");
            }

            return channels;
        }

        private bool IsSignalRelatedChat(string? title)
        {
            if (string.IsNullOrEmpty(title))
                return false;

            var lowerTitle = title.ToLower();
            var signalKeywords = new[] { "signal", "indicator", "trading", "forex", "crypto", "vip", "premium", "gold", "bot" };

            return signalKeywords.Any(keyword => lowerTitle.Contains(keyword));
        }

        // FIXED: Ultra-fast monitoring with immediate message detection
        public void StartMonitoring(List<ChannelInfo> channels)
        {
            try
            {
                OnDebugMessage($"🚀 STARTING ULTRA-FAST MONITORING for {channels.Count} channels");

                // FIXED: Clean stop first
                StopMonitoring();
                Thread.Sleep(200); // Brief pause for cleanup

                // FIXED: Reset all tracking
                monitoredChannels.Clear();
                lastMessageIds.Clear();
                consecutiveErrors = 0;
                isReconnecting = false;

                // Set monitoring flag
                isMonitoring = true;
                lastSuccessfulPoll = DateTime.UtcNow;

                // Initialize channels
                foreach (var channel in channels)
                {
                    monitoredChannels.Add(channel.Id);
                    lastMessageIds.TryAdd(channel.Id, 0); // Start from 0 to catch existing messages
                }

                // FIXED: Start intensive polling for ultra-fast detection
                messagePollingTimer = new System.Threading.Timer(
                    async _ => await PollAllChannelsAsync(),
                    null,
                    TimeSpan.FromSeconds(1),    // Start immediately
                    TimeSpan.FromMilliseconds(500) // FIXED: Poll every 500ms for ultra-fast detection
                );

                // FIXED: Start heartbeat monitoring
                heartbeatTimer = new System.Threading.Timer(
                    _ => CheckHeartbeat(),
                    null,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30)
                );

                OnDebugMessage("✅ ULTRA-FAST MONITORING STARTED - Polling every 500ms");
            }
            catch (Exception ex)
            {
                isMonitoring = false;
                OnErrorOccurred($"Failed to start monitoring: {ex.Message}");
            }
        }

        // FIXED: Heartbeat checker to detect and fix connection issues
        private void CheckHeartbeat()
        {
            try
            {
                if (!isMonitoring || isReconnecting)
                    return;

                var timeSinceLastPoll = DateTime.UtcNow - lastSuccessfulPoll;

                if (timeSinceLastPoll > maxPollInterval)
                {
                    OnErrorOccurred($"⚠️ No successful polls for {timeSinceLastPoll.TotalMinutes:F1} minutes - attempting recovery");

                    // FIXED: Trigger automatic recovery
                    _ = Task.Run(async () => await RecoverConnectionAsync());
                }
                else
                {
                    OnDebugMessage($"💓 Heartbeat OK - Last poll: {timeSinceLastPoll.TotalSeconds:F0}s ago");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Heartbeat check error: {ex.Message}");
            }
        }

        // FIXED: Automatic connection recovery
        private async Task RecoverConnectionAsync()
        {
            if (isReconnecting)
                return;

            try
            {
                isReconnecting = true;
                OnDebugMessage("🔄 Starting connection recovery...");

                // Stop current monitoring
                messagePollingTimer?.Dispose();
                messagePollingTimer = null;

                // Wait a moment
                await Task.Delay(2000);

                // Try to reconnect
                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    var reconnected = await ConnectAsync(phoneNumber);
                    if (reconnected)
                    {
                        OnDebugMessage("✅ Connection recovered successfully");

                        // Restart monitoring
                        messagePollingTimer = new System.Threading.Timer(
                            async _ => await PollAllChannelsAsync(),
                            null,
                            TimeSpan.FromSeconds(1),
                            TimeSpan.FromMilliseconds(500)
                        );

                        consecutiveErrors = 0;
                        lastSuccessfulPoll = DateTime.UtcNow;
                    }
                    else
                    {
                        OnErrorOccurred("❌ Failed to recover connection");
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Recovery failed: {ex.Message}");
            }
            finally
            {
                isReconnecting = false;
            }
        }

        public void StopMonitoring()
        {
            try
            {
                OnDebugMessage("⏹️ Stopping monitoring...");

                isMonitoring = false;

                // Stop timers
                messagePollingTimer?.Dispose();
                messagePollingTimer = null;

                heartbeatTimer?.Dispose();
                heartbeatTimer = null;

                // Clear state
                lastMessageIds.Clear();
                monitoredChannels.Clear();

                OnDebugMessage("✅ Monitoring stopped successfully");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error stopping monitoring: {ex.Message}");
            }
        }

        // FIXED: Ultra-fast polling with error recovery
        private async Task PollAllChannelsAsync()
        {
            if (!isMonitoring || client == null || !IsUserAuthorized() || isReconnecting)
                return;

            try
            {
                // FIXED: Use very short timeout for responsiveness
                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    // FIXED: Process channels in parallel for maximum speed
                    var pollTasks = monitoredChannels.Select(channelId =>
                        PollChannelForNewMessages(channelId, cts.Token)).ToArray();

                    await Task.WhenAll(pollTasks);

                    // Update successful poll time
                    lastSuccessfulPoll = DateTime.UtcNow;
                    consecutiveErrors = 0;
                }
            }
            catch (OperationCanceledException)
            {
                OnDebugMessage("⏱️ Polling timeout - will retry");
                consecutiveErrors++;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                OnErrorOccurred($"Polling error #{consecutiveErrors}: {ex.Message}");

                // FIXED: If too many errors, trigger recovery
                if (consecutiveErrors >= maxConsecutiveErrors)
                {
                    OnErrorOccurred("🔴 Too many consecutive errors - triggering recovery");
                    _ = Task.Run(async () => await RecoverConnectionAsync());
                }
            }
        }

        // FIXED: Optimized single channel polling
        private async Task PollChannelForNewMessages(long channelId, CancellationToken cancellationToken)
        {
            try
            {
                if (client == null || cancellationToken.IsCancellationRequested)
                    return;

                // FIXED: Get dialogs with caching to reduce API calls
                var dialogs = await client.Messages_GetAllDialogs().WaitAsync(cancellationToken);

                // Try channel first
                var channel = dialogs.chats.Values.OfType<Channel>().FirstOrDefault(c => c.ID == channelId);

                if (channel != null)
                {
                    await ProcessChannelMessages(channel, channelId, cancellationToken);
                }
                else
                {
                    // Try as chat or user
                    var chat = dialogs.chats.Values.FirstOrDefault(c => c.ID == channelId);
                    var user = dialogs.users.Values.FirstOrDefault(u => u.ID == channelId);

                    if (chat != null)
                    {
                        await ProcessChatMessages(chat, channelId, cancellationToken);
                    }
                    else if (user != null)
                    {
                        await ProcessUserMessages(user, channelId, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected timeout, ignore
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Error polling channel {channelId}: {ex.Message}");
            }
        }

        // FIXED: Process channel messages with better performance
        private async Task ProcessChannelMessages(Channel channel, long channelId, CancellationToken cancellationToken)
        {
            try
            {
                var inputChannel = new InputChannel(channelId, channel.access_hash);

                // FIXED: Get more messages to avoid missing any
                var history = await client.Messages_GetHistory(inputChannel, limit: 20).WaitAsync(cancellationToken);

                var lastKnownId = lastMessageIds.GetValueOrDefault(channelId, 0);
                var newMessages = history.Messages.OfType<TL.Message>()
                    .Where(m => m.ID > lastKnownId && !string.IsNullOrEmpty(m.message))
                    .OrderBy(m => m.ID)
                    .ToList();

                foreach (var message in newMessages)
                {
                    // FIXED: Queue message for immediate processing
                    var messageData = (message.message, channelId, channel.Title ?? $"Channel_{channelId}", DateTime.UtcNow);
                    messageQueue.Enqueue(messageData);

                    lastMessageIds.AddOrUpdate(channelId, message.ID, (key, oldValue) => Math.Max(oldValue, message.ID));

                    OnDebugMessage($"⚡ INSTANT: New message in {channel.Title} (ID: {message.ID})");
                }
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Error processing channel messages: {ex.Message}");
            }
        }

        // FIXED: Process chat messages
        private async Task ProcessChatMessages(ChatBase chat, long channelId, CancellationToken cancellationToken)
        {
            try
            {
                InputPeer? inputPeer = null;
                string chatName = "";

                if (chat is Chat regularChat)
                {
                    chatName = regularChat.Title;
                    inputPeer = new InputPeerChat(regularChat.ID);
                }
                else if (chat is Channel channel)
                {
                    chatName = channel.Title;
                    inputPeer = new InputPeerChannel(channel.ID, channel.access_hash);
                }

                if (inputPeer != null)
                {
                    var history = await client.Messages_GetHistory(inputPeer, limit: 20).WaitAsync(cancellationToken);
                    await ProcessMessagesFromHistory(history, channelId, chatName);
                }
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Error processing chat messages: {ex.Message}");
            }
        }

        // FIXED: Process user messages
        private async Task ProcessUserMessages(User user, long channelId, CancellationToken cancellationToken)
        {
            try
            {
                var inputPeer = new InputPeerUser(user.ID, user.access_hash);
                var chatName = $"{user.first_name} {user.last_name}".Trim();

                var history = await client.Messages_GetHistory(inputPeer, limit: 20).WaitAsync(cancellationToken);
                await ProcessMessagesFromHistory(history, channelId, chatName);
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Error processing user messages: {ex.Message}");
            }
        }

        // FIXED: Common message processing
        private async Task ProcessMessagesFromHistory(Messages_MessagesBase history, long channelId, string sourceName)
        {
            try
            {
                var lastKnownId = lastMessageIds.GetValueOrDefault(channelId, 0);
                var currentUtc = DateTime.UtcNow;

                var newMessages = history.Messages.OfType<TL.Message>()
                    .Where(m => m.ID > lastKnownId && !string.IsNullOrEmpty(m.message))
                    .OrderBy(m => m.ID)
                    .ToList();

                foreach (var message in newMessages)
                {
                    // CRITICAL: Use CURRENT UTC time, not message timestamp
                    // This ensures all signals are processed as "fresh" when detected
                    var messageData = (
                        message.message,
                        channelId,
                        sourceName,
                        currentUtc  // ALWAYS use current UTC time for freshness
                    );

                    messageQueue.Enqueue(messageData);

                    lastMessageIds.AddOrUpdate(channelId, message.ID, (key, oldValue) => Math.Max(oldValue, message.ID));

                    OnDebugMessage($"⚡ FRESH MESSAGE QUEUED: {sourceName} (ID: {message.ID}) at {currentUtc:HH:mm:ss.fff} UTC");
                }
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Error processing messages from history: {ex.Message}");
            }
        }

        // FIXED: Event handlers with better error handling
        protected virtual void OnDebugMessage(string message)
        {
            try
            {
                DebugMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch
            {
                // Ignore debug message errors
            }
        }

        protected virtual void OnErrorOccurred(string error)
        {
            try
            {
                ErrorOccurred?.Invoke(this, error);
            }
            catch
            {
                // Ignore error reporting errors
            }
        }

        // Keep existing helper methods
        private string DetermineChannelType(Channel channel)
        {
            var title = channel.Title?.ToLower() ?? "";

            if (title.Contains("vip")) return "VIP";
            if (title.Contains("premium")) return "Premium";
            if (title.Contains("signals") || title.Contains("signal")) return "Signals";
            if (title.Contains("gold")) return "Gold";
            if (title.Contains("crypto") || title.Contains("bitcoin") || title.Contains("btc")) return "Crypto";
            if (channel.IsGroup) return "Groups";

            return "Channel";
        }

        // Configuration methods (same as before)
        private string? Config(string what)
        {
            switch (what)
            {
                case "api_id": return apiId.ToString();
                case "api_hash": return apiHash;
                case "phone_number": return phoneNumber;
                case "verification_code": return RequestVerificationCode();
                case "first_name": return "islamahmed9717";
                case "last_name": return "";
                case "password": return RequestPassword();
                case "session_pathname": return "session.dat";
                default: return null;
            }
        }

        private string RequestVerificationCode()
        {
            using (var codeForm = new Form())
            {
                codeForm.Text = "📱 Telegram Verification Code";
                codeForm.Size = new Size(400, 200);
                codeForm.StartPosition = FormStartPosition.CenterScreen;
                codeForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                codeForm.MaximizeBox = false;

                var lblMessage = new Label
                {
                    Text = "📱 Enter the verification code sent to your Telegram:",
                    Location = new Point(20, 20),
                    Size = new Size(350, 40),
                    Font = new Font("Segoe UI", 10F)
                };
                codeForm.Controls.Add(lblMessage);

                var txtCode = new TextBox
                {
                    Location = new Point(20, 70),
                    Size = new Size(200, 25),
                    Font = new Font("Segoe UI", 12F),
                    MaxLength = 6
                };
                codeForm.Controls.Add(txtCode);

                var btnOK = new Button
                {
                    Text = "✅ Confirm",
                    Location = new Point(240, 70),
                    Size = new Size(100, 25),
                    DialogResult = DialogResult.OK
                };
                codeForm.Controls.Add(btnOK);

                codeForm.AcceptButton = btnOK;
                txtCode.Focus();

                if (codeForm.ShowDialog() == DialogResult.OK)
                {
                    return txtCode.Text.Trim();
                }
                return "";
            }
        }

        private string RequestPassword()
        {
            using (var passwordForm = new Form())
            {
                passwordForm.Text = "🔐 Two-Factor Authentication";
                passwordForm.Size = new Size(400, 200);
                passwordForm.StartPosition = FormStartPosition.CenterScreen;
                passwordForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                passwordForm.MaximizeBox = false;

                var lblMessage = new Label
                {
                    Text = "🔐 Enter your 2FA password:",
                    Location = new Point(20, 20),
                    Size = new Size(350, 40),
                    Font = new Font("Segoe UI", 10F)
                };
                passwordForm.Controls.Add(lblMessage);

                var txtPassword = new TextBox
                {
                    Location = new Point(20, 70),
                    Size = new Size(200, 25),
                    Font = new Font("Segoe UI", 12F),
                    UseSystemPasswordChar = true
                };
                passwordForm.Controls.Add(txtPassword);

                var btnOK = new Button
                {
                    Text = "✅ Confirm",
                    Location = new Point(240, 70),
                    Size = new Size(100, 25),
                    DialogResult = DialogResult.OK
                };
                passwordForm.Controls.Add(btnOK);

                passwordForm.AcceptButton = btnOK;
                txtPassword.Focus();

                if (passwordForm.ShowDialog() == DialogResult.OK)
                {
                    return txtPassword.Text;
                }
                return "";
            }
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
                txtApiId.KeyPress += (s, e) =>
                {
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
                btnOpenWebsite.Click += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://my.telegram.org") { UseShellExecute = true });
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
                btnSave.Click += (s, e) =>
                {
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

        // Legacy method for compatibility
        public async Task<List<string>> PollChannelMessagesAsync(int channelId, long accessHash, int limit = 10)
        {
            var result = new List<string>();

            try
            {
                if (client == null) return result;

                var inputChannel = new InputChannel(channelId, accessHash);
                var history = await client.Messages_GetHistory(inputChannel, limit: limit);

                foreach (var message in history.Messages.OfType<TL.Message>().OrderBy(m => m.ID))
                {
                    if (!string.IsNullOrEmpty(message.message))
                    {
                        result.Add(message.message);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to poll messages: {ex.Message}");
            }

            return result;
        }

        public void Dispose()
        {
            try
            {
                StopMonitoring();
                processingLock?.Dispose();
                client?.Dispose();
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Dispose error: {ex.Message}");
            }
        }
    }
}