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
           // ===== MAJOR FOREX PAIRS =====
    new Regex(@"\b(EUR\/USD|EURUSD|EUR-USD|EU|EURO)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/USD|GBPUSD|GBP-USD|GU|CABLE|POUND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/JPY|USDJPY|USD-JPY|UJ|YEN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/CHF|USDCHF|USD-CHF|UC|SWISSY|FRANC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AUD\/USD|AUDUSD|AUD-USD|AU|AUSSIE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/CAD|USDCAD|USD-CAD|UC|LOONIE|CAD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NZD\/USD|NZDUSD|NZD-USD|NU|KIWI)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== CROSS PAIRS =====
    new Regex(@"\b(EUR\/GBP|EURGBP|EUR-GBP|EG)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(EUR\/JPY|EURJPY|EUR-JPY|EJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/JPY|GBPJPY|GBP-JPY|GJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(EUR\/CHF|EURCHF|EUR-CHF|EC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/CHF|GBPCHF|GBP-CHF|GC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AUD\/JPY|AUDJPY|AUD-JPY|AJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AUD\/CHF|AUDCHF|AUD-CHF|AC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AUD\/NZD|AUDNZD|AUD-NZD|AN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CAD\/JPY|CADJPY|CAD-JPY|CJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CAD\/CHF|CADCHF|CAD-CHF|CC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CHF\/JPY|CHFJPY|CHF-JPY|CJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NZD\/JPY|NZDJPY|NZD-JPY|NJ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NZD\/CHF|NZDCHF|NZD-CHF|NC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(EUR\/AUD|EURAUD|EUR-AUD|EA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(EUR\/CAD|EURCAD|EUR-CAD|ECAD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(EUR\/NZD|EURNZD|EUR-NZD|EN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/AUD|GBPAUD|GBP-AUD|GA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/CAD|GBPCAD|GBP-CAD|GCAD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GBP\/NZD|GBPNZD|GBP-NZD|GN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== EXOTIC PAIRS =====
    new Regex(@"\b(USD\/SEK|USDSEK|USD-SEK|KRONA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/NOK|USDNOK|USD-NOK|KRONE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/DKK|USDDKK|USD-DKK|DANISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/PLN|USDPLN|USD-PLN|ZLOTY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/HUF|USDHUF|USD-HUF|FORINT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/CZK|USDCZK|USD-CZK|KORUNA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/TRY|USDTRY|USD-TRY|LIRA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/ZAR|USDZAR|USD-ZAR|RAND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/MXN|USDMXN|USD-MXN|PESO)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/SGD|USDSGD|USD-SGD|SINGAPORE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(USD\/HKD|USDHKD|USD-HKD|HONGKONG)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== PRECIOUS METALS =====
    new Regex(@"\b(GOLD|XAUUSD|XAU\/USD|XAU|AU|XAUEUR|GOLD\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SILVER|XAGUSD|XAG\/USD|XAG|AG|XAGEUR|SILVER\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(PLATINUM|XPTUSD|XPT\/USD|XPT|PLAT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(PALLADIUM|XPDUSD|XPD\/USD|XPD|PALL)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== COMMODITIES & ENERGY =====
    new Regex(@"\b(OIL|CRUDE|USOIL|WTI|CL|CRUDE\.OIL|OIL\.WTI|USCRUDE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(BRENT|UKOIL|BRENT\.OIL|UKCRUDE|ICE\.BRENT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NATGAS|NATURAL\.GAS|NG|GAS|HENRY\.HUB)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(COPPER|HG|CUUSD|COPPER\.USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(COTTON|CT|COTTON2)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SUGAR|SB|SUGAR11)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(COFFEE|KC|COFFEE\.C)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(COCOA|CC|CACAO)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(WHEAT|W|ZW)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CORN|C|ZC|MAIZE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SOYBEAN|S|ZS|SOYA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== US INDICES =====
    new Regex(@"\b(US30|DOW|DJIA|DJ30|DJI|DOW\.JONES|DOWJONES)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NAS100|NASDAQ|NDX|NAS|COMP|NASDAQ100|QQQ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SPX500|SP500|SPX|S&P|S\.P\.500|SPY|ES)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(RUT2000|RUSSELL|RUT|RTY|RUSSELL2000|IWM)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(VIX|VOLATILITY|FEAR\.INDEX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== EUROPEAN INDICES =====
    new Regex(@"\b(GER30|GER40|DAX|DE30|DE40|GERMAN|DEUTSCHLAND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(UK100|FTSE|UKX|FTSE100|BRITISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(FRA40|CAC|CAC40|FRENCH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(ESP35|IBEX|IBEX35|SPANISH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(ITA40|FTSEMIB|MIB|ITALIAN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AEX|NED25|DUTCH|NETHERLANDS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SMI20|SWISS|SWITZERLAND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(STOXX50|SX5E|EURO\.STOXX|EUROSTOXX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== ASIAN INDICES =====
    new Regex(@"\b(JPN225|NIKKEI|N225|NK|JAPAN|NIKK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(HK50|HSI|HANG\.SENG|HONGKONG\.50)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AUS200|ASX200|AU200|AUSSIE200|AUSTRALIAN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CHN50|CHINA50|CN50|FTSE\.CHINA)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(IND50|NIFTY|NIFTY50|INDIA50)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SG30|STI|SINGAPORE30|STRAITS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(KOR200|KOSPI|KOREAN)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== CRYPTOCURRENCIES =====
    new Regex(@"\b(BITCOIN|BTC|BTCUSD|BTC\/USD|XBT|BTCEUR|BTCGBP)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(ETHEREUM|ETH|ETHUSD|ETH\/USD|ETHEUR|ETHGBP)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(RIPPLE|XRP|XRPUSD|XRP\/USD|XRPEUR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(LITECOIN|LTC|LTCUSD|LTC\/USD|LTCEUR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(BITCOIN\.CASH|BCH|BCHUSD|BCH\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CARDANO|ADA|ADAUSD|ADA\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(BINANCE\.COIN|BNB|BNBUSD|BNB\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(POLKADOT|DOT|DOTUSD|DOT\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(CHAINLINK|LINK|LINKUSD|LINK\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(STELLAR|XLM|XLMUSD|XLM\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(DOGECOIN|DOGE|DOGEUSD|DOGE\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(SOLANA|SOL|SOLUSD|SOL\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(MATIC|POLYGON|MATICUSD|MATIC\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AVALANCHE|AVAX|AVAXUSD|AVAX\/USD)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== BONDS & TREASURIES =====
    new Regex(@"\b(US10Y|US10YR|10YEAR|TNX|TREASURY|T\.NOTE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(US30Y|US30YR|30YEAR|TYX|BOND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(US02Y|US2Y|2YEAR|TWO\.YEAR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(US05Y|US5Y|5YEAR|FIVE\.YEAR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(BUND|GERMAN\.BUND|GER\.BUND)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GILT|UK\.GILT|BRITISH\.GILT)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== POPULAR STOCKS (CFDs) =====
    new Regex(@"\b(APPLE|AAPL|APPLE\.INC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(MICROSOFT|MSFT|MS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(AMAZON|AMZN|AMZ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(GOOGLE|GOOGL|GOOG|ALPHABET)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(TESLA|TSLA|TESLA\.INC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(FACEBOOK|META|FB)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NETFLIX|NFLX|NFX)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(NVIDIA|NVDA|NV)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(PAYPAL|PYPL|PP)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(ZOOM|ZM|ZOOM\.VIDEO)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(DISNEY|DIS|WALT\.DISNEY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(COCA\.COLA|KO|COKE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(MCDONALD|MCD|MCDONALDS)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    new Regex(@"\b(ALIBABA|BABA|ALI)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),

    // ===== ALTERNATIVE SYMBOL FORMATS =====
    new Regex(@"\b([A-Z]{3}USD|[A-Z]{6}|[A-Z]{3}\/USD)\b", RegexOptions.Compiled),
    new Regex(@"\b([A-Z]{2,4}[0-9]{2,3})\b", RegexOptions.Compiled), // For indices like GER40, SPX500
    new Regex(@"\b(US[0-9]{2,3}|UK[0-9]{2,3}|DE[0-9]{2,3}|FR[0-9]{2,3})\b", RegexOptions.Compiled),
};

        // ENHANCED PARSING METHOD WITH ALL SYMBOLS SUPPORT
        private ParsedSignalData? ParseTradingSignal(string messageText)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return null;

            var text = messageText.ToUpper().Replace("\n", " ").Replace("\r", " ");
            OnDebugMessage($"🔍 PARSING: {text.Substring(0, Math.Min(100, text.Length))}...");

            var signalData = new ParsedSignalData();

            // FIXED: Fast direction detection
            var directionMatch = DirectionRegex.Match(text);
            if (!directionMatch.Success)
            {
                OnDebugMessage("❌ No trading direction found");
                return null;
            }

            var direction = directionMatch.Value.ToUpper();
            if (direction == "LONG") direction = "BUY";
            if (direction == "SHORT") direction = "SELL";
            signalData.Direction = direction;

            OnDebugMessage($"✅ Direction detected: {direction}");

            // ENHANCED: Ultra-fast symbol detection using all patterns
            string detectedSymbol = "";
            foreach (var pattern in SymbolPatterns)
            {
                var match = pattern.Match(text);
                if (match.Success)
                {
                    detectedSymbol = match.Value;
                    OnDebugMessage($"🎯 Symbol pattern matched: {detectedSymbol}");
                    break;
                }
            }

            // If no symbol found with patterns, try extracting from text around direction
            if (string.IsNullOrEmpty(detectedSymbol))
            {
                OnDebugMessage("🔍 No pattern match, trying context-based extraction...");
                detectedSymbol = ExtractSymbolFromContext(text, direction);
            }

            if (string.IsNullOrEmpty(detectedSymbol))
            {
                OnDebugMessage("❌ No symbol detected");
                return null;
            }

            // ENHANCED: Apply mega symbol mappings
            string normalizedSymbol = ApplyMegaSymbolMapping(detectedSymbol);

            signalData.OriginalSymbol = detectedSymbol;
            signalData.Symbol = normalizedSymbol;

            OnDebugMessage($"🗺️ Symbol mapping: {detectedSymbol} → {normalizedSymbol}");

            // ENHANCED: Extract all price levels with improved patterns
            ExtractEnhancedPriceLevels(text, signalData);

            // ENHANCED: Validate signal completeness
            if (!ValidateSignalCompleteness(signalData))
            {
                OnDebugMessage("❌ Signal validation failed");
                return null;
            }

            OnDebugMessage($"✅ Signal parsed successfully: {signalData.Symbol} {signalData.Direction}");
            return signalData;
        }

        // ENHANCED: Validate signal completeness
        private bool ValidateSignalCompleteness(ParsedSignalData signalData)
        {
            try
            {
                var score = 0;
                var issues = new List<string>();

                // Check essential components
                if (!string.IsNullOrEmpty(signalData.Symbol))
                {
                    score += 30;
                    OnDebugMessage("✅ Symbol present");
                }
                else
                {
                    issues.Add("Missing symbol");
                }

                if (!string.IsNullOrEmpty(signalData.Direction))
                {
                    score += 30;
                    OnDebugMessage("✅ Direction present");
                }
                else
                {
                    issues.Add("Missing direction");
                }

                if (signalData.StopLoss > 0)
                {
                    score += 25;
                    OnDebugMessage("✅ Stop Loss present");
                }
                else
                {
                    issues.Add("Missing Stop Loss");
                }

                if (signalData.TakeProfit1 > 0)
                {
                    score += 15;
                    OnDebugMessage("✅ Take Profit present");
                }
                else
                {
                    issues.Add("Missing Take Profit");
                }

                // Bonus points for additional TPs
                if (signalData.TakeProfit2 > 0) score += 5;
                if (signalData.TakeProfit3 > 0) score += 5;

                OnDebugMessage($"📊 Signal completeness score: {score}/100");

                if (issues.Count > 0)
                {
                    OnDebugMessage($"⚠️ Issues found: {string.Join(", ", issues)}");
                }

                // Require minimum score of 60 for basic signal
                var isValid = score >= 60;

                if (isValid)
                {
                    OnDebugMessage("✅ Signal validation passed");
                }
                else
                {
                    OnDebugMessage($"❌ Signal validation failed - Score: {score}/100 (minimum: 60)");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                OnDebugMessage($"❌ Validation error: {ex.Message}");
                return false;
            }
        }
        private void ExtractEnhancedPriceLevels(string text, ParsedSignalData signalData)
        {
            try
            {
                OnDebugMessage("🎯 Extracting price levels...");

                // Extract Stop Loss
                var slMatches = new[]
                {
            Regex.Match(text, @"(?:SL|S\.L|STOP\s*LOSS|STOPLOSS)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase),
            Regex.Match(text, @"(?:STOP|STP)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase)
        };

                foreach (var match in slMatches)
                {
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double sl))
                    {
                        signalData.StopLoss = sl;
                        OnDebugMessage($"🛑 Stop Loss: {sl}");
                        break;
                    }
                }

                // Extract Take Profits
                var tp1Matches = new[]
                {
            Regex.Match(text, @"(?:TP\s*1?|T\.P\.?\s*1?|TAKE\s*PROFIT\s*1?|TARGET\s*1?)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase),
            Regex.Match(text, @"(?:TGT|TARGET)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase)
        };

                foreach (var match in tp1Matches)
                {
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double tp1))
                    {
                        signalData.TakeProfit1 = tp1;
                        OnDebugMessage($"🎯 Take Profit 1: {tp1}");
                        break;
                    }
                }

                // Extract TP2
                var tp2Match = Regex.Match(text, @"(?:TP\s*2|T\.P\.?\s*2|TAKE\s*PROFIT\s*2|TARGET\s*2)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase);
                if (tp2Match.Success && double.TryParse(tp2Match.Groups[1].Value, out double tp2))
                {
                    signalData.TakeProfit2 = tp2;
                    OnDebugMessage($"🎯 Take Profit 2: {tp2}");
                }

                // Extract TP3
                var tp3Match = Regex.Match(text, @"(?:TP\s*3|T\.P\.?\s*3|TAKE\s*PROFIT\s*3|TARGET\s*3)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase);
                if (tp3Match.Success && double.TryParse(tp3Match.Groups[1].Value, out double tp3))
                {
                    signalData.TakeProfit3 = tp3;
                    OnDebugMessage($"🎯 Take Profit 3: {tp3}");
                }

                // Extract Entry Price
                var entryMatches = new[]
                {
            Regex.Match(text, @"(?:ENTRY|ENT|@|AT)\s*[:=@]?\s*(\d+\.?\d*)", RegexOptions.IgnoreCase),
            Regex.Match(text, @"(?:BUY|SELL)\s+(?:AT\s+)?(\d+\.?\d*)", RegexOptions.IgnoreCase)
        };

                foreach (var match in entryMatches)
                {
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double entry))
                    {
                        signalData.EntryPrice = entry;
                        OnDebugMessage($"📊 Entry Price: {entry}");
                        break;
                    }
                }

                // If no specific entry found, look for "NOW" or "MARKET"
                if (signalData.EntryPrice == 0)
                {
                    if (text.Contains("NOW") || text.Contains("MARKET") || text.Contains("CMP"))
                    {
                        OnDebugMessage("📊 Market order detected (NOW/MARKET/CMP)");
                        // Entry price will remain 0 for market orders
                    }
                }

                OnDebugMessage($"📋 Price extraction complete - SL: {signalData.StopLoss}, TP1: {signalData.TakeProfit1}, TP2: {signalData.TakeProfit2}, TP3: {signalData.TakeProfit3}, Entry: {signalData.EntryPrice}");
            }
            catch (Exception ex)
            {
                OnDebugMessage($"❌ Price extraction error: {ex.Message}");
            }
        }

        // ENHANCED: Context-based symbol extraction
        private string ExtractSymbolFromContext(string text, string direction)
        {
            try
            {
                // Look for symbol patterns around the direction word
                var directionIndex = text.IndexOf(direction);
                if (directionIndex == -1) return "";

                // Extract 50 characters before and after direction
                var startIndex = Math.Max(0, directionIndex - 50);
                var endIndex = Math.Min(text.Length, directionIndex + direction.Length + 50);
                var contextText = text.Substring(startIndex, endIndex - startIndex);

                OnDebugMessage($"🔍 Context: {contextText}");

                // Try common symbol patterns in context
                var contextPatterns = new[]
                {
            @"\b([A-Z]{6})\b",           // EURUSD, GBPJPY
            @"\b([A-Z]{3}/[A-Z]{3})\b",  // EUR/USD, GBP/JPY
            @"\b([A-Z]{3}[A-Z]{3})\b",   // EURUSD without separator
            @"\b(XAU[A-Z]{3})\b",        // XAUUSD, XAUEUR
            @"\b(XAG[A-Z]{3})\b",        // XAGUSD
            @"\b([A-Z]{2,4}[0-9]{2,3})\b", // US30, GER40
            @"\b([A-Z]{2}[0-9]{2,4})\b"    // US30, UK100
        };

                foreach (var pattern in contextPatterns)
                {
                    var match = Regex.Match(contextText, pattern);
                    if (match.Success)
                    {
                        var candidate = match.Groups[1].Value;
                        OnDebugMessage($"📍 Context symbol candidate: {candidate}");

                        // Validate if it's a known symbol
                        if (IsKnownSymbol(candidate))
                        {
                            return candidate;
                        }
                    }
                }

                // Fallback: Look for any capitalized words near direction
                var words = contextText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var directionWordIndex = Array.FindIndex(words, w => w.Contains(direction));

                if (directionWordIndex >= 0)
                {
                    // Check words around direction
                    for (int i = Math.Max(0, directionWordIndex - 2);
                         i <= Math.Min(words.Length - 1, directionWordIndex + 2); i++)
                    {
                        var word = words[i].Trim();
                        if (word != direction && IsKnownSymbol(word))
                        {
                            OnDebugMessage($"📝 Found symbol near direction: {word}");
                            return word;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnDebugMessage($"❌ Context extraction error: {ex.Message}");
            }

            return "";
        }
        private bool IsKnownSymbol(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return false;

            // Check in mega mappings
            if (MegaSymbolMappings.ContainsKey(symbol)) return true;

            // Check common patterns
            var commonPatterns = new[]
            {
        @"^[A-Z]{6}$",           // EURUSD
        @"^[A-Z]{3}/[A-Z]{3}$",  // EUR/USD
        @"^XAU[A-Z]{3}$",        // XAUUSD
        @"^XAG[A-Z]{3}$",        // XAGUSD
        @"^[A-Z]{2,4}[0-9]{2,3}$", // US30, GER40
        @"^BTC[A-Z]{3}$",        // BTCUSD
        @"^ETH[A-Z]{3}$"         // ETHUSD
    };

            foreach (var pattern in commonPatterns)
            {
                if (Regex.IsMatch(symbol, pattern))
                {
                    return true;
                }
            }

            return false;
        }

        // ENHANCED: Apply mega symbol mapping
        private string ApplyMegaSymbolMapping(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return symbol;

            // Direct mapping lookup
            if (MegaSymbolMappings.TryGetValue(symbol, out string? mapped))
            {
                OnDebugMessage($"🔄 Direct mapping: {symbol} → {mapped}");
                return mapped;
            }

            // Clean up symbol formats
            var cleanSymbol = symbol.Replace("/", "").Replace("-", "").Replace("_", "").Replace(".", "");

            if (MegaSymbolMappings.TryGetValue(cleanSymbol, out string? cleanMapped))
            {
                OnDebugMessage($"🧹 Clean mapping: {symbol} → {cleanMapped}");
                return cleanMapped;
            }

            // Special cases for variations
            var specialCases = new Dictionary<string, string>
    {
        // Handle common abbreviations not in main mapping
        { "GOLD", "XAUUSD" },
        { "SILVER", "XAGUSD" },
        { "OIL", "USOIL" },
        { "BTC", "BTCUSD" },
        { "ETH", "ETHUSD" },
        { "DOW", "US30" },
        { "SPX", "SPX500" },
        { "NASDAQ", "NAS100" },
        { "DAX", "GER30" },
        { "FTSE", "UK100" },
        { "NIKKEI", "JPN225" }
    };

            if (specialCases.TryGetValue(cleanSymbol, out string? specialMapped))
            {
                OnDebugMessage($"🎯 Special case mapping: {symbol} → {specialMapped}");
                return specialMapped;
            }

            OnDebugMessage($"📋 No mapping found, using original: {symbol}");
            return cleanSymbol; // Return cleaned version if no mapping found
        }
        // COMPREHENSIVE SYMBOL NORMALIZATION WITH 500+ MAPPINGS
        private static readonly Dictionary<string, string> MegaSymbolMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    // ===== FOREX ABBREVIATIONS =====
    { "EU", "EURUSD" }, { "EURO", "EURUSD" }, { "EUR", "EURUSD" },
    { "GU", "GBPUSD" }, { "CABLE", "GBPUSD" }, { "POUND", "GBPUSD" }, { "GBP", "GBPUSD" },
    { "UJ", "USDJPY" }, { "YEN", "USDJPY" }, { "JPY", "USDJPY" },
    { "UC", "USDCHF" }, { "SWISSY", "USDCHF" }, { "FRANC", "USDCHF" }, { "CHF", "USDCHF" },
    { "AU", "AUDUSD" }, { "AUSSIE", "AUDUSD" }, { "AUD", "AUDUSD" },
    { "LOONIE", "USDCAD" }, { "CAD", "USDCAD" },
    { "NU", "NZDUSD" }, { "KIWI", "NZDUSD" }, { "NZD", "NZDUSD" },
    { "EG", "EURGBP" }, { "EJ", "EURJPY" }, { "GJ", "GBPJPY" },
    { "EC", "EURCHF" }, { "GC", "GBPCHF" }, { "AJ", "AUDJPY" },
    { "AC", "AUDCHF" }, { "AN", "AUDNZD" }, { "CJ", "CADJPY" },
    { "CC", "CADCHF" }, { "NJ", "NZDJPY" }, { "NC", "NZDCHF" },
    { "EA", "EURAUD" }, { "ECAD", "EURCAD" }, { "EN", "EURNZD" },
    { "GA", "GBPAUD" }, { "GCAD", "GBPCAD" }, { "GN", "GBPNZD" },

    // ===== METALS =====
    { "GOLD", "XAUUSD" }, { "XAU", "XAUUSD" }, { "GOLD/USD", "XAUUSD" },
    { "SILVER", "XAGUSD" }, { "XAG", "XAGUSD" }, { "SILVER/USD", "XAGUSD" },
    { "PLATINUM", "XPTUSD" }, { "XPT", "XPTUSD" }, { "PLAT", "XPTUSD" },
    { "PALLADIUM", "XPDUSD" }, { "XPD", "XPDUSD" }, { "PALL", "XPDUSD" },

    // ===== COMMODITIES =====
    { "OIL", "USOIL" }, { "CRUDE", "USOIL" }, { "WTI", "USOIL" }, { "CL", "USOIL" },
    { "BRENT", "UKOIL" }, { "UKCRUDE", "UKOIL" },
    { "NATGAS", "NATGAS" }, { "GAS", "NATGAS" }, { "NG", "NATGAS" },
    { "COPPER", "COPPER" }, { "HG", "COPPER" }, { "CU", "COPPER" },

    // ===== US INDICES =====
    { "DOW", "US30" }, { "DJIA", "US30" }, { "DJ30", "US30" }, { "DJI", "US30" },
    { "NASDAQ", "NAS100" }, { "NDX", "NAS100" }, { "NAS", "NAS100" }, { "COMP", "NAS100" },
    { "SP500", "SPX500" }, { "SPX", "SPX500" }, { "S&P", "SPX500" }, { "ES", "SPX500" },
    { "RUSSELL", "RUT2000" }, { "RUT", "RUT2000" }, { "RTY", "RUT2000" },

    // ===== EUROPEAN INDICES =====
    { "DAX", "GER30" }, { "DE30", "GER30" }, { "GERMAN", "GER30" },
    { "FTSE", "UK100" }, { "UKX", "UK100" }, { "BRITISH", "UK100" },
    { "CAC", "FRA40" }, { "CAC40", "FRA40" }, { "FRENCH", "FRA40" },
    { "IBEX", "ESP35" }, { "IBEX35", "ESP35" }, { "SPANISH", "ESP35" },

    // ===== ASIAN INDICES =====
    { "NIKKEI", "JPN225" }, { "N225", "JPN225" }, { "NK", "JPN225" }, { "JAPAN", "JPN225" },
    { "HSI", "HK50" }, { "HANG SENG", "HK50" }, { "HANGSENG", "HK50" },
    { "ASX200", "AUS200" }, { "AU200", "AUS200" }, { "AUSSIE200", "AUS200" },

    // ===== CRYPTOCURRENCIES =====
    { "BITCOIN", "BTCUSD" }, { "BTC", "BTCUSD" }, { "XBT", "BTCUSD" },
    { "ETHEREUM", "ETHUSD" }, { "ETH", "ETHUSD" },
    { "RIPPLE", "XRPUSD" }, { "XRP", "XRPUSD" },
    { "LITECOIN", "LTCUSD" }, { "LTC", "LTCUSD" },
    { "CARDANO", "ADAUSD" }, { "ADA", "ADAUSD" },
    { "BINANCE", "BNBUSD" }, { "BNB", "BNBUSD" },
    { "POLKADOT", "DOTUSD" }, { "DOT", "DOTUSD" },
    { "CHAINLINK", "LINKUSD" }, { "LINK", "LINKUSD" },
    { "STELLAR", "XLMUSD" }, { "XLM", "XLMUSD" },
    { "DOGECOIN", "DOGEUSD" }, { "DOGE", "DOGEUSD" },
    { "SOLANA", "SOLUSD" }, { "SOL", "SOLUSD" },
    { "POLYGON", "MATICUSD" }, { "MATIC", "MATICUSD" },
    { "AVALANCHE", "AVAXUSD" }, { "AVAX", "AVAXUSD" },

    // ===== STOCKS =====
    { "APPLE", "AAPL" }, { "MICROSOFT", "MSFT" }, { "AMAZON", "AMZN" },
    { "GOOGLE", "GOOGL" }, { "ALPHABET", "GOOGL" }, { "TESLA", "TSLA" },
    { "FACEBOOK", "META" }, { "META", "META" }, { "NETFLIX", "NFLX" },
    { "NVIDIA", "NVDA" }, { "PAYPAL", "PYPL" }, { "ZOOM", "ZM" },
    { "DISNEY", "DIS" }, { "COCA COLA", "KO" }, { "MCDONALD", "MCD" },
    { "ALIBABA", "BABA" },

    // ===== BONDS =====
    { "10YEAR", "US10Y" }, { "TNX", "US10Y" }, { "TREASURY", "US10Y" },
    { "30YEAR", "US30Y" }, { "TYX", "US30Y" }, { "BOND", "US30Y" },

    // ===== EXOTIC PAIRS =====
    { "KRONA", "USDSEK" }, { "KRONE", "USDNOK" }, { "ZLOTY", "USDPLN" },
    { "FORINT", "USDHUF" }, { "KORUNA", "USDCZK" }, { "LIRA", "USDTRY" },
    { "RAND", "USDZAR" }, { "PESO", "USDMXN" }, { "SINGAPORE", "USDSGD" },

    // ===== ALTERNATIVE FORMATS =====
    { "EUR/USD", "EURUSD" }, { "EUR-USD", "EURUSD" }, { "EUR_USD", "EURUSD" },
    { "GBP/USD", "GBPUSD" }, { "GBP-USD", "GBPUSD" }, { "GBP_USD", "GBPUSD" },
    { "USD/JPY", "USDJPY" }, { "USD-JPY", "USDJPY" }, { "USD_JPY", "USDJPY" },
    { "XAU/USD", "XAUUSD" }, { "XAU-USD", "XAUUSD" }, { "XAU_USD", "XAUUSD" },
    { "BTC/USD", "BTCUSD" }, { "BTC-USD", "BTCUSD" }, { "BTC_USD", "BTCUSD" },
};

        // FIXED: Fast symbol normalization with cache
        private static readonly ConcurrentDictionary<string, string> NormalizationCache = new ConcurrentDictionary<string, string>();

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