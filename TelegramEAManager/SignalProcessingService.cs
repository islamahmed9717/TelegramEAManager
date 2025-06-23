using System;
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
        private readonly object fileLock = new object();

        // Events
        public event EventHandler<ProcessedSignal>? SignalProcessed;
        public event EventHandler<string>? ErrorOccurred;

        public SignalProcessingService()
        {
            LoadSymbolMapping();
            LoadEASettings();
            LoadSignalsHistory();
        }

        /// <summary>
        /// Process a raw Telegram message and extract trading signals
        /// </summary>
        public ProcessedSignal ProcessTelegramMessage(string messageText, long channelId, string channelName)
        {
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
                        // Write to EA file with proper file sharing
                        WriteSignalToEAFile(signal);
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
        /// </summary>
        private bool ValidateSignal(ParsedSignalData? parsedData)
        {
            return parsedData != null &&
                   !string.IsNullOrEmpty(parsedData.Symbol) &&
                   !string.IsNullOrEmpty(parsedData.Direction) &&
                   !string.IsNullOrEmpty(parsedData.FinalSymbol);
        }

        /// <summary>
        /// Write signal to EA file with proper file sharing and locking
        /// </summary>
        private void WriteSignalToEAFile(ProcessedSignal signal)
        {
            if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
            {
                throw new InvalidOperationException("MT4 files path not configured");
            }

            // FIXED: Use correct filename case that matches EA expectation
            var filePath = Path.Combine(eaSettings.MT4FilesPath, "telegram_signals.txt");

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var signalText = FormatSignalForEA(signal);

            // FIXED: Proper file sharing with retry mechanism
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
                            FileShare.Read,  // Allow EA to read while we write
                            4096,
                            FileOptions.WriteThrough))
                        using (var writer = new StreamWriter(fs, System.Text.Encoding.UTF8) { AutoFlush = true })
                        {
                            writer.WriteLine(signalText);
                        }
                    }

                    // Success - break the retry loop
                    break;
                }
                catch (IOException ex) when (retryCount < maxRetries - 1)
                {
                    // File is locked, wait and retry
                    retryCount++;
                    Thread.Sleep(100 * retryCount); // Progressive delay
                    OnErrorOccurred($"File write retry {retryCount}/{maxRetries}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to write signal to EA file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Format signal for EA with proper field order and formatting
        /// </summary>
        private string FormatSignalForEA(ProcessedSignal signal)
        {
            // FIXED: Match exact EA format
            // Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS
            var formatted = $"{signal.DateTime:yyyy.MM.dd HH:mm:ss}|" +
                            $"{signal.ChannelId}|" +
                            $"{signal.ChannelName}|" +
                            $"{signal.ParsedData?.Direction ?? "BUY"}|" +
                            $"{signal.ParsedData?.FinalSymbol ?? "EURUSD"}|" +
                            $"{signal.ParsedData?.EntryPrice:F5}|" +
                            $"{signal.ParsedData?.StopLoss:F5}|" +
                            $"{signal.ParsedData?.TakeProfit1:F5}|" +
                            $"{signal.ParsedData?.TakeProfit2:F5}|" +
                            $"{signal.ParsedData?.TakeProfit3:F5}|" +
                            $"NEW";

            return formatted;
        }

        // ... rest of the existing methods remain the same ...

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
    }
}