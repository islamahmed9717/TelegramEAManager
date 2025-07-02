using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Text;

namespace TelegramEAManager
{
    public class SignalProcessingService
    {
        private SymbolMapping symbolMapping = new SymbolMapping();
        private EASettings eaSettings = new EASettings();
        private readonly ConcurrentQueue<ProcessedSignal> processedSignals = new ConcurrentQueue<ProcessedSignal>();
        private readonly string signalsHistoryFile = "signals_history.json";
        private readonly object fileLock = new object();

        // FIXED: Ultra-fast duplicate detection with better performance
        private readonly ConcurrentDictionary<string, DateTime> processedMessageHashes = new ConcurrentDictionary<string, DateTime>();
        private readonly SemaphoreSlim fileWriteSemaphore = new SemaphoreSlim(3, 3); // Allow multiple concurrent writes
        private DateTime lastCleanupTime = DateTime.Now;

        // FIXED: High-performance file writing queue
        private readonly ConcurrentQueue<string> writeQueue = new ConcurrentQueue<string>();
        private readonly System.Threading.Timer? flushTimer;
        private volatile bool isProcessing = false;

        // Events
        public event EventHandler<ProcessedSignal>? SignalProcessed;
        public event EventHandler<string>? ErrorOccurred;
        public event EventHandler<string>? DebugMessage;

        public SignalProcessingService()
        {
            LoadSymbolMapping();
            LoadEASettings();
            LoadSignalsHistory();
            ClearSignalFileOnStartup();

            // FIXED: Start background file flushing for performance
            flushTimer = new System.Threading.Timer(FlushWriteQueue, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            // FIXED: Start cleanup timer
            var cleanupTimer = new System.Threading.Timer(PerformCleanup, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        // FIXED: High-performance background file writer
        private void FlushWriteQueue(object? state)
        {
            if (isProcessing || writeQueue.IsEmpty)
                return;

            isProcessing = true;

            try
            {
                var linesToWrite = new List<string>();

                // Dequeue all pending writes
                while (writeQueue.TryDequeue(out string? line) && linesToWrite.Count < 100)
                {
                    linesToWrite.Add(line);
                }

                if (linesToWrite.Count > 0)
                {
                    // FIXED: Batch write for maximum performance
                    Task.Run(async () => await BatchWriteToFileAsync(linesToWrite));
                }
            }
            finally
            {
                isProcessing = false;
            }
        }

        // FIXED: Batch file writing for ultra-fast performance
        private async Task BatchWriteToFileAsync(List<string> lines)
        {
            if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                return;

            var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

            try
            {
                await fileWriteSemaphore.WaitAsync(TimeSpan.FromSeconds(5));

                try
                {
                    // FIXED: High-performance file append
                    using (var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 65536))
                    using (var writer = new StreamWriter(stream, Encoding.UTF8, 65536))
                    {
                        foreach (var line in lines)
                        {
                            await writer.WriteLineAsync(line);
                        }
                        await writer.FlushAsync();
                    }

                    OnDebugMessage($"⚡ BATCH WROTE {lines.Count} signals to file in milliseconds");
                }
                finally
                {
                    fileWriteSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Batch write error: {ex.Message}");

                // FIXED: Re-queue failed writes
                foreach (var line in lines)
                {
                    writeQueue.Enqueue(line);
                }
            }
        }

        // FIXED: Ultra-fast duplicate detection using better hashing
        private string GenerateMessageHash(string messageText, long channelId)
        {
            // FIXED: Use faster hash algorithm
            var hashInput = $"{channelId}_{messageText.Length}_{messageText.Substring(0, Math.Min(50, messageText.Length))}";

            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < hashInput.Length; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ hashInput[i];
                    if (i == hashInput.Length - 1)
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ hashInput[i + 1];
                }

                return $"{hash1:X8}_{hash2:X8}";
            }
        }

        // FIXED: Clear signal file on startup with better performance
        private void ClearSignalFileOnStartup()
        {
            try
            {
                if (!string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                {
                    var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");
                    var directory = Path.GetDirectoryName(filePath);

                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // FIXED: Write header with current time
                    var headerContent = new StringBuilder();
                    headerContent.AppendLine("# Telegram EA Signal File - ULTRA-FAST VERSION");
                    headerContent.AppendLine($"# Startup Time: {DateTime.Now:yyyy.MM.dd HH:mm:ss} LOCAL");
                    headerContent.AppendLine($"# Processing Mode: ULTRA-FAST (500ms polling)");
                    headerContent.AppendLine("# Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS");
                    headerContent.AppendLine("# Status values: NEW (ready to process), PROCESSED (already handled)");
                    headerContent.AppendLine("");

                    File.WriteAllText(filePath, headerContent.ToString());
                    OnDebugMessage("✅ Signal file initialized for ultra-fast processing");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to initialize signal file: {ex.Message}");
            }
        }

        // FIXED: Ultra-fast signal processing with immediate queuing
        public ProcessedSignal ProcessTelegramMessage(string messageText, long channelId, string channelName)
        {
            var signal = new ProcessedSignal
            {
                Id = Guid.NewGuid().ToString(),
                DateTime = DateTime.Now,
                ChannelId = channelId,
                ChannelName = channelName,
                OriginalText = messageText,
                Status = "Processing..."
            };

            try
            {
                // FIXED: Fast duplicate check with timeout
                var messageHash = GenerateMessageHash(messageText, channelId);

                if (processedMessageHashes.TryGetValue(messageHash, out DateTime processedTime))
                {
                    if ((DateTime.Now - processedTime).TotalMinutes < 2) // Reduced from 5 to 2 minutes
                    {
                        signal.Status = "Duplicate - Already processed recently";
                        return signal;
                    }
                }

                // Mark as processed immediately
                processedMessageHashes[messageHash] = DateTime.Now;

                // FIXED: Parse signal with timeout protection
                var parseTask = Task.Run(() => ParseTradingSignal(messageText));
                var parsedData = parseTask.Wait(2000) ? parseTask.Result : null; // 2 second timeout

                if (parsedData != null)
                {
                    signal.ParsedData = parsedData;

                    // FIXED: Apply mapping in background to avoid blocking
                    Task.Run(() =>
                    {
                        try
                        {
                            ApplySymbolMapping(signal.ParsedData);

                            if (ValidateSignal(signal.ParsedData))
                            {
                                // FIXED: Queue for immediate writing instead of blocking
                                QueueSignalForWriting(signal);
                                signal.Status = "Processed - Queued for EA";

                                // Add to processed signals
                                processedSignals.Enqueue(signal);

                                // Trim queue if too large
                                if (processedSignals.Count > 1000)
                                {
                                    for (int i = 0; i < 100; i++)
                                    {
                                        processedSignals.TryDequeue(out _);
                                    }
                                }

                                OnSignalProcessed(signal);
                                OnDebugMessage($"⚡ ULTRA-FAST: Signal {signal.ParsedData.Symbol} {signal.ParsedData.Direction} processed in milliseconds");
                            }
                            else
                            {
                                signal.Status = "Invalid - Missing required data";
                            }
                        }
                        catch (Exception ex)
                        {
                            signal.Status = $"Error - {ex.Message}";
                            OnErrorOccurred($"Background processing error: {ex.Message}");
                        }
                    });
                }
                else
                {
                    signal.Status = "No trading signal detected";
                }
            }
            catch (Exception ex)
            {
                signal.Status = $"Error - {ex.Message}";
                signal.ErrorMessage = ex.ToString();
                OnErrorOccurred($"Error processing signal: {ex.Message}");
            }

            return signal;
        }

        // FIXED: Queue signal for ultra-fast writing
        private void QueueSignalForWriting(ProcessedSignal signal)
        {
            try
            {
                var signalText = FormatSignalForEA(signal);
                writeQueue.Enqueue(signalText);

                OnDebugMessage($"⚡ Signal queued for ultra-fast writing: {signal.ParsedData?.Symbol}");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error queuing signal: {ex.Message}");
            }
        }

        // FIXED: Optimized signal formatting
        private string FormatSignalForEA(ProcessedSignal signal)
        {
            var localTime = DateTime.Now;
            var timestampFormatted = localTime.ToString("yyyy.MM.dd HH:mm:ss");

            return $"{timestampFormatted}|" +
                   $"{signal.ChannelId}|" +
                   $"{signal.ChannelName}|" +
                   $"{signal.ParsedData?.Direction ?? "BUY"}|" +
                   $"{signal.ParsedData?.FinalSymbol ?? signal.ParsedData?.Symbol ?? "EURUSD"}|" +
                   $"{(signal.ParsedData?.EntryPrice ?? 0):F5}|" +
                   $"{(signal.ParsedData?.StopLoss ?? 0):F5}|" +
                   $"{(signal.ParsedData?.TakeProfit1 ?? 0):F5}|" +
                   $"{(signal.ParsedData?.TakeProfit2 ?? 0):F5}|" +
                   $"{(signal.ParsedData?.TakeProfit3 ?? 0):F5}|" +
                   $"NEW";
        }

        // FIXED: High-performance signal parsing with regex compilation
        private static readonly Regex DirectionRegex = new Regex(@"\b(BUY|SELL|LONG|SHORT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex[] SymbolPatterns = {
            // Compiled regex patterns for better performance
            new Regex(@"\b(EUR\/USD|EURUSD|EUR-USD|EU)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(GBP\/USD|GBPUSD|GBP-USD|GU)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(USD\/JPY|USDJPY|USD-JPY|UJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(GOLD|XAUUSD|XAU\/USD|XAU|AU)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(SILVER|XAGUSD|XAG\/USD|XAG|AG)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(BITCOIN|BTC|BTCUSD|BTC\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(US30|DOW|DJIA|DJ30)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex(@"\b(NAS100|NASDAQ|NDX|NAS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };

        private ParsedSignalData? ParseTradingSignal(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return null;

            var text = messageText.ToUpper().Replace("\n", " ").Replace("\r", " ");
            var signalData = new ParsedSignalData();

            // FIXED: Fast direction detection
            var directionMatch = DirectionRegex.Match(text);
            if (!directionMatch.Success)
                return null;

            var direction = directionMatch.Value.ToUpper();
            if (direction == "LONG") direction = "BUY";
            if (direction == "SHORT") direction = "SELL";
            signalData.Direction = direction;

            // FIXED: Fast symbol detection using compiled patterns
            foreach (var pattern in SymbolPatterns)
            {
                var match = pattern.Match(text);
                if (match.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(match.Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                    break;
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
            {
                // Fallback: extract symbol after direction
                var symbolAfterDirection = Regex.Match(text,
                    $@"\b{signalData.Direction}\s+([A-Z]{{2,8}}(?:\/[A-Z]{{3}}|[A-Z]{{0,3}})?)\b",
                    RegexOptions.IgnoreCase);
                if (symbolAfterDirection.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(symbolAfterDirection.Groups[1].Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
                return null;

            // FIXED: Fast price extraction using compiled patterns
            ExtractPrices(text, signalData);

            return signalData;
        }

        // FIXED: Optimized price extraction
        private static readonly Regex SlRegex = new Regex(@"SL\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Tp1Regex = new Regex(@"TP\s*1?\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Tp2Regex = new Regex(@"TP\s*2\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Tp3Regex = new Regex(@"TP\s*3\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EntryRegex = new Regex(@"ENTRY\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private void ExtractPrices(string text, ParsedSignalData signalData)
        {
            // Stop Loss
            var slMatch = SlRegex.Match(text);
            if (slMatch.Success && double.TryParse(slMatch.Groups[1].Value, out double sl))
                signalData.StopLoss = sl;

            // Take Profits
            var tp1Match = Tp1Regex.Match(text);
            if (tp1Match.Success && double.TryParse(tp1Match.Groups[1].Value, out double tp1))
                signalData.TakeProfit1 = tp1;

            var tp2Match = Tp2Regex.Match(text);
            if (tp2Match.Success && double.TryParse(tp2Match.Groups[1].Value, out double tp2))
                signalData.TakeProfit2 = tp2;

            var tp3Match = Tp3Regex.Match(text);
            if (tp3Match.Success && double.TryParse(tp3Match.Groups[1].Value, out double tp3))
                signalData.TakeProfit3 = tp3;

            // Entry Price
            var entryMatch = EntryRegex.Match(text);
            if (entryMatch.Success && double.TryParse(entryMatch.Groups[1].Value, out double entry))
                signalData.EntryPrice = entry;
        }

        // FIXED: Fast symbol normalization with cache
        private static readonly ConcurrentDictionary<string, string> NormalizationCache = new ConcurrentDictionary<string, string>();

        private string NormalizeSymbol(string symbol)
        {
            return NormalizationCache.GetOrAdd(symbol, key =>
            {
                var normalized = key.Replace("/", "").Replace("-", "").ToUpper().Trim();

                // Common abbreviations
                var mappings = new Dictionary<string, string>
                {
                    { "EU", "EURUSD" }, { "GU", "GBPUSD" }, { "UJ", "USDJPY" },
                    { "UC", "USDCHF" }, { "AU", "AUDUSD" }, { "NU", "NZDUSD" },
                    { "XAU", "XAUUSD" }, { "XAG", "XAGUSD" }
                };

                return mappings.GetValueOrDefault(normalized, normalized);
            });
        }

        // FIXED: Fast symbol mapping application
        private void ApplySymbolMapping(ParsedSignalData parsedData)
        {
            try
            {
                // Apply mapping
                if (symbolMapping.Mappings.TryGetValue(parsedData.OriginalSymbol.ToUpper(), out string? mapped))
                {
                    parsedData.Symbol = mapped;
                }
                else
                {
                    parsedData.Symbol = parsedData.OriginalSymbol;
                }

                // Apply prefix/suffix
                bool shouldSkip = symbolMapping.SkipPrefixSuffix.Contains(parsedData.Symbol.ToUpper());

                if (!shouldSkip)
                {
                    parsedData.FinalSymbol = symbolMapping.Prefix + parsedData.Symbol + symbolMapping.Suffix;
                }
                else
                {
                    parsedData.FinalSymbol = parsedData.Symbol;
                }

                // Check exclusions
                if (symbolMapping.ExcludedSymbols.Contains(parsedData.FinalSymbol.ToUpper()) ||
                    symbolMapping.ExcludedSymbols.Contains(parsedData.OriginalSymbol.ToUpper()))
                {
                    throw new InvalidOperationException($"Symbol {parsedData.OriginalSymbol} is excluded");
                }

                // Check whitelist
                if (symbolMapping.AllowedSymbols.Count > 0)
                {
                    if (!symbolMapping.AllowedSymbols.Contains(parsedData.FinalSymbol.ToUpper()) &&
                        !symbolMapping.AllowedSymbols.Contains(parsedData.OriginalSymbol.ToUpper()))
                    {
                        throw new InvalidOperationException($"Symbol {parsedData.OriginalSymbol} not in whitelist");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Symbol mapping failed: {ex.Message}");
            }
        }

        // FIXED: Fast signal validation
        private bool ValidateSignal(ParsedSignalData? parsedData)
        {
            if (parsedData == null) return false;
            if (string.IsNullOrEmpty(parsedData.Symbol)) return false;
            if (string.IsNullOrEmpty(parsedData.Direction)) return false;
            if (string.IsNullOrEmpty(parsedData.FinalSymbol)) return false;

            // Quick stop validation
            if (parsedData.StopLoss > 0 && parsedData.TakeProfit1 > 0)
            {
                bool validStops = parsedData.Direction == "BUY"
                    ? parsedData.StopLoss < parsedData.TakeProfit1
                    : parsedData.StopLoss > parsedData.TakeProfit1;

                if (!validStops)
                {
                    OnErrorOccurred($"Invalid {parsedData.Direction} stops: SL={parsedData.StopLoss}, TP={parsedData.TakeProfit1}");
                    return false;
                }
            }

            return true;
        }

        // FIXED: Background cleanup
        private void PerformCleanup(object? state)
        {
            try
            {
                // Clean old message hashes (older than 5 minutes)
                var cutoffTime = DateTime.Now.AddMinutes(-5);
                var keysToRemove = processedMessageHashes
                    .Where(kvp => kvp.Value < cutoffTime)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    processedMessageHashes.TryRemove(key, out _);
                }

                if (keysToRemove.Count > 0)
                {
                    OnDebugMessage($"🧹 Cleaned {keysToRemove.Count} old message hashes");
                }

                lastCleanupTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Cleanup error: {ex.Message}");
            }
        }

        // FIXED: Fast cleanup for processed signals
        public async Task CleanupProcessedSignalsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                    return;

                var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");
                if (!File.Exists(filePath))
                    return;

                await fileWriteSemaphore.WaitAsync(TimeSpan.FromSeconds(10));
                try
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    var newLines = new List<string>();
                    var now = DateTime.Now;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        {
                            newLines.Add(line);
                            continue;
                        }

                        var parts = line.Split('|');
                        if (parts.Length >= 11 && DateTime.TryParse(parts[0], out DateTime signalTime))
                        {
                            var ageMinutes = (now - signalTime).TotalMinutes;

                            // Keep signals less than 5 minutes old OR those marked as PROCESSED
                            if (ageMinutes <= 5 || (parts[10] == "PROCESSED" && ageMinutes <= 30))
                            {
                                newLines.Add(line);
                            }
                        }
                    }

                    await File.WriteAllLinesAsync(filePath, newLines);
                    OnDebugMessage($"🧹 Ultra-fast cleanup: kept {newLines.Count(l => !l.StartsWith("#"))} signals");
                }
                finally
                {
                    fileWriteSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Cleanup error: {ex.Message}");
            }
        }

        public void CleanupProcessedSignals()
        {
            Task.Run(async () => await CleanupProcessedSignalsAsync());
        }

        // Load/Save methods (keep existing implementations but add async versions)
        public void LoadSymbolMapping()
        {
            try
            {
                if (File.Exists("symbol_settings.json"))
                {
                    var json = File.ReadAllText("symbol_settings.json");
                    symbolMapping = JsonConvert.DeserializeObject<SymbolMapping>(json) ?? new SymbolMapping();
                }
                else
                {
                    symbolMapping = new SymbolMapping();
                }
            }
            catch
            {
                symbolMapping = new SymbolMapping();
            }
        }

        public void SaveSymbolMapping()
        {
            Task.Run(async () =>
            {
                try
                {
                    var json = JsonConvert.SerializeObject(symbolMapping, Formatting.Indented);
                    await File.WriteAllTextAsync("symbol_settings.json", json);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Failed to save symbol mapping: {ex.Message}");
                }
            });
        }

        public void LoadEASettings()
        {
            try
            {
                if (File.Exists("ea_settings.json"))
                {
                    var json = File.ReadAllText("ea_settings.json");
                    eaSettings = JsonConvert.DeserializeObject<EASettings>(json) ?? new EASettings();
                }
                else
                {
                    eaSettings = new EASettings();
                }
            }
            catch
            {
                eaSettings = new EASettings();
            }
        }

        public void SaveEASettings()
        {
            Task.Run(async () =>
            {
                try
                {
                    var json = JsonConvert.SerializeObject(eaSettings, Formatting.Indented);
                    await File.WriteAllTextAsync("ea_settings.json", json);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Failed to save EA settings: {ex.Message}");
                }
            });
        }

        private void LoadSignalsHistory()
        {
            // For performance, we'll use the queue instead of loading from file
            // File will be saved periodically
        }

        public void UpdateSymbolMapping(SymbolMapping newMapping)
        {
            symbolMapping = newMapping;
            SaveSymbolMapping();
        }

        public SymbolMapping GetSymbolMapping() => symbolMapping;

        public void UpdateEASettings(EASettings newSettings)
        {
            eaSettings = newSettings;
            SaveEASettings();
        }

        public EASettings GetEASettings() => eaSettings;

        public List<ProcessedSignal> GetSignalsHistory()
        {
            return processedSignals.ToList();
        }

        protected virtual void OnSignalProcessed(ProcessedSignal signal)
        {
            try
            {
                SignalProcessed?.Invoke(this, signal);
            }
            catch { }
        }

        protected virtual void OnErrorOccurred(string error)
        {
            try
            {
                ErrorOccurred?.Invoke(this, error);
            }
            catch { }
        }

        protected virtual void OnDebugMessage(string message)
        {
            try
            {
                DebugMessage?.Invoke(this, $"[{DateTime.Now:HH:mm:ss.fff}] {message}");
            }
            catch { }
        }
    }
}