using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Concurrent;

namespace TelegramEAManager
{
    public class SignalProcessingService
    {
        private SymbolMapping symbolMapping = new SymbolMapping();
        private EASettings eaSettings = new EASettings();
        private List<ProcessedSignal> processedSignals = new List<ProcessedSignal>();
        private readonly string signalsHistoryFile = "signals_history.json";
        private readonly object fileLock = new object();

        // CRITICAL: Track processed messages to avoid duplicates
        private readonly ConcurrentDictionary<string, DateTime> processedMessageHashes = new ConcurrentDictionary<string, DateTime>();
        private readonly SemaphoreSlim fileWriteSemaphore = new SemaphoreSlim(1, 1);
        private DateTime lastCleanupTime = DateTime.Now;

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
        }
        private string GenerateMessageHash(string messageText, long channelId)
        {
            // Create a more unique identifier using actual content hash
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes($"{channelId}_{messageText}");
                var hashBytes = md5.ComputeHash(inputBytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
        /// <summary>
        /// Clear signal file on startup to prevent old signals from being processed
        /// </summary>
        private void ClearSignalFileOnStartup()
        {
            try
            {
                if (!string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                {
                    var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

                    // Ensure directory exists
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Write header only
                    lock (fileLock)
                    {
                        using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                        {
                            writer.WriteLine("# Telegram EA Signal File - CLEARED ON STARTUP");
                            writer.WriteLine($"# Startup Time: {DateTime.UtcNow:yyyy.MM.dd HH:mm:ss} UTC");
                            writer.WriteLine("# Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS");
                            writer.WriteLine("# Status values: NEW (ready to process), PROCESSED (already handled)");
                            writer.WriteLine("");
                        }
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Signal file cleared on startup: {filePath}");
                    OnDebugMessage($"Signal file cleared on startup: {filePath}");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to clear signal file on startup: {ex.Message}");
            }
        }

        public ProcessedSignal ProcessTelegramMessage(string messageText, long channelId, string channelName)
        {
            OnDebugMessage($"Processing message from {channelName} (ID: {channelId})");
            OnDebugMessage($"Message text: {messageText.Substring(0, Math.Min(100, messageText.Length))}...");

            // Generate message hash to check for duplicates
            var messageHash = GenerateMessageHash(messageText, channelId);

            // Check if we've already processed this message recently (within 5 minutes)
            if (processedMessageHashes.TryGetValue(messageHash, out DateTime processedTime))
            {
                if ((DateTime.Now - processedTime).TotalMinutes < 5)
                {
                    OnDebugMessage("Message already processed recently - skipping");
                    // Return a dummy signal indicating it's a duplicate
                    return new ProcessedSignal
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateTime = DateTime.UtcNow,
                        ChannelId = channelId,
                        ChannelName = channelName,
                        OriginalText = messageText,
                        Status = "Duplicate - Already processed"
                    };
                }
            }

            // Mark this message as processed
            processedMessageHashes[messageHash] = DateTime.Now;

            // Clean up old message hashes periodically
            if ((DateTime.Now - lastCleanupTime).TotalMinutes > 10)
            {
                CleanupOldMessageHashes();
                lastCleanupTime = DateTime.Now;
            }

            var signal = new ProcessedSignal
            {
                Id = Guid.NewGuid().ToString(),
                DateTime = DateTime.UtcNow,
                ChannelId = channelId,
                ChannelName = channelName,
                OriginalText = messageText,
                Status = "Processing..."
            };

            try
            {
                OnDebugMessage("Parsing message for trading signals...");

                // Parse the message for trading signals
                var parsedData = ParseTradingSignal(messageText);
                if (parsedData != null)
                {
                    OnDebugMessage($"Signal parsed: {parsedData.Symbol} {parsedData.Direction}");
                    signal.ParsedData = parsedData;

                    // Apply symbol mapping
                    OnDebugMessage("Applying symbol mapping...");
                    ApplySymbolMapping(signal.ParsedData);
                    OnDebugMessage($"Symbol mapped: {parsedData.OriginalSymbol} -> {parsedData.FinalSymbol}");

                    // Validate signal
                    OnDebugMessage("Validating signal...");
                    if (ValidateSignal(signal.ParsedData))
                    {
                        OnDebugMessage("Signal validation passed - writing to file...");

                        // Write to EA file synchronously first, then async cleanup
                        bool fileWritten = WriteSignalToEAFileSync(signal);

                        if (fileWritten)
                        {
                            signal.Status = "Processed - Sent to EA";
                            OnDebugMessage("Signal successfully written to EA file");
                        }
                        else
                        {
                            signal.Status = "Error - Failed to write to file";
                            OnDebugMessage("Failed to write signal to EA file");
                        }

                        // Add to history
                        lock (processedSignals)
                        {
                            processedSignals.Add(signal);
                            // Keep only last 1000 signals in memory
                            if (processedSignals.Count > 1000)
                            {
                                processedSignals.RemoveRange(0, processedSignals.Count - 1000);
                            }
                        }

                        // Save history asynchronously
                        Task.Run(async () => await SaveSignalsHistoryAsync());

                        OnSignalProcessed(signal);
                    }
                    else
                    {
                        signal.Status = "Invalid - Missing required data";
                        OnDebugMessage("Signal validation failed");
                    }
                }
                else
                {
                    signal.Status = "No trading signal detected";
                    OnDebugMessage("No trading signal pattern detected in message");
                }
            }
            catch (Exception ex)
            {
                signal.Status = $"Error - {ex.Message}";
                signal.ErrorMessage = ex.ToString();
                OnErrorOccurred($"Error processing signal: {ex.Message}");
                OnDebugMessage($"Error processing signal: {ex}");
            }

            return signal;
        }
        private bool WriteSignalToEAFileSync(ProcessedSignal signal)
        {
            try
            {
                if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                {
                    OnDebugMessage("MT4 files path not configured");
                    throw new InvalidOperationException("MT4 files path not configured");
                }

                var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");
                var directory = Path.GetDirectoryName(filePath);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    OnDebugMessage($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }

                OnDebugMessage($"Writing signal to file: {filePath}");

                var signalText = FormatSignalForEA(signal);
                OnDebugMessage($"Signal formatted: {signalText}");

                // Use lock to prevent concurrent file access
                lock (fileLock)
                {
                    // Check for duplicate in file before writing
                    var existingLines = File.Exists(filePath) ? File.ReadAllLines(filePath) : Array.Empty<string>();

                    // Create a signature for this signal
                    var signalSignature = $"|{signal.ChannelId}|{signal.ChannelName}|{signal.ParsedData?.Direction ?? ""}|{signal.ParsedData?.Symbol ?? ""}|";

                    // Check if a similar signal was written in the last minute
                    var recentDuplicate = existingLines
                        .Where(line => !line.StartsWith("#") && line.Contains(signalSignature))
                        .Select(line => line.Split('|'))
                        .Where(parts => parts.Length > 0)
                        .Select(parts => DateTime.TryParse(parts[0], out var dt) ? dt : DateTime.MinValue)
                        .Any(signalTime => (DateTime.UtcNow - signalTime).TotalSeconds < 60);

                    if (!recentDuplicate)
                    {
                        // Append to file with proper encoding and flushing
                        using (var fs = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8))
                        {
                            writer.WriteLine(signalText);
                            writer.Flush();
                        }

                        OnDebugMessage($"Signal written to file successfully");
                        return true;
                    }
                    else
                    {
                        OnDebugMessage("Skipped duplicate signal in file");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to write signal to file: {ex.Message}");
                OnDebugMessage($"File write error: {ex}");
                return false;
            }
        }

        private void CleanupOldMessageHashes()
        {
            var cutoffTime = DateTime.Now.AddMinutes(-10);
            var keysToRemove = processedMessageHashes
                .Where(kvp => kvp.Value < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                processedMessageHashes.TryRemove(key, out _);
            }
        }
        private async Task WriteSignalToEAFileAsync(ProcessedSignal signal)
        {
            if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
            {
                OnErrorOccurred("MT4 files path not configured.");
                throw new InvalidOperationException("MT4 files path not configured");
            }

            var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var signalText = FormatSignalForEA(signal);

            // Use semaphore to prevent concurrent file access
            await fileWriteSemaphore.WaitAsync();
            try
            {
                OnDebugMessage($"Checking for duplicates in file: {filePath}");
                // Check for duplicate in file before writing
                var existingLines = File.Exists(filePath) ? await File.ReadAllLinesAsync(filePath) : Array.Empty<string>();

                // Create a signature for this signal
                var signalSignature = $"|{signal.ChannelId}|{signal.ChannelName}|{signal.ParsedData?.Direction ?? ""}|{signal.ParsedData?.Symbol ?? ""}|";

                // Check if a similar signal was written in the last minute
                var recentDuplicate = existingLines
                    .Where(line => !line.StartsWith("#") && line.Contains(signalSignature))
                    .Select(line => line.Split('|'))
                    .Where(parts => parts.Length > 0)
                    .Select(parts => DateTime.TryParse(parts[0], out var dt) ? dt : DateTime.MinValue)
                    .Any(signalTime => (DateTime.Now - signalTime).TotalSeconds < 60);

                if (!recentDuplicate)
                {
                    OnDebugMessage("No recent duplicate found. Appending signal to file.");
                    await File.AppendAllTextAsync(filePath, signalText + Environment.NewLine);
                    OnDebugMessage("Signal written to file successfully.");
                }
                else
                {
                    OnDebugMessage("Skipped writing duplicate signal to file.");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error writing signal to file: {ex.Message}");
            }
            finally
            {
                fileWriteSemaphore.Release();
            }
        }
        private async Task SaveSignalsHistoryAsync()
        {
            try
            {
                List<ProcessedSignal> signalsToSave;
                lock (processedSignals)
                {
                    signalsToSave = processedSignals.ToList();
                }

                var json = JsonConvert.SerializeObject(signalsToSave, Formatting.Indented);
                await File.WriteAllTextAsync(signalsHistoryFile, json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to save signals history: {ex.Message}");
            }
        }
        public async Task CleanupProcessedSignalsAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                    return;

                var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

                if (!File.Exists(filePath))
                    return;

                await fileWriteSemaphore.WaitAsync();
                try
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    var newLines = new List<string>();
                    var now = DateTime.UtcNow;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line))
                        {
                            newLines.Add(line);
                            continue;
                        }

                        var parts = line.Split('|');
                        if (parts.Length >= 11)
                        {
                            if (DateTime.TryParse(parts[0], out DateTime signalTime))
                            {
                                var ageMinutes = (now - signalTime).TotalMinutes;
                                // Keep signals less than 10 minutes OR marked as PROCESSED
                                if (ageMinutes <= 10 || parts[10] == "PROCESSED")
                                {
                                    // Don't keep processed signals older than 30 minutes
                                    if (!(parts[10] == "PROCESSED" && ageMinutes > 30))
                                    {
                                        newLines.Add(line);
                                    }
                                }
                            }
                        }
                    }

                    await File.WriteAllLinesAsync(filePath, newLines);
                    OnDebugMessage($"Cleanup completed - kept {newLines.Count(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l))} signals");
                }
                finally
                {
                    fileWriteSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to cleanup signals: {ex.Message}");
                OnDebugMessage($"Cleanup error: {ex}");
            }
        }




        private string FormatSignalForEA(ProcessedSignal signal)
        {
            // Always use UTC time for consistency
            var utcTime = DateTime.Now;

            var formatted = $"{utcTime:yyyy.MM.dd HH:mm:ss}|" +
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

            OnDebugMessage($"Signal formatted for EA: {formatted}");
            return formatted;
        }

        /// <summary>
        /// Parse trading signal from message text using regex patterns
        /// </summary>
        private ParsedSignalData? ParseTradingSignal(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
            {
                OnDebugMessage("Message text is empty or whitespace");
                return null;
            }

            var text = messageText.ToUpper().Replace("\n", " ").Replace("\r", " ");
            OnDebugMessage($"Normalized text for parsing: {text.Substring(0, Math.Min(100, text.Length))}...");

            var signalData = new ParsedSignalData();

            // Extract direction (BUY/SELL) - improved pattern
            var directionMatch = Regex.Match(text, @"\b(BUY|SELL|LONG|SHORT)\b");
            if (!directionMatch.Success)
            {
                OnDebugMessage("No trading direction found in message");
                return null;
            }

            var direction = directionMatch.Value;
            if (direction == "LONG") direction = "BUY";
            if (direction == "SHORT") direction = "SELL";

            signalData.Direction = direction;
            OnDebugMessage($"Direction detected: {direction}");

            // Extract symbol - comprehensive patterns
            var symbolPatterns = new[]
            {
                // Major Forex Pairs
                @"\b(EUR\/USD|EURUSD|EUR-USD|EU)\b",
                @"\b(GBP\/USD|GBPUSD|GBP-USD|GU)\b",
                @"\b(USD\/JPY|USDJPY|USD-JPY|UJ)\b",
                @"\b(USD\/CHF|USDCHF|USD-CHF|UC)\b",
                @"\b(AUD\/USD|AUDUSD|AUD-USD|AU)\b",
                @"\b(USD\/CAD|USDCAD|USD-CAD|UCAD)\b",
                @"\b(NZD\/USD|NZDUSD|NZD-USD|NU)\b",
                @"\b(GBP\/JPY|GBPJPY|GBP-JPY|GJ)\b",
                @"\b(EUR\/JPY|EURJPY|EUR-JPY|EJ)\b",
                @"\b(EUR\/GBP|EURGBP|EUR-GBP|EG)\b",
                
                // Metals
                @"\b(GOLD|XAUUSD|XAU\/USD|XAU|AU)\b",
                @"\b(SILVER|XAGUSD|XAG\/USD|XAG|AG)\b",
                
                // Commodities
                @"\b(OIL|CRUDE|USOIL|WTI|BRENT|UKOIL)\b",
                @"\b(COPPER|XPTUSD|PLATINUM)\b",
                
                // Crypto
                @"\b(BITCOIN|BTC|BTCUSD|BTC\/USD)\b",
                @"\b(ETHEREUM|ETH|ETHUSD|ETH\/USD)\b",
                @"\b(RIPPLE|XRP|XRPUSD)\b",
                @"\b(LITECOIN|LTC|LTCUSD)\b",
                
                // Indices
                @"\b(US30|DOW|DJIA|DJ30)\b",
                @"\b(NAS100|NASDAQ|NDX|NAS)\b",
                @"\b(SPX500|SP500|SPX|S&P)\b",
                @"\b(GER30|DAX|DE30|GER40)\b",
                @"\b(UK100|FTSE|UKX|FTSE100)\b",
                @"\b(JPN225|NIKKEI|N225|NK)\b",
                @"\b(AUS200|ASX200|AU200)\b",
                @"\b(HK50|HSI|HANG.SENG)\b"
            };

            foreach (var pattern in symbolPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(match.Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                    OnDebugMessage($"Symbol detected: {signalData.OriginalSymbol}");
                    break;
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
            {
                // Try to extract symbol after BUY/SELL - more flexible
                var symbolAfterDirection = Regex.Match(text,
                    $@"\b{signalData.Direction}\s+([A-Z]{{2,8}}(?:\/[A-Z]{{3}}|[A-Z]{{0,3}})?)\b");
                if (symbolAfterDirection.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(symbolAfterDirection.Groups[1].Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                    OnDebugMessage($"Symbol detected after direction: {signalData.OriginalSymbol}");
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
            {
                OnDebugMessage("No symbol found in message");
                return null;
            }

            // Extract Stop Loss - improved patterns
            var slPatterns = new[]
            {
                @"SL\s*[:=@]?\s*(\d+\.?\d*)",
                @"STOP\s*LOSS\s*[:=@]?\s*(\d+\.?\d*)",
                @"STOPLOSS\s*[:=@]?\s*(\d+\.?\d*)",
                @"S/L\s*[:=@]?\s*(\d+\.?\d*)",
                @"STOP\s*[:=@]?\s*(\d+\.?\d*)"
            };

            foreach (var pattern in slPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double sl))
                {
                    signalData.StopLoss = sl;
                    OnDebugMessage($"Stop Loss detected: {sl}");
                    break;
                }
            }

            // Extract Take Profits - improved patterns
            var tpPatterns = new[]
            {
                (@"TP\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TP\s*2\s*[:=@]?\s*(\d+\.?\d*)", 2),
                (@"TP\s*3\s*[:=@]?\s*(\d+\.?\d*)", 3),
                (@"TAKE\s*PROFIT\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"T/P\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TARGET\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TARGET\s*2\s*[:=@]?\s*(\d+\.?\d*)", 2),
                (@"TARGET\s*3\s*[:=@]?\s*(\d+\.?\d*)", 3),
                (@"PROFIT\s*[:=@]?\s*(\d+\.?\d*)", 1)
            };

            foreach (var (pattern, level) in tpPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double tp))
                {
                    switch (level)
                    {
                        case 1:
                            signalData.TakeProfit1 = tp;
                            OnDebugMessage($"Take Profit 1 detected: {tp}");
                            break;
                        case 2:
                            signalData.TakeProfit2 = tp;
                            OnDebugMessage($"Take Profit 2 detected: {tp}");
                            break;
                        case 3:
                            signalData.TakeProfit3 = tp;
                            OnDebugMessage($"Take Profit 3 detected: {tp}");
                            break;
                    }
                }
            }

            // Extract Entry Price - improved patterns
            var entryPatterns = new[]
            {
                @"ENTRY\s*[:=@]?\s*(\d+\.?\d*)",
                @"PRICE\s*[:=@]?\s*(\d+\.?\d*)",
                @"AT\s*(\d+\.?\d*)",
                @"@\s*(\d+\.?\d*)",
                @"ENTER\s*[:=@]?\s*(\d+\.?\d*)"
            };

            foreach (var pattern in entryPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double entry))
                {
                    signalData.EntryPrice = entry;
                    OnDebugMessage($"Entry Price detected: {entry}");
                    break;
                }
            }

            OnDebugMessage($"Signal parsing completed: {signalData.Symbol} {signalData.Direction} SL:{signalData.StopLoss} TP1:{signalData.TakeProfit1}");
            return signalData;
        }
        /// <summary>
        /// Normalize symbol format
        /// </summary>
        private string NormalizeSymbol(string symbol)
        {
            var normalized = symbol.Replace("/", "").Replace("-", "").ToUpper().Trim();

            // Handle common abbreviations
            var symbolMappings = new Dictionary<string, string>
            {
                { "EU", "EURUSD" },
                { "GU", "GBPUSD" },
                { "UJ", "USDJPY" },
                { "UC", "USDCHF" },
                { "AU", "AUDUSD" },
                { "NU", "NZDUSD" },
                { "GJ", "GBPJPY" },
                { "EJ", "EURJPY" },
                { "EG", "EURGBP" },
                { "XAU", "XAUUSD" },
                { "XAG", "XAGUSD" }
            };

            if (symbolMappings.ContainsKey(normalized))
            {
                normalized = symbolMappings[normalized];
                OnDebugMessage($"Symbol abbreviation expanded: {symbol} -> {normalized}");
            }

            return normalized;
        }
        /// <summary>
        /// Apply symbol mapping with proper error handling
        /// </summary>
        private void ApplySymbolMapping(ParsedSignalData parsedData)
        {
            try
            {
                OnDebugMessage($"Applying symbol mapping for: {parsedData.OriginalSymbol}");

                // Step 1: Apply symbol mapping
                if (symbolMapping.Mappings.ContainsKey(parsedData.OriginalSymbol.ToUpper()))
                {
                    parsedData.Symbol = symbolMapping.Mappings[parsedData.OriginalSymbol.ToUpper()];
                    OnDebugMessage($"Symbol mapped: {parsedData.OriginalSymbol} -> {parsedData.Symbol}");
                }
                else
                {
                    parsedData.Symbol = parsedData.OriginalSymbol;
                    OnDebugMessage($"No mapping found, using original: {parsedData.Symbol}");
                }

                // Step 2: Check if should skip prefix/suffix
                bool shouldSkip = symbolMapping.SkipPrefixSuffix.Contains(parsedData.Symbol.ToUpper());
                OnDebugMessage($"Skip prefix/suffix: {shouldSkip}");

                // Step 3: Apply prefix/suffix if not skipping
                if (!shouldSkip)
                {
                    parsedData.FinalSymbol = symbolMapping.Prefix + parsedData.Symbol + symbolMapping.Suffix;
                    OnDebugMessage($"Applied prefix/suffix: {parsedData.Symbol} -> {parsedData.FinalSymbol}");
                }
                else
                {
                    parsedData.FinalSymbol = parsedData.Symbol;
                    OnDebugMessage($"Skipped prefix/suffix: {parsedData.FinalSymbol}");
                }

                // Step 4: Check exclusions
                if (symbolMapping.ExcludedSymbols.Contains(parsedData.FinalSymbol.ToUpper()) ||
                    symbolMapping.ExcludedSymbols.Contains(parsedData.OriginalSymbol.ToUpper()))
                {
                    OnDebugMessage($"Symbol excluded: {parsedData.OriginalSymbol}");
                    throw new InvalidOperationException($"Symbol {parsedData.OriginalSymbol} is excluded");
                }

                // Step 5: Check whitelist
                if (symbolMapping.AllowedSymbols.Count > 0)
                {
                    if (!symbolMapping.AllowedSymbols.Contains(parsedData.FinalSymbol.ToUpper()) &&
                        !symbolMapping.AllowedSymbols.Contains(parsedData.OriginalSymbol.ToUpper()))
                    {
                        OnDebugMessage($"Symbol not in whitelist: {parsedData.OriginalSymbol}");
                        throw new InvalidOperationException($"Symbol {parsedData.OriginalSymbol} not in whitelist");
                    }
                }

                OnDebugMessage($"Symbol mapping completed: {parsedData.OriginalSymbol} -> {parsedData.FinalSymbol}");
            }
            catch (Exception ex)
            {
                OnDebugMessage($"Symbol mapping failed: {ex.Message}");
                throw new InvalidOperationException($"Symbol mapping failed: {ex.Message}");
            }
        }
        /// <summary>
        /// Validate signal data
        private bool ValidateSignal(ParsedSignalData? parsedData)
        {
            if (parsedData == null)
            {
                OnDebugMessage("Validation failed: parsedData is null");
                return false;
            }

            if (string.IsNullOrEmpty(parsedData.Symbol))
            {
                OnDebugMessage("Validation failed: Symbol is empty");
                return false;
            }

            if (string.IsNullOrEmpty(parsedData.Direction))
            {
                OnDebugMessage("Validation failed: Direction is empty");
                return false;
            }

            if (string.IsNullOrEmpty(parsedData.FinalSymbol))
            {
                OnDebugMessage("Validation failed: FinalSymbol is empty");
                return false;
            }

            // Validate SL and TP logic
            if (parsedData.StopLoss > 0 && parsedData.TakeProfit1 > 0)
            {
                if (parsedData.Direction == "BUY")
                {
                    // For BUY: SL should be below TP
                    if (parsedData.StopLoss >= parsedData.TakeProfit1)
                    {
                        OnErrorOccurred($"Invalid BUY stops: SL ({parsedData.StopLoss}) >= TP ({parsedData.TakeProfit1})");
                        OnDebugMessage($"Validation failed: Invalid BUY stops - SL >= TP");
                        return false;
                    }
                }
                else if (parsedData.Direction == "SELL")
                {
                    // For SELL: SL should be above TP
                    if (parsedData.StopLoss <= parsedData.TakeProfit1)
                    {
                        OnErrorOccurred($"Invalid SELL stops: SL ({parsedData.StopLoss}) <= TP ({parsedData.TakeProfit1})");
                        OnDebugMessage($"Validation failed: Invalid SELL stops - SL <= TP");
                        return false;
                    }
                }
            }

            OnDebugMessage("Signal validation passed");
            return true;
        }

        /// <summary>
        /// Write signal to EA file with proper file sharing and locking
        /// </summary>
        // Update WriteSignalToEAFile header comment
        // Also update the WriteSignalToEAFile method to ensure proper file handling:
        private void WriteSignalToEAFile(ProcessedSignal signal)
        {
            if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
            {
                throw new InvalidOperationException("MT4 files path not configured");
            }

            var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

            // FIX: Ensure directory exists with proper null check
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var signalText = FormatSignalForEA(signal);

            // Write with retry mechanism
            var maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    lock (fileLock)
                    {
                        // Read existing content to check for duplicates
                        var existingLines = File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();

                        // Check if this exact signal already exists
                        if (!existingLines.Any(line => line.Contains($"|{signal.ChannelId}|") &&
                                                       line.Contains($"|{signal.ParsedData?.Symbol ?? ""}|") &&
                                                       line.Contains($"|{signal.ParsedData?.Direction ?? ""}|") &&
                                                       !line.EndsWith("|PROCESSED")))
                        {
                            using (var fs = new FileStream(
                                filePath,
                                FileMode.Append,
                                FileAccess.Write,
                                FileShare.Read,
                                4096,
                                FileOptions.WriteThrough))
                            using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8) { AutoFlush = true })
                            {
                                writer.WriteLine(signalText);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Signal already exists in file, skipping duplicate");
                        }
                    }

                    // Success - break the retry loop
                    break;
                }
                catch (IOException ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    Thread.Sleep(100 * retryCount);
                    OnErrorOccurred($"File write retry {retryCount}/{maxRetries}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to write signal to EA file: {ex.Message}");
                }
            }
        }
        public void CleanupProcessedSignals()
        {
            Task.Run(async () => await CleanupProcessedSignalsAsync()).Wait();
        }
        /// <summary>
        /// Format signal for EA with proper field order and formatting
        /// </summary>

        // ... rest of the existing methods remain the same ...
        private string GenerateSignalHash(string channelId, string symbol, string direction, DateTime timestamp)
        {
            // Create a deterministic string from signal components
            string hashSource = $"{channelId}|{symbol}|{direction}|{timestamp:yyyyMMddHHmmss}";

            // Simple hash algorithm that matches MQL5
            int hash = 0;
            foreach (char c in hashSource)
            {
                hash = ((hash * 31) + (int)c) % 1000000;
            }

            // Return hash with timestamp suffix for uniqueness
            return $"{hash}_{timestamp:yyyyMMddHHmmss}";
        }

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
            try
            {
                var json = JsonConvert.SerializeObject(symbolMapping, Formatting.Indented);
                File.WriteAllText("symbol_settings.json", json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to save symbol mapping: {ex.Message}");
            }
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
            try
            {
                var json = JsonConvert.SerializeObject(eaSettings, Formatting.Indented);
                File.WriteAllText("ea_settings.json", json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to save EA settings: {ex.Message}");
            }
        }

        private void LoadSignalsHistory()
        {
            try
            {
                if (File.Exists(signalsHistoryFile))
                {
                    var json = File.ReadAllText(signalsHistoryFile);
                    processedSignals = JsonConvert.DeserializeObject<List<ProcessedSignal>>(json) ?? new List<ProcessedSignal>();
                }
                else
                {
                    processedSignals = new List<ProcessedSignal>();
                }
            }
            catch
            {
                processedSignals = new List<ProcessedSignal>();
            }
        }

        private void SaveSignalsHistory()
        {
            try
            {
                // Keep only last 1000 signals
                if (processedSignals.Count > 1000)
                {
                    processedSignals = processedSignals
                        .OrderByDescending(s => s.DateTime)
                        .Take(1000)
                        .ToList();
                }

                var json = JsonConvert.SerializeObject(processedSignals, Formatting.Indented);
                File.WriteAllText(signalsHistoryFile, json);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to save signals history: {ex.Message}");
            }
        }

        public void UpdateSymbolMapping(SymbolMapping newMapping)
        {
            symbolMapping = newMapping;
            SaveSymbolMapping();
        }

        public SymbolMapping GetSymbolMapping()
        {
            return symbolMapping;
        }

        public void UpdateEASettings(EASettings newSettings)
        {
            eaSettings = newSettings;
            SaveEASettings();
        }

        public EASettings GetEASettings()
        {
            return eaSettings;
        }

        public List<ProcessedSignal> GetSignalsHistory()
        {
            lock (processedSignals)
            {
                return processedSignals.ToList();
            }
        }


        protected virtual void OnSignalProcessed(ProcessedSignal signal)
        {
            SignalProcessed?.Invoke(this, signal);
        }

        protected virtual void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }

        protected virtual void OnDebugMessage(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}