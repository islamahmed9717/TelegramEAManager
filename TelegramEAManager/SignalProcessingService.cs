﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace TelegramEAManager
{
    public class SignalProcessingService
    {
        private SymbolMapping symbolMapping = new SymbolMapping();
        private EASettings eaSettings = new EASettings();
        private List<ProcessedSignal> processedSignals = new List<ProcessedSignal>();
        private readonly string signalsHistoryFile = "signals_history.json";
        private readonly HashSet<string> writtenSignalIds = new HashSet<string>(); // Track written signals
        private readonly object fileLock = new object();

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

                    if (File.Exists(filePath))
                    {
                        // Write header only
                        lock (fileLock)
                        {
                            using (var writer = new StreamWriter(filePath, false))
                            {
                                writer.WriteLine("# Telegram EA Signal File - CLEARED ON STARTUP");
                                writer.WriteLine($"# Startup Time: {DateTime.UtcNow:yyyy.MM.dd HH:mm:ss} UTC");
                                writer.WriteLine("# Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS");
                                writer.WriteLine("");
                            }
                        }

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Signal file cleared on startup");
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to clear signal file on startup: {ex.Message}");
            }
        }

        /// <summary>
        /// Process a raw Telegram message and extract trading signals
        /// </summary>
        // Update ProcessTelegramMessage method
        /// <summary>
        /// Process a raw Telegram message and extract trading signals
        /// </summary>
        public ProcessedSignal ProcessTelegramMessage(string messageText, long channelId, string channelName)
        {
            // IMPORTANT: Use the ACTUAL time the message was received, not processing time
            var actualReceiveTime = DateTime.Now;

            var signal = new ProcessedSignal
            {
                Id = Guid.NewGuid().ToString(),
                DateTime = actualReceiveTime, // Use actual receive time
                ChannelId = channelId,
                ChannelName = channelName,
                OriginalText = messageText,
                Status = "Processing..."
            };

            try
            {
                // Parse the message for trading signals
                var parsedData = ParseTradingSignal(messageText);
                if (parsedData != null)
                {
                    signal.ParsedData = parsedData;

                    // Apply symbol mapping
                    ApplySymbolMapping(signal.ParsedData);

                    // Validate signal
                    if (ValidateSignal(signal.ParsedData))
                    {
                        // Write to EA file with ACTUAL timestamp
                        WriteSignalToEAFile(signal, actualReceiveTime);
                        signal.Status = "Processed - Sent to EA";

                        // Add to history
                        processedSignals.Add(signal);
                        SaveSignalsHistory();

                        OnSignalProcessed(signal);
                    }
                    else
                    {
                        signal.Status = "Invalid - Missing required data";
                    }
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
        private void WriteSignalToEAFile(ProcessedSignal signal, DateTime actualTimestamp)
        {
            if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
            {
                throw new InvalidOperationException("MT4 files path not configured");
            }

            var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Generate unique signal identifier
            var signalIdentifier = $"{signal.ChannelId}_{signal.ParsedData?.Symbol}_{signal.ParsedData?.Direction}_{actualTimestamp:yyyyMMddHHmmss}";

            // Check if we already wrote this signal
            if (writtenSignalIds.Contains(signalIdentifier))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Signal already written, skipping duplicate: {signalIdentifier}");
                return;
            }

            var signalText = FormatSignalForEA(signal, actualTimestamp);

            // Write with retry mechanism
            var maxRetries = 3;
            var retryCount = 0;

            while (retryCount < maxRetries)
            {
                try
                {
                    lock (fileLock)
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

                        // Mark as written
                        writtenSignalIds.Add(signalIdentifier);

                        // Clean up old entries if too many
                        if (writtenSignalIds.Count > 1000)
                        {
                            writtenSignalIds.Clear();
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
        private string FormatSignalForEA(ProcessedSignal signal, DateTime actualTimestamp)
        {
            // Use the ACTUAL timestamp, not current time
            // Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS
            var formatted = $"{actualTimestamp:yyyy.MM.dd HH:mm:ss}|" +
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

            // Debug log with actual timestamp
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Writing signal with timestamp: {actualTimestamp:yyyy.MM.dd HH:mm:ss} - {signal.ParsedData?.Symbol} {signal.ParsedData?.Direction}");

            return formatted;
        }


        /// <summary>
        /// Parse trading signal from message text using regex patterns
        /// </summary>
        private ParsedSignalData? ParseTradingSignal(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return null;

            var text = messageText.ToUpper().Replace("\n", " ").Replace("\r", " ");

            var signalData = new ParsedSignalData();

            // Extract direction (BUY/SELL)
            var directionMatch = Regex.Match(text, @"\b(BUY|SELL)\b");
            if (!directionMatch.Success)
                return null; // No direction found

            signalData.Direction = directionMatch.Value;

            // Extract symbol - look for common forex patterns
            var symbolPatterns = new[]
            {
                @"\b(EUR\/USD|EURUSD|EUR-USD)\b",
                @"\b(GBP\/USD|GBPUSD|GBP-USD)\b",
                @"\b(USD\/JPY|USDJPY|USD-JPY)\b",
                @"\b(USD\/CHF|USDCHF|USD-CHF)\b",
                @"\b(AUD\/USD|AUDUSD|AUD-USD)\b",
                @"\b(USD\/CAD|USDCAD|USD-CAD)\b",
                @"\b(NZD\/USD|NZDUSD|NZD-USD)\b",
                @"\b(GBP\/JPY|GBPJPY|GBP-JPY)\b",
                @"\b(EUR\/JPY|EURJPY|EUR-JPY)\b",
                @"\b(EUR\/GBP|EURGBP|EUR-GBP)\b",
                @"\b(GOLD|XAUUSD|XAU\/USD)\b",
                @"\b(SILVER|XAGUSD|XAG\/USD)\b",
                @"\b(OIL|CRUDE|USOIL|WTI)\b",
                @"\b(BITCOIN|BTC|BTCUSD|BTC\/USD)\b",
                @"\b(ETHEREUM|ETH|ETHUSD|ETH\/USD)\b",
                @"\b(US30|DOW|DJIA)\b",
                @"\b(NAS100|NASDAQ|NDX)\b",
                @"\b(SPX500|SP500|SPX)\b",
                @"\b(GER30|DAX|DE30)\b",
                @"\b(UK100|FTSE|UKX)\b",
                @"\b(JPN225|NIKKEI|N225)\b"
            };

            foreach (var pattern in symbolPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(match.Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                    break;
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
            {
                // Try to extract symbol after BUY/SELL
                var symbolAfterDirection = Regex.Match(text,
                    $@"\b{signalData.Direction}\s+([A-Z]{{3,8}}(?:\/[A-Z]{{3}}|[A-Z]{{3}})?)\b");
                if (symbolAfterDirection.Success)
                {
                    signalData.OriginalSymbol = NormalizeSymbol(symbolAfterDirection.Groups[1].Value);
                    signalData.Symbol = signalData.OriginalSymbol;
                }
            }

            if (string.IsNullOrEmpty(signalData.Symbol))
                return null; // No symbol found

            // Extract Stop Loss
            var slPatterns = new[]
            {
                @"SL\s*[:=@]?\s*(\d+\.?\d*)",
                @"STOP\s*LOSS\s*[:=@]?\s*(\d+\.?\d*)",
                @"STOPLOSS\s*[:=@]?\s*(\d+\.?\d*)"
            };

            foreach (var pattern in slPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double sl))
                {
                    signalData.StopLoss = sl;
                    break;
                }
            }

            // Extract Take Profits
            var tpPatterns = new[]
            {
                (@"TP\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TP\s*2\s*[:=@]?\s*(\d+\.?\d*)", 2),
                (@"TP\s*3\s*[:=@]?\s*(\d+\.?\d*)", 3),
                (@"TAKE\s*PROFIT\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TARGET\s*1?\s*[:=@]?\s*(\d+\.?\d*)", 1),
                (@"TARGET\s*2\s*[:=@]?\s*(\d+\.?\d*)", 2),
                (@"TARGET\s*3\s*[:=@]?\s*(\d+\.?\d*)", 3)
            };

            foreach (var (pattern, level) in tpPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double tp))
                {
                    switch (level)
                    {
                        case 1: signalData.TakeProfit1 = tp; break;
                        case 2: signalData.TakeProfit2 = tp; break;
                        case 3: signalData.TakeProfit3 = tp; break;
                    }
                }
            }

            // Extract Entry Price (optional)
            var entryPatterns = new[]
            {
                @"ENTRY\s*[:=@]?\s*(\d+\.?\d*)",
                @"PRICE\s*[:=@]?\s*(\d+\.?\d*)",
                @"AT\s*(\d+\.?\d*)",
                @"@\s*(\d+\.?\d*)"
            };

            foreach (var pattern in entryPatterns)
            {
                var match = Regex.Match(text, pattern);
                if (match.Success && double.TryParse(match.Groups[1].Value, out double entry))
                {
                    signalData.EntryPrice = entry;
                    break;
                }
            }

            return signalData;
        }
        /// <summary>
        /// Normalize symbol format
        /// </summary>
        private string NormalizeSymbol(string symbol)
        {
            return symbol.Replace("/", "").Replace("-", "").ToUpper().Trim();
        }

        /// <summary>
        /// Apply symbol mapping with proper error handling
        /// </summary>
        private void ApplySymbolMapping(ParsedSignalData parsedData)
        {
            try
            {
                // Step 1: Apply symbol mapping
                if (symbolMapping.Mappings.ContainsKey(parsedData.OriginalSymbol.ToUpper()))
                {
                    parsedData.Symbol = symbolMapping.Mappings[parsedData.OriginalSymbol.ToUpper()];
                }
                else
                {
                    parsedData.Symbol = parsedData.OriginalSymbol;
                }

                // Step 2: Check if should skip prefix/suffix
                bool shouldSkip = symbolMapping.SkipPrefixSuffix.Contains(parsedData.Symbol.ToUpper());

                // Step 3: Apply prefix/suffix if not skipping
                if (!shouldSkip)
                {
                    parsedData.FinalSymbol = symbolMapping.Prefix + parsedData.Symbol + symbolMapping.Suffix;
                }
                else
                {
                    parsedData.FinalSymbol = parsedData.Symbol;
                }

                // Step 4: Check exclusions
                if (symbolMapping.ExcludedSymbols.Contains(parsedData.FinalSymbol.ToUpper()) ||
                    symbolMapping.ExcludedSymbols.Contains(parsedData.OriginalSymbol.ToUpper()))
                {
                    throw new InvalidOperationException($"Symbol {parsedData.OriginalSymbol} is excluded");
                }

                // Step 5: Check whitelist
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

        /// <summary>
        /// Validate signal data
        private bool ValidateSignal(ParsedSignalData? parsedData)
        {
            if (parsedData == null ||
                string.IsNullOrEmpty(parsedData.Symbol) ||
                string.IsNullOrEmpty(parsedData.Direction) ||
                string.IsNullOrEmpty(parsedData.FinalSymbol))
            {
                return false;
            }

            // Validate SL and TP logic
            if (parsedData.StopLoss > 0 && parsedData.TakeProfit1 > 0)
            {
                if (parsedData.Direction == "BUY")
                {
                    // For BUY: SL should be below entry, TP should be above entry
                    if (parsedData.StopLoss >= parsedData.TakeProfit1)
                    {
                        OnErrorOccurred($"Invalid BUY stops: SL ({parsedData.StopLoss}) >= TP ({parsedData.TakeProfit1})");
                        return false;
                    }
                }
                else if (parsedData.Direction == "SELL")
                {
                    // For SELL: SL should be above entry, TP should be below entry
                    if (parsedData.StopLoss <= parsedData.TakeProfit1)
                    {
                        OnErrorOccurred($"Invalid SELL stops: SL ({parsedData.StopLoss}) <= TP ({parsedData.TakeProfit1})");
                        return false;
                    }
                }
            }

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

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var signalText = FormatSignalForEA(signal);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] About to write signal: {signalText}");

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

                        // Check if this exact signal already exists (check by channel + symbol + direction + time)
                        var signalKey = $"|{signal.ChannelId}|{signal.ChannelName}|{signal.ParsedData?.Direction}|{signal.ParsedData?.Symbol}|";
                        bool isDuplicate = existingLines.Any(line =>
                            line.Contains(signalKey) &&
                            line.EndsWith("|NEW") &&
                            !line.StartsWith("#"));

                        if (!isDuplicate)
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

                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Signal written successfully to: {filePath}");
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
            try
            {
                if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                    return;

                var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

                if (!File.Exists(filePath))
                    return;

                lock (fileLock)
                {
                    var lines = File.ReadAllLines(filePath).ToList();
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
                            // Parse timestamp
                            if (DateTime.TryParse(parts[0], out DateTime signalTime))
                            {
                                var ageMinutes = (now - signalTime).TotalMinutes;

                                // Keep only recent signals (less than MaxSignalAge)
                                if (ageMinutes <= 10)
                                {
                                    newLines.Add(line);
                                }
                                else
                                {
                                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Removing old signal: {parts[4]} {parts[3]} - Age: {ageMinutes:F1} minutes");
                                }
                            }
                        }
                    }

                    // Write back cleaned file
                    File.WriteAllLines(filePath, newLines);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Cleanup complete - Kept {newLines.Count(l => !l.StartsWith("#") && !string.IsNullOrWhiteSpace(l))} signals");
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to cleanup signals: {ex.Message}");
            }
        }

        /// <summary>
        /// Format signal for EA with proper field order and formatting
        /// </summary>
        private string FormatSignalForEA(ProcessedSignal signal)
        {
            var utcTime = DateTime.Now;

            // FIXED FORMAT: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS
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

            // Debug log with UTC time
            Console.WriteLine($"[{utcTime:HH:mm:ss} UTC] Writing signal: {signal.ParsedData?.Symbol} {signal.ParsedData?.Direction}");
            Console.WriteLine($"[{utcTime:HH:mm:ss} UTC] Formatted line: {formatted}");

            return formatted;
        }
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
            return processedSignals.ToList();
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
            DebugMessage?.Invoke(this, message);
        }
    }
}