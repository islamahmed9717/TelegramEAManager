using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TelegramEAManager
{
    public class SignalProcessingService
    {
        private SymbolMapping symbolMapping = new SymbolMapping();
        private EASettings eaSettings = new EASettings();
        private List<ProcessedSignal> processedSignals = new List<ProcessedSignal>();
        private readonly string signalsHistoryFile = "signals_history.json";

        // Events
        public event EventHandler<ProcessedSignal>? SignalProcessed;
        public event EventHandler<string>? ErrorOccurred;

        public SignalProcessingService()
        {
            LoadSymbolMapping();
            LoadEASettings();
            LoadSignalsHistory();
        }

        public void ProcessSignal(ProcessedSignal signal, List<long> monitoredChannels)
        {
            try
            {
                // Check if channel is monitored
                if (!monitoredChannels.Contains(signal.ChannelId))
                {
                    signal.Status = "Ignored - Channel not monitored";
                    return;
                }

                // Check for duplicate signals
                if (IsDuplicateSignal(signal))
                {
                    signal.Status = "Ignored - Duplicate";
                    return;
                }

                // Apply symbol mapping
                if (signal.ParsedData != null)
                {
                    ApplySymbolMapping(signal.ParsedData);
                }

                // Validate signal
                if (!ValidateSignal(signal.ParsedData))
                {
                    signal.Status = "Invalid - Missing required data";
                    return;
                }

                // Write to EA file
                WriteSignalToEAFile(signal);

                signal.Status = "Processed - Sent to EA";

                // Add to history
                processedSignals.Add(signal);
                SaveSignalsHistory();

                OnSignalProcessed(signal);
            }
            catch (Exception ex)
            {
                signal.Status = $"Error - {ex.Message}";
                signal.ErrorMessage = ex.ToString();
                OnErrorOccurred($"Error processing signal: {ex.Message}");
            }
        }

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

        private bool ValidateSignal(ParsedSignalData? parsedData)
        {
            return parsedData != null &&
                   !string.IsNullOrEmpty(parsedData.Symbol) &&
                   !string.IsNullOrEmpty(parsedData.Direction) &&
                   !string.IsNullOrEmpty(parsedData.FinalSymbol);
        }

        private bool IsDuplicateSignal(ProcessedSignal signal)
        {
            var recentSignals = processedSignals
                .Where(s => s.DateTime > DateTime.UtcNow.AddMinutes(-5))
                .Where(s => s.ChannelId == signal.ChannelId);

            return recentSignals.Any(s =>
                s.OriginalText.Trim().Equals(signal.OriginalText.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private void WriteSignalToEAFile(ProcessedSignal signal)
        {
            try
            {
                if (string.IsNullOrEmpty(eaSettings.MT4FilesPath))
                {
                    throw new InvalidOperationException("MT4 files path not configured");
                }

                var filePath = Path.Combine(eaSettings.MT4FilesPath, eaSettings.SignalFilePath);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var signalText = FormatSignalForEA(signal);

                // Append to file
                File.AppendAllText(filePath, signalText);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to write signal to EA file: {ex.Message}");
            }
        }

        private string FormatSignalForEA(ProcessedSignal signal)
        {
            var formatted = $@"[{signal.DateTime:yyyy-MM-dd HH:mm:ss} UTC] - Channel: {signal.ChannelName} [{signal.ChannelId}]
{signal.ParsedData?.Direction ?? "N/A"} NOW {signal.ParsedData?.FinalSymbol ?? "N/A"}";

            if (signal.ParsedData?.StopLoss > 0)
                formatted += $"\nSL {signal.ParsedData.StopLoss:F5}";

            if (signal.ParsedData?.TakeProfit1 > 0)
                formatted += $"\nTP {signal.ParsedData.TakeProfit1:F5}";

            if (signal.ParsedData?.TakeProfit2 > 0)
                formatted += $"\nTP2 {signal.ParsedData.TakeProfit2:F5}";

            if (signal.ParsedData?.TakeProfit3 > 0)
                formatted += $"\nTP3 {signal.ParsedData.TakeProfit3:F5}";

            formatted += $"\n{new string('=', 50)}\n\n";

            return formatted;
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
    }
}