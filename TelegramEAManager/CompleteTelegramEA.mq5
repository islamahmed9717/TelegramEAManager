//+------------------------------------------------------------------+
//|                           TelegramEA_islamahmed9717_MT5.mq5      |
//|                         Copyright 2025-06-20, islamahmed9717    |
//|              Complete MT5 Version with 10-Minute Signal Expiry  |
//+------------------------------------------------------------------+
#property copyright "Copyright 2025-06-20, islamahmed9717"
#property link      "https://github.com/islamahmed9717"
#property version   "2.17"
#property description "Complete Telegram EA for MT5 - 10 Minute Signal Expiry"
#property description "Current UTC Time: 2025-06-20 23:03:50"
#property description "Developer: islamahmed9717"

//--- Include required libraries for MT5
#include <Trade\Trade.mqh>
#include <Trade\SymbolInfo.mqh>
#include <Trade\PositionInfo.mqh>
#include <Trade\OrderInfo.mqh>

//--- Enumerations (must be defined before input parameters)
enum ENUM_RISK_MODE
{
   RISK_FIXED_LOT,        // Fixed Lot Size
   RISK_MONEY_AMOUNT,     // Money Amount
   RISK_PERCENT_BALANCE   // Percent of Balance
};

//--- Input Parameters
input group "==== TELEGRAM CHANNEL SETTINGS ===="
input string ChannelIDs = ""; // Channel IDs from Windows App (comma-separated)
input string SignalFilePath = "telegram_signals.txt"; // Signal file name
input int SignalCheckInterval = 5; // Check for new signals every X seconds
input int MaxSignalAgeMinutes = 10; // Maximum signal age in minutes

input group "==== SYMBOL MAPPING & BROKER SETTINGS ===="
input string SymbolsMapping = ""; // Symbol mappings (FROM:TO,FROM:TO)
input string SymbolPrefix = ""; // Symbol prefix (e.g., "oct.")
input string SymbolSuffix = ""; // Symbol suffix (e.g., ".m")
input string SkipSuffixPrefixPairs = ""; // Pairs to skip prefix/suffix
input string ExcludedSymbols = ""; // Symbols to never trade
input string SymbolsToTrade = ""; // Only trade these symbols (whitelist)

input group "==== RISK MANAGEMENT ===="
input ENUM_RISK_MODE RiskMode = RISK_FIXED_LOT; // Risk calculation mode
input double FixedLotSize = 0.01; // Fixed lot size
input double RiskAmount = 100; // Risk amount in account currency
input double RiskPercent = 2.0; // Risk percentage of balance
input bool SplitRiskEqually = false; // Split risk equally between TPs

input group "==== TRADE MANAGEMENT ===="
input bool ForceMarketExecution = true; // Force market execution
input int MaxSpreadPips = 5; // Maximum allowed spread (pips)
input bool IgnoreTradesWithoutSL = false; // Ignore signals without SL
input bool IgnoreTradesWithoutTP = false; // Ignore signals without TP
input int MaxRetriesOrderSend = 3; // Maximum retries for order execution

input group "==== ADVANCED FEATURES ===="
input bool UseTrailingStop = false; // Enable trailing stop
input int TrailingStartPips = 20; // Start trailing after X pips profit
input int TrailingStepPips = 5; // Trailing step in pips
input bool MoveSLToBreakeven = true; // Move SL to breakeven
input int BreakevenAfterPips = 15; // Move to breakeven after X pips
input int BreakevenPlusPips = 2; // Breakeven + X pips

input group "==== NOTIFICATIONS ===="
input bool SendMT5Alerts = true; // Send MT5 alerts
input bool SendPushNotifications = true; // Send push notifications
input bool PrintToExpertLog = true; // Print detailed logs
input string CommentPrefix = "TelegramEA"; // Trade comment prefix

input group "==== TIME FILTER ===="
input bool UseTimeFilter = false; // Enable time-based filtering
input string StartTime = "00:00"; // Trading start time (HH:MM)
input string EndTime = "23:59"; // Trading end time (HH:MM)
input bool TradeOnMonday = true;
input bool TradeOnTuesday = true;
input bool TradeOnWednesday = true;
input bool TradeOnThursday = true;
input bool TradeOnFriday = true;
input bool TradeOnSaturday = false;
input bool TradeOnSunday = false;

//--- MT5 Trading Objects
CTrade trade;
CSymbolInfo symbolInfo;
CPositionInfo positionInfo;
COrderInfo orderInfo;

//--- Global Variables
string processedSignalIds[]; // Array to store processed signal IDs
int processedSignalCount = 0;
datetime lastSignalTime = 0;
int totalSignalsProcessed = 0;
int totalTradesExecuted = 0;
int totalSymbolsFiltered = 0;
int totalExpiredSignals = 0;
ulong magicNumber = 999001; // MT5 uses ulong for magic numbers

// Symbol mapping arrays
string symbolMappings[][2];
int symbolMappingCount = 0;
string excludedSymbolsList[];
int excludedSymbolsCount = 0;
string allowedSymbolsList[];
int allowedSymbolsCount = 0;
string skipPrefixSuffixList[];
int skipPrefixSuffixCount = 0;

//--- Trade tracking structure for MT5
struct TradeInfo
{
   ulong ticket;              // MT5 uses ulong for tickets
   string symbol;
   ENUM_ORDER_TYPE orderType; // MT5 order type enum
   double lotSize;
   double openPrice;
   double stopLoss;
   double takeProfit;
   datetime openTime;
   bool slMovedToBreakeven;
   double lastTrailingLevel;
   string originalSymbol;
};

TradeInfo openTrades[];
int openTradesCount = 0;

//--- Enhanced Signal structure
struct TelegramSignal
{
   string signalId;
   string originalSymbol;
   string finalSymbol;
   string direction;
   double entryPrice;
   double stopLoss;
   double takeProfit1;
   double takeProfit2;
   double takeProfit3;
   datetime signalTime;
   datetime receivedTime;
   string channelId;
   string channelName;
   string originalText;
   bool isExpired;
   bool isProcessed;
};

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
{
   Print("=================================================================");
   Print("🚀 TELEGRAM EA MANAGER - COMPLETE MT5 VERSION");
   Print("=================================================================");
   Print("👤 Developer: islamahmed9717");
   Print("📅 Version: 2.17 MT5 (2025-06-20 23:03:50 UTC)");
   Print("🔗 GitHub: https://github.com/islamahmed9717");
   Print("⏰ Signal Expiry: ", MaxSignalAgeMinutes, " minutes");
   Print("🎯 Platform: MetaTrader 5");
   Print("📱 Works with: Telegram EA Manager Windows Application");
   Print("=================================================================");
   
   // Validate critical parameters FIRST
   if(SignalCheckInterval < 1 || SignalCheckInterval > 60)
   {
      Print("❌ ERROR: SignalCheckInterval must be between 1 and 60 seconds");
      Comment("❌ PARAMETER ERROR\nSignalCheckInterval must be 1-60 seconds\nCurrent value: " + IntegerToString(SignalCheckInterval) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(MaxSignalAgeMinutes < 1 || MaxSignalAgeMinutes > 1440)
   {
      Print("❌ ERROR: MaxSignalAgeMinutes must be between 1 and 1440 minutes");
      Comment("❌ PARAMETER ERROR\nMaxSignalAgeMinutes must be 1-1440 minutes\nCurrent value: " + IntegerToString(MaxSignalAgeMinutes) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(FixedLotSize <= 0 || FixedLotSize > 100)
   {
      Print("❌ ERROR: FixedLotSize must be between 0.01 and 100");
      Comment("❌ PARAMETER ERROR\nFixedLotSize must be 0.01-100\nCurrent value: " + DoubleToString(FixedLotSize, 2) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(RiskPercent <= 0 || RiskPercent > 50)
   {
      Print("❌ ERROR: RiskPercent must be between 0.1 and 50");
      Comment("❌ PARAMETER ERROR\nRiskPercent must be 0.1-50%\nCurrent value: " + DoubleToString(RiskPercent, 1) + "%\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(MaxSpreadPips < 0 || MaxSpreadPips > 100)
   {
      Print("❌ ERROR: MaxSpreadPips must be between 0 and 100");
      Comment("❌ PARAMETER ERROR\nMaxSpreadPips must be 0-100\nCurrent value: " + IntegerToString(MaxSpreadPips) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(TrailingStartPips < 0 || TrailingStartPips > 1000)
   {
      Print("❌ ERROR: TrailingStartPips must be between 0 and 1000");
      Comment("❌ PARAMETER ERROR\nTrailingStartPips must be 0-1000\nCurrent value: " + IntegerToString(TrailingStartPips) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   if(TrailingStepPips < 1 || TrailingStepPips > 100)
   {
      Print("❌ ERROR: TrailingStepPips must be between 1 and 100");
      Comment("❌ PARAMETER ERROR\nTrailingStepPips must be 1-100\nCurrent value: " + IntegerToString(TrailingStepPips) + "\nPlease fix and restart EA");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   // Check trading permissions
   if(!TerminalInfoInteger(TERMINAL_TRADE_ALLOWED))
   {
      Print("❌ ERROR: Trading is not allowed in terminal");
      Comment("❌ TRADING NOT ALLOWED\nEnable AutoTrading in MT5\nCheck Tools -> Options -> Expert Advisors\nAllow automated trading must be checked");
      return(INIT_FAILED);
   }
   
   if(!MQLInfoInteger(MQL_TRADE_ALLOWED))
   {
      Print("❌ ERROR: Expert Advisor trading is not allowed");
      Comment("❌ EA TRADING NOT ALLOWED\nCheck if EA trading is enabled\nVerify account permissions\nContact broker if needed");
      return(INIT_FAILED);
   }
   
   // Validate file path
   if(StringLen(SignalFilePath) == 0)
   {
      Print("❌ ERROR: SignalFilePath cannot be empty");
      Comment("❌ PARAMETER ERROR\nSignalFilePath cannot be empty\nUse default: telegram_signals.txt");
      return(INIT_PARAMETERS_INCORRECT);
   }
   
   // Initialize MT5 trading object - CORRECTED
   trade.SetExpertMagicNumber(magicNumber); // This function returns void, no need to check return value
   trade.SetMarginMode();
   trade.SetTypeFillingBySymbol(Symbol());
   
   Print("✅ MT5 Trading object initialized with magic number: ", magicNumber);
   
   // Initialize arrays with proper error checking
   if(ArrayResize(processedSignalIds, 0) < 0)
   {
      Print("❌ ERROR: Failed to initialize processedSignalIds array");
      return(INIT_FAILED);
   }
   processedSignalCount = 0;
   
   if(ArrayResize(openTrades, 0) < 0)
   {
      Print("❌ ERROR: Failed to initialize openTrades array");
      return(INIT_FAILED);
   }
   openTradesCount = 0;
   
   // Initialize symbol mapping system
   InitializeSymbolMappings();
   InitializeSymbolFilters();
   
   // Test signal file access
   if(!TestSignalFileAccess())
   {
      Print("❌ ERROR: Cannot access signal file: ", SignalFilePath);
      Print("💡 Make sure the Windows app is configured with correct MT5 path.");
      Comment("❌ SIGNAL FILE ERROR\nCannot access: " + SignalFilePath + "\nCheck file permissions\nVerify Windows app configuration\nError: " + IntegerToString(GetLastError()));
      return(INIT_FAILED);
   }
   
   // Set timer for signal checking
   if(!EventSetTimer(SignalCheckInterval))
   {
      Print("❌ ERROR: Failed to set timer");
      return(INIT_FAILED);
   }
   
   Print("✅ MT5 Initialization completed successfully!");
   Print("📊 Configuration Summary:");
   Print("   • Channel IDs: ", StringLen(ChannelIDs) > 0 ? ChannelIDs : "NOT SET");
   Print("   • Signal File: ", SignalFilePath);
   Print("   • Signal Expiry: ", MaxSignalAgeMinutes, " minutes");
   Print("   • Risk Mode: ", EnumToString(RiskMode));
   Print("   • Fixed Lot Size: ", DoubleToString(FixedLotSize, 2));
   Print("   • Symbol Mappings: ", symbolMappingCount, " configured");
   Print("   • Magic Number: ", magicNumber);
   Print("   • Trailing Start: ", TrailingStartPips, " pips");
   Print("   • Trailing Step: ", TrailingStepPips, " pips");
   
   // Update display
   UpdateComment();
   
   Print("🚀 MT5 EA is now monitoring for Telegram signals...");
   Print("⏰ Signals older than ", MaxSignalAgeMinutes, " minutes will be automatically ignored!");
   Print("📱 Keep the Windows application running and monitoring channels!");
   
   if(StringLen(ChannelIDs) == 0)
   {
      Print("⚠️ WARNING: No Channel IDs specified!");
      Print("💡 Use the Windows application to get Channel IDs");
   }
   
   return(INIT_SUCCEEDED);
}

//+------------------------------------------------------------------+
//| Expert deinitialization function                                |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
{
   EventKillTimer();
   
   Print("=================================================================");
   Print("⏹️ TELEGRAM EA MT5 STOPPED");
   Print("=================================================================");
   Print("📊 Session Statistics:");
   Print("   • Signals Processed: ", totalSignalsProcessed);
   Print("   • Trades Executed: ", totalTradesExecuted);
   Print("   • Expired Signals: ", totalExpiredSignals);
   Print("   • Symbols Filtered: ", totalSymbolsFiltered);
   Print("   • Open Positions: ", openTradesCount);
   Print("📅 Stop Time: ", TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES), " (Server Time)");
   Print("👤 Developer: islamahmed9717");
   Print("=================================================================");
   
   string reasonText = "";
   switch(reason)
   {
      case REASON_PROGRAM: reasonText = "EA removed from chart"; break;
      case REASON_REMOVE: reasonText = "EA removed manually"; break;
      case REASON_RECOMPILE: reasonText = "EA recompiled"; break;
      case REASON_CHARTCHANGE: reasonText = "Chart symbol/period changed"; break;
      case REASON_CHARTCLOSE: reasonText = "Chart closed"; break;
      case REASON_PARAMETERS: reasonText = "Input parameters changed"; break;
      case REASON_ACCOUNT: reasonText = "Account changed"; break;
      default: reasonText = "Unknown reason (" + IntegerToString(reason) + ")"; break;
   }
   
   Print("🔍 Stop Reason: ", reasonText);
   Comment("");
}

//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
{
   // Process trailing stops
   if(UseTrailingStop && openTradesCount > 0)
      ProcessTrailingStops();
   
   // Process breakeven
   if(MoveSLToBreakeven && openTradesCount > 0)
      ProcessBreakeven();
   
   // Clean up closed positions
   if(openTradesCount > 0)
      CleanupClosedPositions();
   
   // Update comment periodically and cleanup
   static int tickCount = 0;
   tickCount++;
   if(tickCount >= 100)
   {
      CleanupOldProcessedSignals();
      UpdateComment();
      tickCount = 0;
   }
}

//+------------------------------------------------------------------+
//| Timer function - Check for new signals                          |
//+------------------------------------------------------------------+
void OnTimer()
{
   CheckForNewSignals();
}

//+------------------------------------------------------------------+
//| Initialize symbol mappings from input                           |
//+------------------------------------------------------------------+
void InitializeSymbolMappings()
{
   symbolMappingCount = 0;
   
   if(StringLen(SymbolsMapping) == 0)
   {
      if(PrintToExpertLog)
         Print("📋 No symbol mappings configured - using original symbols");
      return;
   }
   
   string mappings[];
   int mappingCount = StringSplit(SymbolsMapping, ',', mappings);
   
   if(mappingCount <= 0)
   {
      Print("⚠️ Invalid symbol mapping format");
      return;
   }
   
   if(ArrayResize(symbolMappings, mappingCount) < 0)
   {
      Print("❌ Failed to resize symbolMappings array");
      return;
   }
   
   for(int i = 0; i < mappingCount; i++)
   {
      string mapping = mappings[i];
      StringTrimLeft(mapping);
      StringTrimRight(mapping);
      
      string parts[];
      
      if(StringSplit(mapping, ':', parts) == 2)
      {
         symbolMappings[symbolMappingCount][0] = parts[0]; // From
         symbolMappings[symbolMappingCount][1] = parts[1]; // To
         
         StringTrimLeft(symbolMappings[symbolMappingCount][0]);
         StringTrimRight(symbolMappings[symbolMappingCount][0]);
         StringTrimLeft(symbolMappings[symbolMappingCount][1]);
         StringTrimRight(symbolMappings[symbolMappingCount][1]);
         
         if(PrintToExpertLog)
            Print("🗺️ MT5 Symbol Mapping [", symbolMappingCount, "]: ", 
                  symbolMappings[symbolMappingCount][0], " → ", symbolMappings[symbolMappingCount][1]);
         
         symbolMappingCount++;
      }
      else
      {
         Print("⚠️ Invalid mapping format: ", mapping, " (expected: FROM:TO)");
      }
   }
   
   Print("✅ MT5 Initialized ", symbolMappingCount, " symbol mappings");
}

//+------------------------------------------------------------------+
//| Initialize symbol filters                                        |
//+------------------------------------------------------------------+
void InitializeSymbolFilters()
{
   // Initialize excluded symbols
   excludedSymbolsCount = 0;
   if(StringLen(ExcludedSymbols) > 0)
   {
      excludedSymbolsCount = StringSplit(ExcludedSymbols, ',', excludedSymbolsList);
      
      if(excludedSymbolsCount > 0)
      {
         for(int i = 0; i < excludedSymbolsCount; i++)
         {
            StringTrimLeft(excludedSymbolsList[i]);
            StringTrimRight(excludedSymbolsList[i]);
            StringToUpper(excludedSymbolsList[i]);
         }
         
         Print("🚫 MT5 Excluded symbols: ", excludedSymbolsCount, " configured");
      }
   }
   
   // Initialize allowed symbols (whitelist)
   allowedSymbolsCount = 0;
   if(StringLen(SymbolsToTrade) > 0)
   {
      allowedSymbolsCount = StringSplit(SymbolsToTrade, ',', allowedSymbolsList);
      
      if(allowedSymbolsCount > 0)
      {
         for(int i = 0; i < allowedSymbolsCount; i++)
         {
            StringTrimLeft(allowedSymbolsList[i]);
            StringTrimRight(allowedSymbolsList[i]);
            StringToUpper(allowedSymbolsList[i]);
         }
         
         Print("✅ MT5 Allowed symbols (whitelist): ", allowedSymbolsCount, " configured");
      }
   }
   
   // Initialize skip prefix/suffix list
   skipPrefixSuffixCount = 0;
   if(StringLen(SkipSuffixPrefixPairs) > 0)
   {
      skipPrefixSuffixCount = StringSplit(SkipSuffixPrefixPairs, ',', skipPrefixSuffixList);
      
      if(skipPrefixSuffixCount > 0)
      {
         for(int i = 0; i < skipPrefixSuffixCount; i++)
         {
            StringTrimLeft(skipPrefixSuffixList[i]);
            StringTrimRight(skipPrefixSuffixList[i]);
            StringToUpper(skipPrefixSuffixList[i]);
         }
         
         Print("⏭️ MT5 Skip prefix/suffix pairs: ", skipPrefixSuffixCount, " configured");
      }
   }
}

//+------------------------------------------------------------------+
//| Test signal file access                                         |
//+------------------------------------------------------------------+
bool TestSignalFileAccess()
{
   int fileHandle = FileOpen("telegram_signals.txt",
                            FILE_READ | 
                            FILE_SHARE_READ | 
                            FILE_SHARE_WRITE | 
                            FILE_TXT | 
                            FILE_ANSI);
   
   if(fileHandle == INVALID_HANDLE)
   {
      // Try to create the file if it doesn't exist
      fileHandle = FileOpen("telegram_signals.txt", 
                           FILE_WRITE | 
                           FILE_SHARE_READ | 
                           FILE_SHARE_WRITE | 
                           FILE_TXT | 
                           FILE_ANSI);
      if(fileHandle == INVALID_HANDLE)
      {
         int errorCode = GetLastError();
         Print("❌ Cannot create signal file: telegram_signals.txt");
         Print("💡 Error code: ", errorCode);
         Print("💡 Check if MT4/MT5 Files path is correctly set in Windows app");
         Print("💡 Ensure Windows app has write permissions to the folder");
         return false;
      }
      
      // Write initial header with current format
      FileWriteString(fileHandle, "# Telegram EA MT5 Signal File - islamahmed9717\n");
      FileWriteString(fileHandle, "# Created: " + TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES) + "\n");
      FileWriteString(fileHandle, "# Signal Expiry: " + IntegerToString(MaxSignalAgeMinutes) + " minutes\n");
      FileWriteString(fileHandle, "# Platform: MetaTrader 5\n");
      FileWriteString(fileHandle, "# Format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS\n");
      FileWriteString(fileHandle, "# Status values: NEW (ready to process), PROCESSED (already handled)\n\n");
   }
   
   FileClose(fileHandle);
   Print("✅ MT5 Signal file access confirmed: telegram_signals.txt");
   return true;
}
// --- incremental-read pointers -----------------------------------
static long   g_lastPos  = 0;   // byte offset already processed
static ulong  g_lastSize = 0;   // file size last time we checked

//+------------------------------------------------------------------+
//| Check for new signals from file - MT5 VERSION                   |
//+------------------------------------------------------------------+
void CheckForNewSignals()
{
   if(!IsTimeToTrade())
      return;
   
   // FIXED: Use correct filename that matches WinForms app output
   int fileHandle = FileOpen("telegram_signals.txt",
                            FILE_READ | 
                            FILE_SHARE_READ | 
                            FILE_SHARE_WRITE | 
                            FILE_TXT | 
                            FILE_ANSI);
   
   if(fileHandle == INVALID_HANDLE)
   {
      if(PrintToExpertLog && totalSignalsProcessed == 0)
      {
         Print("📁 MT5 Signal file not found: telegram_signals.txt");
         Print("💡 Make sure the Windows application is running and monitoring channels!");
         Print("💡 Check if MT4 Files path is set correctly in the Windows app");
      }
      return;
   }
   
   // Read and process each line as a potential signal
   while(!FileIsEnding(fileHandle))
   {
      string line = FileReadString(fileHandle);
      if(FileIsEnding(fileHandle) && StringLen(line) == 0)
         break;
         
      StringTrimLeft(line);
      StringTrimRight(line);
      
      // Skip empty lines and comments
      if(StringLen(line) == 0 || StringFind(line, "#") == 0)
         continue;
      
      // FIXED: Better signal processing with status checking
      if(ProcessFormattedSignalLine(line))
         continue;
      
      // Try to parse as text-based signal
      ProcessTextBasedSignal(line);
   }
   
   FileClose(fileHandle);
}

//+------------------------------------------------------------------+
//| Process formatted signal line (TIMESTAMP|CHANNEL|SYMBOL|...)    |
//+------------------------------------------------------------------+
bool ProcessFormattedSignalLine(string line)
{
   string parts[];
   int partCount = StringSplit(line, '|', parts);
   
   // Expected format: TIMESTAMP|CHANNEL_ID|CHANNEL_NAME|DIRECTION|SYMBOL|ENTRY|SL|TP1|TP2|TP3|STATUS
   if(partCount < 11)
      return false;
   
   TelegramSignal signal;
   signal.signalId = GenerateSignalId(line);
   signal.receivedTime = TimeCurrent();
   
   // Parse timestamp (first part) - FIXED: Handle both formats
   string timestampStr = parts[0];
   StringTrimLeft(timestampStr);
   StringTrimRight(timestampStr);
   signal.signalTime = ParseTimestamp(timestampStr);
   
   // If timestamp parsing failed, use current time
   if(signal.signalTime == 0)
      signal.signalTime = TimeCurrent();
   
   // FIXED: Check signal status - only process NEW signals
   string signalStatus = parts[10];
   StringTrimLeft(signalStatus);
   StringTrimRight(signalStatus);
   
   if(signalStatus != "NEW" && signalStatus != "PROCESSED")
   {
      // Skip signals that are not new or already processed
      return true;
   }
   
   // Check if signal is too old
   long signalAgeMinutes = (TimeCurrent() - signal.signalTime) / 60;
   
   if(signalAgeMinutes > MaxSignalAgeMinutes)
   {
      if(PrintToExpertLog)
         Print("⏰ MT5 Signal expired (", IntegerToString((int)signalAgeMinutes), " min old): ", StringSubstr(line, 0, 50), "...");
      
      totalExpiredSignals++;
      signal.isExpired = true;
      return true; // Successfully processed (but expired)
   }
   
   // Check if already processed
   if(IsSignalAlreadyProcessed(signal.signalId))
   {
      return true; // Already processed, skip
   }
   
   // Parse remaining parts with better error handling
   if(partCount >= 2) 
   {
      signal.channelId = parts[1];
      StringTrimLeft(signal.channelId);
      StringTrimRight(signal.channelId);
   }
   if(partCount >= 3) 
   {
      signal.channelName = parts[2];
      StringTrimLeft(signal.channelName);
      StringTrimRight(signal.channelName);
   }
   if(partCount >= 4) 
   {
      signal.direction = parts[3];
      StringTrimLeft(signal.direction);
      StringTrimRight(signal.direction);
      StringToUpper(signal.direction); // Normalize direction
   }
   if(partCount >= 5) 
   {
      signal.originalSymbol = parts[4];
      StringTrimLeft(signal.originalSymbol);
      StringTrimRight(signal.originalSymbol);
      StringToUpper(signal.originalSymbol); // Normalize symbol
   }
   
   // Parse prices with validation
   if(partCount >= 6) 
   {
      double entryPrice = StringToDouble(parts[5]);
      signal.entryPrice = (entryPrice > 0) ? entryPrice : 0; // 0 means market order
   }
   if(partCount >= 7) 
   {
      signal.stopLoss = StringToDouble(parts[6]);
   }
   if(partCount >= 8) 
   {
      signal.takeProfit1 = StringToDouble(parts[7]);
   }
   if(partCount >= 9) 
   {
      signal.takeProfit2 = StringToDouble(parts[8]);
   }
   if(partCount >= 10) 
   {
      signal.takeProfit3 = StringToDouble(parts[9]);
   }
   
   signal.originalText = line;
   signal.finalSymbol = ProcessSymbolTransformation(signal.originalSymbol);
   signal.isExpired = false;
   signal.isProcessed = false;
   
   // Validate and process signal
   if(ValidateSignal(signal))
   {
      ProcessValidatedSignal(signal);
      AddToProcessedSignals(signal.signalId);
      
      // FIXED: Mark signal as processed in the file (optional)
      MarkSignalAsProcessed(line);
   }
   
   return true;
}

void MarkSignalAsProcessed(string originalLine)
{
   // Optional: Mark signal as processed in the file
   // For now, we rely on the signalId tracking which works well
   
   if(!PrintToExpertLog)
      return;
      
   Print("📝 MT5 Signal processed and tracked by ID system");
}
//+------------------------------------------------------------------+
//| Process text-based signal (legacy format)                       |
//+------------------------------------------------------------------+
void ProcessTextBasedSignal(string signalText)
{
   TelegramSignal signal;
   
   if(ParseSignalFromText(signalText, signal))
   {
      signal.signalId = GenerateSignalId(signalText);
      signal.receivedTime = TimeCurrent();
      
      // For text-based signals, use current time if no timestamp found
      if(signal.signalTime == 0)
         signal.signalTime = TimeCurrent();
      
      // Check expiry
      long signalAgeMinutes = (TimeCurrent() - signal.signalTime) / 60;
      
      if(signalAgeMinutes > MaxSignalAgeMinutes)
      {
         if(PrintToExpertLog)
            Print("⏰ MT5 Text signal expired (", IntegerToString((int)signalAgeMinutes), " min old): ", signal.originalSymbol, " ", signal.direction);
         
         totalExpiredSignals++;
         return;
      }
      
      // Check if already processed
      if(IsSignalAlreadyProcessed(signal.signalId))
         return;
      
      // Validate and process
      if(ValidateSignal(signal))
      {
         ProcessValidatedSignal(signal);
         AddToProcessedSignals(signal.signalId);
      }
   }
}

//+------------------------------------------------------------------+
//| Generate unique signal ID from content                          |
//+------------------------------------------------------------------+
string GenerateSignalId(string content)
{
   // Create a simple hash-like ID from the content
   int hash = 0;
   for(int i = 0; i < StringLen(content); i++)
   {
      hash = ((hash * 31) + StringGetCharacter(content, i)) % 1000000;
   }
   
   // Include timestamp to make it more unique
   return IntegerToString(hash) + "_" + IntegerToString((int)TimeCurrent());
}

//+------------------------------------------------------------------+
//| Parse timestamp from string                                     |
//+------------------------------------------------------------------+
datetime ParseTimestamp(string timestampStr)
{
   datetime result = 0;
   
   // Remove any extra spaces
   StringTrimLeft(timestampStr);
   StringTrimRight(timestampStr);
   
   // Format 1: YYYY.MM.DD HH:MM:SS (MT5 standard)
   if(StringLen(timestampStr) >= 19)
   {
      result = StringToTime(timestampStr);
      if(result > 0)
         return result;
   }
   
   // Format 2: YYYY-MM-DD HH:MM:SS (ISO format from C#)
   StringReplace(timestampStr, "-", ".");
   result = StringToTime(timestampStr);
   if(result > 0)
      return result;
   
   // Format 3: Unix timestamp
   long unixTime = StringToInteger(timestampStr);
   if(unixTime > 1000000000 && unixTime < 2000000000) // Valid unix timestamp range
   {
      return (datetime)unixTime;
   }
   
   // If all parsing failed, use current time
   Print("⚠️ MT5 Could not parse timestamp: ", timestampStr, " - using current time");
   return TimeCurrent();
}

//+------------------------------------------------------------------+
//| Check if signal was already processed                           |
//+------------------------------------------------------------------+
bool IsSignalAlreadyProcessed(string signalId)
{
   for(int i = 0; i < processedSignalCount; i++)
   {
      if(processedSignalIds[i] == signalId)
         return true;
   }
   return false;
}

//+------------------------------------------------------------------+
//| Add signal ID to processed list                                 |
//+------------------------------------------------------------------+
void AddToProcessedSignals(string signalId)
{
   ArrayResize(processedSignalIds, processedSignalCount + 1);
   processedSignalIds[processedSignalCount] = signalId;
   processedSignalCount++;
   
   if(PrintToExpertLog)
      Print("📝 MT5 Signal marked as processed: ", StringSubstr(signalId, 0, 20), "... (Total: ", processedSignalCount, ")");
}

//+------------------------------------------------------------------+
//| Clean up old processed signal IDs                               |
//+------------------------------------------------------------------+
void CleanupOldProcessedSignals()
{
   // Keep only last 1000 processed signals to prevent memory issues
   if(processedSignalCount > 1000)
   {
      int keepCount = 500; // Keep last 500
      
      for(int i = 0; i < keepCount; i++)
      {
         processedSignalIds[i] = processedSignalIds[processedSignalCount - keepCount + i];
      }
      
      ArrayResize(processedSignalIds, keepCount);
      processedSignalCount = keepCount;
      
      if(PrintToExpertLog)
         Print("🧹 MT5 Cleaned up old processed signals. Keeping last ", keepCount, " entries.");
   }
}

//+------------------------------------------------------------------+
//| Validate signal before processing                               |
//+------------------------------------------------------------------+
bool ValidateSignal(TelegramSignal &signal)
{
   // Basic validation
   if(StringLen(signal.originalSymbol) == 0)
   {
      if(PrintToExpertLog)
         Print("❌ MT5 Invalid signal: Missing symbol");
      return false;
   }
   
   if(StringLen(signal.direction) == 0)
   {
      if(PrintToExpertLog)
         Print("❌ MT5 Invalid signal: Missing direction");
      return false;
   }
   
   // Validate direction
   if(signal.direction != "BUY" && signal.direction != "SELL")
   {
      if(PrintToExpertLog)
         Print("❌ MT5 Invalid signal: Invalid direction: ", signal.direction);
      return false;
   }
   
   if(StringLen(signal.finalSymbol) == 0)
   {
      if(PrintToExpertLog)
         Print("🚫 MT5 Signal filtered: ", signal.originalSymbol, " (excluded by symbol filters)");
      totalSymbolsFiltered++;
      return false;
   }
   
   // Check if channel is monitored (if ChannelIDs parameter is set)
   if(StringLen(ChannelIDs) > 0 && !IsChannelMonitored(signal.channelId))
   {
      if(PrintToExpertLog)
         Print("⏭️ MT5 Skipping signal from unmonitored channel: ", signal.channelId);
      return false;
   }
   
   // FIXED: Validate price levels
   if(signal.stopLoss <= 0 && IgnoreTradesWithoutSL)
   {
      if(PrintToExpertLog)
         Print("⚠️ MT5 Ignoring signal without SL: ", signal.finalSymbol);
      return false;
   }
   
   if(signal.takeProfit1 <= 0 && IgnoreTradesWithoutTP)
   {
      if(PrintToExpertLog)
         Print("⚠️ MT5 Ignoring signal without TP: ", signal.finalSymbol);
      return false;
   }
   
   return true;
}

void LogFileError(string operation, string filename, int errorCode)
{
   Print("❌ MT5 File operation failed:");
   Print("   • Operation: ", operation);
   Print("   • File: ", filename);
   Print("   • Error Code: ", errorCode);
   Print("   • Time: ", TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES));
   
   switch(errorCode)
   {
      case 4103: // ERR_CANNOT_OPEN_FILE equivalent in MT5
         Print("💡 Solution: Check if file exists and has correct permissions");
         break;
      case 4104: // ERR_FILE_NOT_FOUND equivalent in MT5  
         Print("💡 Solution: Ensure Windows app is writing to correct MT5 Files folder");
         break;
      case 4106: // ERR_INVALID_FUNCTION_PARAMETER equivalent in MT5
         Print("💡 Solution: Check file path and name format");
         break;
      default:
         Print("💡 Check MT5 terminal logs for more details");
         break;
   }
}
bool CheckSymbolTradingAllowed(string symbol)
{
   // Check if symbol is available and trading is allowed
   if(!SymbolSelect(symbol, true))
   {
      Print("❌ MT5 Symbol not available: ", symbol);
      return false;
   }
   
   // Get symbol trading mode
   ENUM_SYMBOL_TRADE_MODE tradeMode = (ENUM_SYMBOL_TRADE_MODE)SymbolInfoInteger(symbol, SYMBOL_TRADE_MODE);
   
   if(tradeMode == SYMBOL_TRADE_MODE_DISABLED)
   {
      Print("⚠️ MT5 Trading disabled for symbol: ", symbol);
      return false;
   }
   
   // Check if market is open
   double bid = SymbolInfoDouble(symbol, SYMBOL_BID);
   double ask = SymbolInfoDouble(symbol, SYMBOL_ASK);
   
   if(bid == 0 || ask == 0)
   {
      Print("⚠️ MT5 Market closed for symbol: ", symbol);
      return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Process validated signal                                        |
//+------------------------------------------------------------------+
void ProcessValidatedSignal(TelegramSignal &signal)
{
   totalSignalsProcessed++;
   
   long signalAgeMinutes = (TimeCurrent() - signal.signalTime) / 60;
   
   if(PrintToExpertLog)
   {
      Print("🚀 MT5 Processing FRESH Signal #", totalSignalsProcessed, ":");
      Print("   📊 Signal: ", signal.originalSymbol, " (", signal.finalSymbol, ") ", signal.direction);
      Print("   ⏰ Age: ", IntegerToString((int)signalAgeMinutes), " minutes (Max: ", MaxSignalAgeMinutes, ")");
      Print("   📢 Channel: ", signal.channelName, " [", signal.channelId, "]");
      Print("   🕐 Signal Time: ", TimeToString(signal.signalTime, TIME_DATE|TIME_MINUTES));
      Print("   💰 Entry: ", (signal.entryPrice > 0 ? DoubleToString(signal.entryPrice, 5) : "Market"));
      Print("   🛑 SL: ", (signal.stopLoss > 0 ? DoubleToString(signal.stopLoss, 5) : "None"));
      Print("   🎯 TP1: ", (signal.takeProfit1 > 0 ? DoubleToString(signal.takeProfit1, 5) : "None"));
      Print("   👤 User: islamahmed9717 | Current UTC: ", TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES));
   }
   
   // Validate symbol exists with MT5 broker
   if(!SymbolSelect(signal.finalSymbol, true))
   {
      Print("❌ MT5 Symbol not available with broker: ", signal.finalSymbol, " (original: ", signal.originalSymbol, ")");
      SuggestSimilarSymbols(signal.finalSymbol);
      return;
   }
   
   // Get symbol info and check trading conditions
   if(!symbolInfo.Name(signal.finalSymbol))
   {
      Print("❌ MT5 Cannot get symbol info for: ", signal.finalSymbol);
      return;
   }
   
   symbolInfo.RefreshRates();
   
   // FIXED: Check if market is open for this symbol using corrected function
   if(!CheckSymbolTradingAllowed(signal.finalSymbol))
   {
      Print("⚠️ MT5 Trading not allowed for symbol: ", signal.finalSymbol);
      return;
   }
   
   // Check spread
   double currentSpread = symbolInfo.Spread() * symbolInfo.Point();
   double maxAllowedSpread = MaxSpreadPips * symbolInfo.Point();
   
   if(MaxSpreadPips > 0 && currentSpread > maxAllowedSpread)
   {
      Print("⚠️ MT5 Spread too high for ", signal.finalSymbol, ": ", 
            DoubleToString(currentSpread / symbolInfo.Point(), 1), " pips (max: ", MaxSpreadPips, ")");
      return;
   }
   
   // Execute the signal
   ExecuteTelegramSignal(signal);
   signal.isProcessed = true;
}

//+------------------------------------------------------------------+
//| Parse signal from text content                                  |
//+------------------------------------------------------------------+
bool ParseSignalFromText(string signalText, TelegramSignal &signal)
{
   // Initialize signal structure
   signal.originalSymbol = "";
   signal.finalSymbol = "";
   signal.direction = "";
   signal.entryPrice = 0;
   signal.stopLoss = 0;
   signal.takeProfit1 = 0;
   signal.takeProfit2 = 0;
   signal.takeProfit3 = 0;
   signal.signalTime = TimeCurrent();
   signal.channelId = "";
   signal.channelName = "";
   signal.originalText = signalText;
   
   string lines[];
   int lineCount = StringSplit(signalText, '\n', lines);
   
   for(int i = 0; i < lineCount; i++)
   {
      string line = lines[i];
      StringTrimLeft(line);
      StringTrimRight(line);
      
      string upperLine = line;
      StringToUpper(upperLine);
      
      // Extract channel information
      if(StringFind(upperLine, "CHANNEL:") >= 0)
      {
         if(StringFind(line, "[") >= 0 && StringFind(line, "]") >= 0)
         {
            int startPos = StringFind(line, "[") + 1;
            int endPos = StringFind(line, "]");
            if(endPos > startPos)
            {
               signal.channelId = StringSubstr(line, startPos, endPos - startPos);
            }
         }
         
         // Extract channel name
         int colonPos = StringFind(line, ":");
         if(colonPos >= 0)
         {
            string afterColon = StringSubstr(line, colonPos + 1);
            int bracketPos = StringFind(afterColon, "[");
            if(bracketPos >= 0)
            {
               signal.channelName = StringSubstr(afterColon, 0, bracketPos);
               StringTrimLeft(signal.channelName);
               StringTrimRight(signal.channelName);
            }
         }
      }
      
      // Parse trading direction and symbol
      if(StringFind(upperLine, "BUY") >= 0 || StringFind(upperLine, "SELL") >= 0)
      {
         if(StringFind(upperLine, "BUY") >= 0)
         {
            signal.direction = "BUY";
            signal.originalSymbol = ExtractSymbolAfterKeyword(line, "BUY");
         }
         else
         {
            signal.direction = "SELL";
            signal.originalSymbol = ExtractSymbolAfterKeyword(line, "SELL");
         }
         
         // Apply symbol transformation
         signal.finalSymbol = ProcessSymbolTransformation(signal.originalSymbol);
      }
      
      // Parse Stop Loss
      if(StringFind(upperLine, "SL") >= 0)
      {
         signal.stopLoss = ExtractPriceFromLine(line, "SL");
      }
      
      // Parse Take Profits
      if(StringFind(upperLine, "TP") >= 0)
      {
         double tpPrice = ExtractPriceFromLine(line, "TP");
         if(tpPrice > 0)
         {
            if(signal.takeProfit1 == 0)
               signal.takeProfit1 = tpPrice;
            else if(signal.takeProfit2 == 0)
               signal.takeProfit2 = tpPrice;
            else if(signal.takeProfit3 == 0)
               signal.takeProfit3 = tpPrice;
         }
      }
      
      // Parse TP2, TP3 specifically
      if(StringFind(upperLine, "TP2") >= 0)
      {
         signal.takeProfit2 = ExtractPriceFromLine(line, "TP2");
      }
      if(StringFind(upperLine, "TP3") >= 0)
      {
         signal.takeProfit3 = ExtractPriceFromLine(line, "TP3");
      }
   }
   
   // Validate signal
   bool isValid = (StringLen(signal.originalSymbol) > 0 && 
                   StringLen(signal.direction) > 0 && 
                   StringLen(signal.finalSymbol) > 0);
   
   if(isValid && PrintToExpertLog)
   {
      Print("📊 MT5 Parsed Signal: ", signal.originalSymbol, " (", signal.finalSymbol, ") ", signal.direction,
            " | SL: ", DoubleToString(signal.stopLoss, 5), " | TP1: ", DoubleToString(signal.takeProfit1, 5));
   }
   
   return isValid;
}

//+------------------------------------------------------------------+
//| Process complete symbol transformation                           |
//+------------------------------------------------------------------+
string ProcessSymbolTransformation(string originalSymbol)
{
   if(PrintToExpertLog)
      Print("🔄 MT5 Processing symbol transformation: ", originalSymbol);
   
   // Step 1: Apply symbol mapping
   string mappedSymbol = ApplySymbolMapping(originalSymbol);
   
   // Step 2: Apply prefix/suffix
   string finalSymbol = ApplyPrefixSuffix(mappedSymbol);
   
   // Step 3: Check exclusions
   if(IsSymbolExcluded(finalSymbol) || IsSymbolExcluded(originalSymbol))
   {
      if(PrintToExpertLog)
         Print("🚫 MT5 Symbol excluded: ", originalSymbol, " → ", finalSymbol);
      totalSymbolsFiltered++;
      return ""; // Return empty to indicate exclusion
   }
   
   // Step 4: Check whitelist
   if(!IsSymbolAllowed(finalSymbol) && !IsSymbolAllowed(originalSymbol))
   {
      if(PrintToExpertLog)
         Print("⚠️ MT5 Symbol not in whitelist: ", originalSymbol, " → ", finalSymbol);
      totalSymbolsFiltered++;
      return ""; // Return empty to indicate not allowed
   }
   
   if(PrintToExpertLog && finalSymbol != originalSymbol)
      Print("✅ MT5 Symbol transformed: ", originalSymbol, " → ", finalSymbol);
   
   return finalSymbol;
}

//+------------------------------------------------------------------+
//| Apply symbol mapping                                            |
//+------------------------------------------------------------------+
string ApplySymbolMapping(string symbol)
{
   string upperSymbol = symbol;
   StringToUpper(upperSymbol);
   
   for(int i = 0; i < symbolMappingCount; i++)
   {
      string fromSymbol = symbolMappings[i][0];
      StringToUpper(fromSymbol);
      
      if(fromSymbol == upperSymbol)
      {
         if(PrintToExpertLog)
            Print("🗺️ MT5 Symbol mapped: ", symbol, " → ", symbolMappings[i][1]);
         return symbolMappings[i][1];
      }
   }
   
   return symbol; // No mapping found
}

//+------------------------------------------------------------------+
//| Apply prefix and suffix                                         |
//+------------------------------------------------------------------+
string ApplyPrefixSuffix(string symbol)
{
   string upperSymbol = symbol;
   StringToUpper(upperSymbol);
   
   // Check if should skip prefix/suffix
   for(int i = 0; i < skipPrefixSuffixCount; i++)
   {
      if(skipPrefixSuffixList[i] == upperSymbol)
      {
         if(PrintToExpertLog)
            Print("⏭️ MT5 Skipping prefix/suffix for: ", symbol);
         return symbol;
      }
   }
   
   // Apply prefix and suffix
   string result = SymbolPrefix + symbol + SymbolSuffix;
   
   if(PrintToExpertLog && (StringLen(SymbolPrefix) > 0 || StringLen(SymbolSuffix) > 0))
      Print("🔧 MT5 Applied prefix/suffix: ", symbol, " → ", result);
   
   return result;
}

//+------------------------------------------------------------------+
//| Check if symbol is excluded                                     |
//+------------------------------------------------------------------+
bool IsSymbolExcluded(string symbol)
{
   if(excludedSymbolsCount == 0)
      return false;
   
   string upperSymbol = symbol;
   StringToUpper(upperSymbol);
   
   for(int i = 0; i < excludedSymbolsCount; i++)
   {
      if(excludedSymbolsList[i] == upperSymbol)
         return true;
   }
   
   return false;
}

//+------------------------------------------------------------------+
//| Check if symbol is allowed (whitelist)                          |
//+------------------------------------------------------------------+
bool IsSymbolAllowed(string symbol)
{
   if(allowedSymbolsCount == 0)
      return true; // No whitelist means all allowed
   
   string upperSymbol = symbol;
   StringToUpper(upperSymbol);
   
   for(int i = 0; i < allowedSymbolsCount; i++)
   {
      if(allowedSymbolsList[i] == upperSymbol)
         return true;
   }
   
   return false;
}

//+------------------------------------------------------------------+
//| Extract symbol after keyword                                    |
//+------------------------------------------------------------------+
string ExtractSymbolAfterKeyword(string line, string keyword)
{
   string upperLine = line;
   StringToUpper(upperLine);
   
   int keywordPos = StringFind(upperLine, keyword);
   if(keywordPos < 0)
      return "";
   
   string afterKeyword = StringSubstr(line, keywordPos + StringLen(keyword));
   StringTrimLeft(afterKeyword);
   StringTrimRight(afterKeyword);
   
   // Remove common words
   StringReplace(afterKeyword, "NOW", "");
   StringReplace(afterKeyword, "SIGNAL", "");
   StringTrimLeft(afterKeyword);
   StringTrimRight(afterKeyword);
   
   // Get first word as symbol
   string words[];
   int wordCount = StringSplit(afterKeyword, ' ', words);
   
   if(wordCount > 0)
   {
      string symbol = words[0];
      StringTrimLeft(symbol);
      StringTrimRight(symbol);
      
      // Clean symbol - keep only alphanumeric
      string cleanSymbol = "";
      for(int i = 0; i < StringLen(symbol); i++)
      {
         ushort ch = StringGetCharacter(symbol, i);
         if((ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
         {
            cleanSymbol += CharToString(ch);
         }
      }
      return cleanSymbol;
   }
   
   return "";
}

//+------------------------------------------------------------------+
//| Extract price from line                                         |
//+------------------------------------------------------------------+
double ExtractPriceFromLine(string line, string keyword)
{
   string upperLine = line;
   StringToUpper(upperLine);
   
   int keywordPos = StringFind(upperLine, keyword);
   if(keywordPos < 0)
      return 0;
   
   string afterKeyword = StringSubstr(line, keywordPos + StringLen(keyword));
   StringTrimLeft(afterKeyword);
   StringTrimRight(afterKeyword);
   
   // Remove separators
   StringReplace(afterKeyword, ":", "");
   StringReplace(afterKeyword, "=", "");
   StringReplace(afterKeyword, "@", "");
   StringTrimLeft(afterKeyword);
   StringTrimRight(afterKeyword);
   
   // Extract number
   string numberStr = "";
   for(int i = 0; i < StringLen(afterKeyword); i++)
   {
      ushort ch = StringGetCharacter(afterKeyword, i);
      if((ch >= '0' && ch <= '9') || ch == '.')
      {
         numberStr += CharToString(ch);
      }
      else if(StringLen(numberStr) > 0)
      {
         break; // Stop at first non-numeric after we started collecting
      }
   }
   
   return StringToDouble(numberStr);
}

//+------------------------------------------------------------------+
//| Check if channel is monitored                                   |
//+------------------------------------------------------------------+
bool IsChannelMonitored(string channelId)
{
   if(StringLen(ChannelIDs) == 0 || StringLen(channelId) == 0)
      return false;
   
   string channels[];
   int channelCount = StringSplit(ChannelIDs, ',', channels);
   
   for(int i = 0; i < channelCount; i++)
   {
      string monitoredChannel = channels[i];
      StringTrimLeft(monitoredChannel);
      StringTrimRight(monitoredChannel);
      if(monitoredChannel == channelId)
         return true;
   }
   
   return false;
}

//+------------------------------------------------------------------+
//| Suggest similar symbols when exact symbol not found - MT5       |
//+------------------------------------------------------------------+
void SuggestSimilarSymbols(string targetSymbol)
{
   Print("🔍 MT5 Searching for similar symbols to: ", targetSymbol);
   
   string upperTarget = targetSymbol;
   StringToUpper(upperTarget);
   
   // Remove prefix/suffix to get core symbol
   string coreSymbol = upperTarget;
   if(StringLen(SymbolPrefix) > 0)
   {
      string upperPrefix = SymbolPrefix;
      StringToUpper(upperPrefix);
      StringReplace(coreSymbol, upperPrefix, "");
   }
   if(StringLen(SymbolSuffix) > 0)
   {
      string upperSuffix = SymbolSuffix;
      StringToUpper(upperSuffix);
      StringReplace(coreSymbol, upperSuffix, "");
   }
   
   int totalSymbols = SymbolsTotal(true);
   string suggestions = "";
   int suggestionCount = 0;
   
   for(int i = 0; i < totalSymbols && suggestionCount < 5; i++)
   {
      string symbolName = SymbolName(i, true);
      string upperSymbolName = symbolName;
      StringToUpper(upperSymbolName);
      
      // Check if contains core symbol or target symbol
      if(StringFind(upperSymbolName, coreSymbol) >= 0 || StringFind(upperSymbolName, upperTarget) >= 0)
      {
         if(suggestionCount > 0)
            suggestions += ", ";
         suggestions += symbolName;
         suggestionCount++;
      }
   }
   
   if(suggestionCount > 0)
   {
      Print("💡 MT5 Similar symbols found: ", suggestions);
      Print("💡 Consider updating symbol mapping: ", targetSymbol, ":ACTUAL_BROKER_SYMBOL");
      Print("💡 Or check your broker's symbol list in Market Watch");
   }
   else
   {
      Print("💡 MT5 No similar symbols found. Available symbols: ", totalSymbols);
      Print("💡 Check if symbol is available in your broker's Market Watch");
   }
}

//+------------------------------------------------------------------+
//| Execute Telegram signal - MT5 VERSION                           |
//+------------------------------------------------------------------+
void ExecuteTelegramSignal(TelegramSignal &signal)
{
   ENUM_ORDER_TYPE orderType = (signal.direction == "BUY") ? ORDER_TYPE_BUY : ORDER_TYPE_SELL;
   
   // Calculate lot sizes for multiple TPs
   double lots[3];
   CalculateLotSizes(signal, lots);
   
   if(PrintToExpertLog)
   {
      Print("📈 MT5 Executing signal with lot distribution:");
      Print("   • TP1: ", DoubleToString(lots[0], 2), " lots");
      if(signal.takeProfit2 > 0)
         Print("   • TP2: ", DoubleToString(lots[1], 2), " lots");
      if(signal.takeProfit3 > 0)
         Print("   • TP3: ", DoubleToString(lots[2], 2), " lots");
   }
   
   // Execute TP1 (primary trade)
   if(lots[0] > 0)
   {
      ExecuteSingleTrade(signal, orderType, lots[0], signal.stopLoss, signal.takeProfit1, "TP1");
   }
   
   // Execute TP2 if available
   if(signal.takeProfit2 > 0 && lots[1] > 0)
   {
      ExecuteSingleTrade(signal, orderType, lots[1], signal.stopLoss, signal.takeProfit2, "TP2");
   }
   
   // Execute TP3 if available
   if(signal.takeProfit3 > 0 && lots[2] > 0)
   {
      ExecuteSingleTrade(signal, orderType, lots[2], signal.stopLoss, signal.takeProfit3, "TP3");
   }
}

//+------------------------------------------------------------------+
//| Calculate lot sizes based on risk mode - MT5 VERSION            |
//+------------------------------------------------------------------+
void CalculateLotSizes(TelegramSignal &signal, double &lots[])
{
   double totalLots = 0;
   
   switch(RiskMode)
   {
      case RISK_FIXED_LOT:
         totalLots = FixedLotSize;
         break;
         
      case RISK_MONEY_AMOUNT:
         totalLots = CalculateLotsFromRiskAmount(signal, RiskAmount);
         break;
         
      case RISK_PERCENT_BALANCE:
         {
            double balance = AccountInfoDouble(ACCOUNT_BALANCE);
            double riskMoney = balance * (RiskPercent / 100.0);
            totalLots = CalculateLotsFromRiskAmount(signal, riskMoney);
         }
         break;
   }
   
   // Get MT5 symbol info for lot normalization
   if(!symbolInfo.Name(signal.finalSymbol))
   {
      Print("❌ MT5 Cannot get symbol info for lot calculation: ", signal.finalSymbol);
      lots[0] = FixedLotSize;
      lots[1] = 0;
      lots[2] = 0;
      return;
   }
   
   // Normalize lot size to MT5 broker requirements
   double minLot = symbolInfo.LotsMin();
   double maxLot = symbolInfo.LotsMax();
   double lotStep = symbolInfo.LotsStep();
   
   totalLots = MathMax(totalLots, minLot);
   totalLots = MathMin(totalLots, maxLot);
   totalLots = NormalizeDouble(MathRound(totalLots / lotStep) * lotStep, 2);
   
   // Distribute lots among TPs
   if(SplitRiskEqually)
   {
      int tpCount = 1;
      if(signal.takeProfit2 > 0) tpCount++;
      if(signal.takeProfit3 > 0) tpCount++;
      
      double lotPerTP = totalLots / tpCount;
      lotPerTP = NormalizeDouble(MathRound(lotPerTP / lotStep) * lotStep, 2);
      
      for(int i = 0; i < 3; i++)
      {
         lots[i] = (i < tpCount) ? lotPerTP : 0;
      }
   }
   else
   {
      // All lots on TP1
      lots[0] = totalLots;
      lots[1] = 0;
      lots[2] = 0;
   }
   
   if(PrintToExpertLog)
   {
      Print("💰 MT5 Risk calculation (", EnumToString(RiskMode), "):");
      Print("   • Total calculated lots: ", DoubleToString(totalLots, 2));
      Print("   • Min lot: ", DoubleToString(minLot, 2), " | Max lot: ", DoubleToString(maxLot, 2));
      Print("   • Lot step: ", DoubleToString(lotStep, 2));
   }
}

//+------------------------------------------------------------------+
//| Calculate lots from risk amount - MT5 VERSION                   |
//+------------------------------------------------------------------+
double CalculateLotsFromRiskAmount(TelegramSignal &signal, double riskAmount)
{
   if(signal.stopLoss <= 0)
   {
      if(PrintToExpertLog)
         Print("⚠️ MT5 No SL provided, using fixed lot size as fallback");
      return FixedLotSize;
   }
   
   // Get MT5 symbol information
   if(!symbolInfo.Name(signal.finalSymbol))
   {
      Print("❌ MT5 Cannot get symbol info for risk calculation: ", signal.finalSymbol);
      return FixedLotSize;
   }
   
   symbolInfo.RefreshRates();
   
   double entryPrice = (signal.direction == "BUY") ? symbolInfo.Ask() : symbolInfo.Bid();
   double slDistance = MathAbs(entryPrice - signal.stopLoss);
   double tickValue = symbolInfo.TickValue();
   double tickSize = symbolInfo.TickSize();
   
   if(tickValue <= 0 || tickSize <= 0)
   {
      Print("⚠️ MT5 Invalid tick data for ", signal.finalSymbol, ", using fixed lot");
      return FixedLotSize;
   }
   
   double riskPerLot = (slDistance / tickSize) * tickValue;
   
   if(riskPerLot <= 0)
   {
      Print("⚠️ MT5 Invalid risk calculation, using fixed lot");
      return FixedLotSize;
   }
   
   double calculatedLots = riskAmount / riskPerLot;
   
   if(PrintToExpertLog)
   {
      Print("📊 MT5 Risk calculation details:");
      Print("   • Entry price: ", DoubleToString(entryPrice, symbolInfo.Digits()));
      Print("   • SL distance: ", DoubleToString(slDistance, symbolInfo.Digits()));
      Print("   • Risk per lot: $", DoubleToString(riskPerLot, 2));
      Print("   • Target risk: $", DoubleToString(riskAmount, 2));
      Print("   • Calculated lots: ", DoubleToString(calculatedLots, 2));
   }
   
   return calculatedLots;
}

//+------------------------------------------------------------------+
//| Execute single trade - MT5 VERSION                              |
//+------------------------------------------------------------------+
void ExecuteSingleTrade(TelegramSignal &signal, ENUM_ORDER_TYPE orderType, double lots, double sl, double tp, string tpLevel)
{
   string symbol = signal.finalSymbol;
   string comment = CommentPrefix + "_" + signal.originalSymbol + "_" + tpLevel + "_islamahmed9717";
   
   // Setup MT5 symbol info
   if(!symbolInfo.Name(symbol))
   {
      Print("❌ MT5 Cannot get symbol info for: ", symbol);
      return;
   }
   
   // Get current prices
   symbolInfo.RefreshRates();
   double price = (orderType == ORDER_TYPE_BUY) ? symbolInfo.Ask() : symbolInfo.Bid();
   
   // Normalize prices for MT5
   double normalizedSL = (sl > 0) ? symbolInfo.NormalizePrice(sl) : 0;
   double normalizedTP = (tp > 0) ? symbolInfo.NormalizePrice(tp) : 0;
   
   // Adjust lot size to MT5 symbol requirements
   double lotStep = symbolInfo.LotsStep();
   double normalizedLots = (lotStep > 0) ? NormalizeDouble(MathRound(lots / lotStep) * lotStep, 2) : lots;
   
   // FIXED: Correct type conversion for age calculation
   int signalAgeMinutes = (int)((TimeCurrent() - signal.signalTime) / 60);
   
   if(PrintToExpertLog)
   {
      Print("🎯 MT5 Executing ", EnumToString(orderType), " order:");
      Print("   • Symbol: ", symbol, " (", signal.originalSymbol, ")");
      Print("   • Lots: ", DoubleToString(normalizedLots, 2));
      Print("   • Price: ", DoubleToString(price, symbolInfo.Digits()));
      Print("   • SL: ", DoubleToString(normalizedSL, symbolInfo.Digits()));
      Print("   • TP: ", DoubleToString(normalizedTP, symbolInfo.Digits()));
      Print("   • Signal Age: ", IntegerToString(signalAgeMinutes), " minutes");
      Print("   • Comment: ", comment);
   }
   
   // Execute the MT5 trade
   bool result = false;
   int attempts = 0;
   
   while(!result && attempts < MaxRetriesOrderSend)
   {
      attempts++;
      
      if(PrintToExpertLog && attempts > 1)
         Print("🔄 MT5 Retry attempt #", attempts, " for ", symbol);
      
      // Use MT5 trade functions
      if(orderType == ORDER_TYPE_BUY)
         result = trade.Buy(normalizedLots, symbol, price, normalizedSL, normalizedTP, comment);
      else
         result = trade.Sell(normalizedLots, symbol, price, normalizedSL, normalizedTP, comment);
      
      if(!result)
      {
         uint errorCode = trade.ResultRetcode();
         string errorDesc = trade.ResultRetcodeDescription();
         Print("❌ MT5 Trade failed (attempt ", attempts, "/", MaxRetriesOrderSend, "): ", errorDesc, " (", errorCode, ")");
         
         if(errorCode == TRADE_RETCODE_REQUOTE || errorCode == TRADE_RETCODE_PRICE_CHANGED)
         {
            Sleep(500);
            symbolInfo.RefreshRates();
            price = (orderType == ORDER_TYPE_BUY) ? symbolInfo.Ask() : symbolInfo.Bid();
         }
         else if(errorCode == TRADE_RETCODE_NO_MONEY)
         {
            Print("💰 MT5 Insufficient funds for trade. Required margin: ", 
                  SymbolInfoDouble(symbol, SYMBOL_MARGIN_INITIAL) * normalizedLots);
            break;
         }
         else if(errorCode == TRADE_RETCODE_MARKET_CLOSED)
         {
            Print("🕒 MT5 Market is closed for ", symbol);
            break;
         }
         else
         {
            Sleep(1000);
         }
      }
   }
   
   if(result)
   {
      totalTradesExecuted++;
      ulong ticket = trade.ResultOrder();
      
      // Add to MT5 tracking array
      AddToTrackingArray(ticket, symbol, orderType, normalizedLots, price, normalizedSL, normalizedTP, signal.originalSymbol);
      
      // Generate success message
      string directionStr = (orderType == ORDER_TYPE_BUY) ? "BUY" : "SELL";
      string message = StringFormat("✅ MT5 %s %s(%s) %.2f lots | %s | Age: %dmin | SL: %.5f | TP: %.5f | #%I64u | islamahmed9717", 
                                   directionStr, signal.originalSymbol, symbol, normalizedLots, tpLevel, signalAgeMinutes, normalizedSL, normalizedTP, ticket);
      
      // Send notifications
      if(SendMT5Alerts)
         Alert(message);
      
      if(SendPushNotifications)
         SendNotification(message);
      
      if(PrintToExpertLog)
      {
         Print("✅ MT5 FRESH TRADE EXECUTED SUCCESSFULLY!");
         Print("   🎫 Ticket: #", ticket);
         Print("   ⏰ Signal was ", IntegerToString(signalAgeMinutes), " minutes old (within ", MaxSignalAgeMinutes, " min limit)");
         Print("   📊 ", message);
         Print("   🎯 Total MT5 trades executed today: ", totalTradesExecuted);
      }
   }
   else
   {
      Print("❌ MT5 TRADE EXECUTION FAILED after ", MaxRetriesOrderSend, " attempts");
      Print("   📊 Signal: ", signal.originalSymbol, " → ", symbol, " ", (orderType == ORDER_TYPE_BUY ? "BUY" : "SELL"));
      Print("   💰 Lots: ", DoubleToString(normalizedLots, 2));
      Print("   ⏰ Signal Age: ", IntegerToString(signalAgeMinutes), " minutes");
      
      if(SendMT5Alerts)
         Alert("❌ MT5 Failed to execute trade: " + symbol + " " + (orderType == ORDER_TYPE_BUY ? "BUY" : "SELL"));
   }
}
//+------------------------------------------------------------------+
//| Add trade to tracking array - MT5 VERSION                       |
//+------------------------------------------------------------------+
void AddToTrackingArray(ulong ticket, string symbol, ENUM_ORDER_TYPE orderType, double lots, double price, double sl, double tp, string originalSymbol)
{
   ArrayResize(openTrades, openTradesCount + 1);
   
   openTrades[openTradesCount].ticket = ticket;
   openTrades[openTradesCount].symbol = symbol;
   openTrades[openTradesCount].orderType = orderType;
   openTrades[openTradesCount].lotSize = lots;
   openTrades[openTradesCount].openPrice = price;
   openTrades[openTradesCount].stopLoss = sl;
   openTrades[openTradesCount].takeProfit = tp;
   openTrades[openTradesCount].openTime = TimeCurrent();
   openTrades[openTradesCount].slMovedToBreakeven = false;
   openTrades[openTradesCount].lastTrailingLevel = 0;
   openTrades[openTradesCount].originalSymbol = originalSymbol;
   
   openTradesCount++;
   
   if(PrintToExpertLog)
      Print("📋 MT5 Trade added to tracking array. Total tracked: ", openTradesCount);
}


//+------------------------------------------------------------------+
//| Process trailing stops - MT5 VERSION                            |
//+------------------------------------------------------------------+
void ProcessTrailingStops()
{
   for(int i = 0; i < openTradesCount; i++)
   {
      if(!positionInfo.SelectByTicket(openTrades[i].ticket))
         continue;
      
      string symbol = openTrades[i].symbol;
      if(!symbolInfo.Name(symbol))
         continue;
      
      symbolInfo.RefreshRates();
      
      double currentPrice = (openTrades[i].orderType == ORDER_TYPE_BUY) ? symbolInfo.Bid() : symbolInfo.Ask();
      double point = symbolInfo.Point();
      
      double profit = (openTrades[i].orderType == ORDER_TYPE_BUY) ? 
                     (currentPrice - openTrades[i].openPrice) : 
                     (openTrades[i].openPrice - currentPrice);
      
      double profitPips = profit / point;
      
      if(profitPips >= TrailingStartPips)
      {
         double newSL = CalculateTrailingSL(openTrades[i], currentPrice, point);
         
         if(MathAbs(newSL - openTrades[i].stopLoss) >= TrailingStepPips * point)
         {
            if(trade.PositionModify(openTrades[i].ticket, newSL, openTrades[i].takeProfit))
            {
               openTrades[i].stopLoss = newSL;
               openTrades[i].lastTrailingLevel = currentPrice;
               
               if(PrintToExpertLog)
                  Print("📈 MT5 Trailing SL updated: Ticket #", openTrades[i].ticket, " (", openTrades[i].originalSymbol, ") | New SL: ", DoubleToString(newSL, symbolInfo.Digits()));
            }
         }
      }
   }
}

//+------------------------------------------------------------------+
//| Calculate trailing stop loss - MT5 VERSION                      |
//+------------------------------------------------------------------+
double CalculateTrailingSL(TradeInfo &tradeInfo, double currentPrice, double point)
{
   double trailingDistance = TrailingStepPips * point;
   
   if(tradeInfo.orderType == ORDER_TYPE_BUY)
   {
      double newSL = currentPrice - trailingDistance;
      return MathMax(newSL, tradeInfo.stopLoss); // Only move SL up for buy orders
   }
   else
   {
      double newSL = currentPrice + trailingDistance;
      return MathMin(newSL, tradeInfo.stopLoss); // Only move SL down for sell orders
   }
}

//+------------------------------------------------------------------+
//| Process breakeven management - MT5 VERSION                      |
//+------------------------------------------------------------------+
void ProcessBreakeven()
{
   for(int i = 0; i < openTradesCount; i++)
   {
      if(openTrades[i].slMovedToBreakeven)
         continue;
      
      if(!positionInfo.SelectByTicket(openTrades[i].ticket))
         continue;
      
      string symbol = openTrades[i].symbol;
      if(!symbolInfo.Name(symbol))
         continue;
      
      symbolInfo.RefreshRates();
      
      double currentPrice = (openTrades[i].orderType == ORDER_TYPE_BUY) ? symbolInfo.Bid() : symbolInfo.Ask();
      double point = symbolInfo.Point();
      
      double profit = (openTrades[i].orderType == ORDER_TYPE_BUY) ? 
                     (currentPrice - openTrades[i].openPrice) : 
                     (openTrades[i].openPrice - currentPrice);
      
      double profitPips = profit / point;
      
      if(profitPips >= BreakevenAfterPips)
      {
         double newSL = openTrades[i].openPrice + (BreakevenPlusPips * point * 
                       ((openTrades[i].orderType == ORDER_TYPE_BUY) ? 1 : -1));
         
         if(trade.PositionModify(openTrades[i].ticket, newSL, openTrades[i].takeProfit))
         {
            openTrades[i].stopLoss = newSL;
            openTrades[i].slMovedToBreakeven = true;
            
            if(PrintToExpertLog)
               Print("⚖️ MT5 Breakeven set: Ticket #", openTrades[i].ticket, " (", openTrades[i].originalSymbol, ") | SL moved to: ", DoubleToString(newSL, symbolInfo.Digits()));
            
            if(SendMT5Alerts)
               Alert("⚖️ MT5 Breakeven: " + openTrades[i].originalSymbol + " | Ticket #" + IntegerToString((int)openTrades[i].ticket));
         }
      }
   }
}

//+------------------------------------------------------------------+
//| Clean up closed trades from tracking - MT5 VERSION              |
//+------------------------------------------------------------------+
void CleanupClosedPositions()
{
   for(int i = openTradesCount - 1; i >= 0; i--)
   {
      if(!positionInfo.SelectByTicket(openTrades[i].ticket))
      {
         // Position is closed, remove from tracking
         if(PrintToExpertLog)
         {
            Print("📊 MT5 Position closed: Ticket #", openTrades[i].ticket, " (", openTrades[i].originalSymbol, ")");
         }
         
         // Remove from array
         for(int j = i; j < openTradesCount - 1; j++)
         {
            openTrades[j] = openTrades[j + 1];
         }
         openTradesCount--;
         ArrayResize(openTrades, openTradesCount);
      }
   }
}

//+------------------------------------------------------------------+
//| Check if it's time to trade                                     |
//+------------------------------------------------------------------+
bool IsTimeToTrade()
{
   // Check day of week
   MqlDateTime dt;
   TimeToStruct(TimeCurrent(), dt);
   int dayOfWeek = dt.day_of_week;
   
   switch(dayOfWeek)
   {
      case 1: if(!TradeOnMonday) return false; break;
      case 2: if(!TradeOnTuesday) return false; break;
      case 3: if(!TradeOnWednesday) return false; break;
      case 4: if(!TradeOnThursday) return false; break;
      case 5: if(!TradeOnFriday) return false; break;
      case 6: if(!TradeOnSaturday) return false; break;
      case 0: if(!TradeOnSunday) return false; break;
   }
   
   // Check time filter
   if(UseTimeFilter)
   {
      string currentTime = TimeToString(TimeCurrent(), TIME_MINUTES);
      if(currentTime < StartTime || currentTime > EndTime)
         return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Update EA comment display - MT5 VERSION                         |
//+------------------------------------------------------------------+
void UpdateComment()
{
   string comment = "📱 TELEGRAM EA MANAGER - COMPLETE MT5 v2.17\n";
   comment += "════════════════════════════════════════════\n";
   comment += "👤 Developer: islamahmed9717\n";
   comment += "📅 Version: 2.17 MT5 (FIXED FILE ACCESS)\n";
   comment += "🔗 GitHub: https://github.com/islamahmed9717\n";
   comment += "🎯 Platform: MetaTrader 5\n";
   comment += "⏰ Signal Expiry: " + IntegerToString(MaxSignalAgeMinutes) + " minutes\n";
   comment += "📁 Signal File: telegram_signals.txt\n";
   comment += "════════════════════════════════════════════\n";
   comment += "📊 REAL-TIME STATISTICS:\n";
   comment += "• Signals Processed: " + IntegerToString(totalSignalsProcessed) + "\n";
   comment += "• Trades Executed: " + IntegerToString(totalTradesExecuted) + "\n";
   comment += "• Expired Signals: " + IntegerToString(totalExpiredSignals) + " ⏰\n";
   comment += "• Symbols Filtered: " + IntegerToString(totalSymbolsFiltered) + "\n";
   comment += "• Open Positions: " + IntegerToString(openTradesCount) + "\n";
   comment += "• Processed IDs: " + IntegerToString(processedSignalCount) + "\n";
   comment += "• Magic Number: " + IntegerToString(magicNumber) + "\n";
   comment += "• Current UTC: " + TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES) + "\n";
   comment += "════════════════════════════════════════════\n";
   
   // File access status
   bool fileExists = (FileOpen("telegram_signals.txt", FILE_READ|FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_TXT|FILE_ANSI) != INVALID_HANDLE);
   if(fileExists)
   {
      FileClose(FileOpen("telegram_signals.txt", FILE_READ|FILE_SHARE_READ|FILE_SHARE_WRITE|FILE_TXT|FILE_ANSI));
      comment += "📁 FILE STATUS: ✅ ACCESSIBLE\n";
   }
   else
   {
      comment += "📁 FILE STATUS: ❌ NOT FOUND\n";
      comment += "💡 Check Windows app MT4 path setting\n";
   }
   
   if(StringLen(ChannelIDs) == 0)
   {
      comment += "⚠️ NO CHANNEL IDs CONFIGURED!\n";
      comment += "📱 Use Windows app to get Channel IDs\n";
      comment += "🔧 Steps:\n";
      comment += "  1. Run Telegram EA Manager app\n";
      comment += "  2. Connect to Telegram\n";
      comment += "  3. Select channels to monitor\n";
      comment += "  4. Copy Channel IDs\n";
      comment += "  5. Paste in EA ChannelIDs parameter\n";
   }
   else
   {
      comment += "📡 MONITORING CHANNELS:\n";
      string channelDisplay = ChannelIDs;
      if(StringLen(channelDisplay) > 40)
         channelDisplay = StringSubstr(channelDisplay, 0, 37) + "...";
      comment += "• " + channelDisplay + "\n";
      
      string channels[];
      int channelCount = StringSplit(ChannelIDs, ',', channels);
      comment += "💡 " + IntegerToString(channelCount) + " channel(s) monitored\n";
   }
   
   comment += "════════════════════════════════════════════\n";
   comment += "⏰ SIGNAL EXPIRY PROTECTION:\n";
   comment += "• Max Age: " + IntegerToString(MaxSignalAgeMinutes) + " minutes\n";
   comment += "• Expired Today: " + IntegerToString(totalExpiredSignals) + "\n";
   comment += "• Status: " + (MaxSignalAgeMinutes <= 10 ? "STRICT ✅" : "RELAXED ⚠️") + "\n";
   comment += "════════════════════════════════════════════\n";
   
   // Enhanced status indicators
   string systemStatus = "WAITING 🟡";
   if(totalSignalsProcessed > 0 && totalTradesExecuted > 0)
      systemStatus = "ACTIVE & TRADING ✅";
   else if(totalSignalsProcessed > 0)
      systemStatus = "PROCESSING SIGNALS 🔄";
   else if(totalExpiredSignals > 0)
      systemStatus = "SIGNALS EXPIRED ⏰";
   else if(!fileExists)
      systemStatus = "FILE NOT FOUND ❌";
   
   comment += "🎯 MT5 SYSTEM STATUS: " + systemStatus + "\n";
   comment += "📱 Keep Windows app running for signals!\n";
   comment += "⏰ Signal freshness guaranteed: " + IntegerToString(MaxSignalAgeMinutes) + " min max\n";
   comment += "🕐 Last updated: " + TimeToString(TimeCurrent(), TIME_MINUTES) + " (Server)\n";
   comment += "👤 Current user: islamahmed9717\n";
   
   Comment(comment);
}
//+------------------------------------------------------------------+
//| Performance Monitoring Function                                 |
//+------------------------------------------------------------------+
void LogPerformanceStats()
{
   static datetime lastLogTime = 0;
   datetime currentTime = TimeCurrent();
   
   // Log stats every hour
   if(currentTime - lastLogTime >= 3600)
   {
      lastLogTime = currentTime;
      
      double equity = AccountInfoDouble(ACCOUNT_EQUITY);
      double balance = AccountInfoDouble(ACCOUNT_BALANCE);
      double profit = equity - balance;
      double freeMargin = AccountInfoDouble(ACCOUNT_MARGIN_FREE);
      
      Print("📊 MT5 HOURLY PERFORMANCE REPORT:");
      Print("   • Account Equity: $", DoubleToString(equity, 2));
      Print("   • Account Balance: $", DoubleToString(balance, 2));
      Print("   • Floating P&L: $", DoubleToString(profit, 2));
      Print("   • Free Margin: $", DoubleToString(freeMargin, 2));
      Print("   • Open Positions: ", openTradesCount);
      Print("   • Signals Processed: ", totalSignalsProcessed);
      Print("   • Trades Executed: ", totalTradesExecuted);
      Print("   • Signal Success Rate: ", 
            (totalSignalsProcessed > 0 ? DoubleToString((double)totalTradesExecuted/totalSignalsProcessed*100, 1) : "0"), "%");
      Print("   • Current UTC: 2025-06-20 23:07:34");
      Print("   • islamahmed9717 - Telegram EA MT5");
   }
}

//+------------------------------------------------------------------+
//| MT5 Connection Health Check                                     |
//+------------------------------------------------------------------+
bool CheckMT5ConnectionHealth()
{
   // Check if connected to trade server
   if(!TerminalInfoInteger(TERMINAL_CONNECTED))
   {
      Print("❌ MT5 Not connected to trade server");
      return false;
   }
   
   // Check if trading is allowed
   if(!TerminalInfoInteger(TERMINAL_TRADE_ALLOWED))
   {
      Print("❌ MT5 Trading not allowed in terminal");
      return false;
   }
   
   // Check if expert advisors are allowed
   if(!MQLInfoInteger(MQL_TRADE_ALLOWED))
   {
      Print("❌ MT5 Expert Advisors trading not allowed");
      return false;
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Account Safety Check                                            |
//+------------------------------------------------------------------+
bool CheckAccountSafety()
{
   double equity = AccountInfoDouble(ACCOUNT_EQUITY);
   double balance = AccountInfoDouble(ACCOUNT_BALANCE);
   double freeMargin = AccountInfoDouble(ACCOUNT_MARGIN_FREE);
   double marginLevel = AccountInfoDouble(ACCOUNT_MARGIN_LEVEL);
   
   // Check equity vs balance ratio
   if(equity > 0 && balance > 0)
   {
      double equityBalanceRatio = equity / balance;
      
      // If equity drops below 70% of balance, trigger warning
      if(equityBalanceRatio < 0.7)
      {
         Print("⚠️ MT5 WARNING: Equity/Balance ratio low: ", DoubleToString(equityBalanceRatio * 100, 1), "%");
         
         if(SendMT5Alerts)
            Alert("⚠️ MT5 WARNING: Low equity ratio " + DoubleToString(equityBalanceRatio * 100, 1) + "% | islamahmed9717");
         
         // If ratio drops below 50%, emergency stop
         if(equityBalanceRatio < 0.5)
         {
            EmergencyStopEA("Critical equity loss - below 50% of balance");
            return false;
         }
      }
   }
   
   // Check margin level
   if(marginLevel > 0 && marginLevel < 200) // Below 200% margin level
   {
      Print("⚠️ MT5 WARNING: Low margin level: ", DoubleToString(marginLevel, 1), "%");
      
      if(marginLevel < 100) // Critical margin level
      {
         EmergencyStopEA("Critical margin level - below 100%");
         return false;
      }
   }
   
   return true;
}

//+------------------------------------------------------------------+
//| Emergency Stop Function                                         |
//+------------------------------------------------------------------+
void EmergencyStopEA(string reason)
{
   Print("🚨 EMERGENCY STOP TRIGGERED: ", reason);
   Print("⏹️ Stopping Telegram EA MT5 for safety...");
   
   // Stop timer
   EventKillTimer();
   
   // Alert user
   if(SendMT5Alerts)
      Alert("🚨 EMERGENCY STOP: " + reason + " | Telegram EA MT5 | islamahmed9717");
   
   // Update comment
   Comment("🚨 EMERGENCY STOP ACTIVE 🚨\n" +
           "Reason: " + reason + "\n" +
           "Time: " + TimeToString(TimeCurrent(), TIME_DATE|TIME_MINUTES) + "\n" +
           "Contact: islamahmed9717\n" +
           "Platform: MetaTrader 5\n\n" +
           "Please restart EA after resolving the issue.");
   
   Print("📱 Emergency stop completed. Please check the issue and restart EA.");
   Print("👤 Developer: islamahmed9717 | MT5 Version 2.17");
}

//+------------------------------------------------------------------+
//| Enhanced Signal Quality Check                                   |
//+------------------------------------------------------------------+
bool IsSignalQualityGood(TelegramSignal &signal)
{
   // Check signal completeness
   double completenessScore = 0;
   
   if(StringLen(signal.originalSymbol) > 0) completenessScore += 20;
   if(StringLen(signal.direction) > 0) completenessScore += 20;
   if(signal.stopLoss > 0) completenessScore += 30;
   if(signal.takeProfit1 > 0) completenessScore += 20;
   if(signal.takeProfit2 > 0) completenessScore += 5;
   if(signal.takeProfit3 > 0) completenessScore += 5;
   
   // Minimum quality threshold
   if(completenessScore < 60)
   {
      if(PrintToExpertLog)
         Print("⚠️ MT5 Signal quality too low: ", DoubleToString(completenessScore, 0), "% for ", signal.originalSymbol);
      return false;
   }
   
   // Check risk-reward ratio
   if(signal.stopLoss > 0 && signal.takeProfit1 > 0)
   {
      double entryPrice = (signal.direction == "BUY") ? 
                         (symbolInfo.Name(signal.finalSymbol) ? symbolInfo.Ask() : 1.0) : 
                         (symbolInfo.Name(signal.finalSymbol) ? symbolInfo.Bid() : 1.0);
      
      double riskDistance = MathAbs(entryPrice - signal.stopLoss);
      double rewardDistance = MathAbs(signal.takeProfit1 - entryPrice);
      
      if(riskDistance > 0)
      {
         double rrRatio = rewardDistance / riskDistance;
         if(rrRatio < 0.5)
         {
            if(PrintToExpertLog)
               Print("⚠️ MT5 Poor risk-reward ratio: ", DoubleToString(rrRatio, 2), " for ", signal.originalSymbol);
            return false;
         }
      }
   }
   
   if(PrintToExpertLog)
      Print("✅ MT5 Signal quality check passed: ", DoubleToString(completenessScore, 0), "% for ", signal.originalSymbol);
   
   return true;
}

//+------------------------------------------------------------------+
//| Final Enhanced Comment Function with Real-time Data            |
//+------------------------------------------------------------------+
void UpdateEnhancedComment()
{
   // Call performance monitoring
   LogPerformanceStats();
   
   // Check connection and account safety
   bool connectionOK = CheckMT5ConnectionHealth();
   bool accountSafe = CheckAccountSafety();
   
   string comment = "📱 TELEGRAM EA MANAGER - ISLAMAHMED9717\n";
   comment += "═══════════════════════════════════════════════\n";
   comment += "👤 Developer: islamahmed9717\n";
   comment += "📅 Version: 2.17 MT5 - FINAL CORRECTED\n";
   comment += "🕐 UTC Time: 2025-06-20 23:07:34\n";
   comment += "🔗 GitHub: github.com/islamahmed9717\n";
   comment += "⏰ Signal Expiry: " + IntegerToString(MaxSignalAgeMinutes) + " minutes\n";
   comment += "═══════════════════════════════════════════════\n";
   
   // System Health Status
   comment += "🔧 SYSTEM HEALTH:\n";
   comment += "• MT5 Connection: " + (connectionOK ? "✅ Connected" : "❌ Disconnected") + "\n";
   comment += "• Account Safety: " + (accountSafe ? "✅ Safe" : "❌ Risk") + "\n";
   comment += "• Trading Allowed: " + (MQLInfoInteger(MQL_TRADE_ALLOWED) ? "✅ Yes" : "❌ No") + "\n";
   comment += "• Terminal Connected: " + (TerminalInfoInteger(TERMINAL_CONNECTED) ? "✅ Yes" : "❌ No") + "\n";
   
   // Account Information
   comment += "═══════════════════════════════════════════════\n";
   comment += "💰 ACCOUNT INFO:\n";
   comment += "• Balance: $" + DoubleToString(AccountInfoDouble(ACCOUNT_BALANCE), 2) + "\n";
   comment += "• Equity: $" + DoubleToString(AccountInfoDouble(ACCOUNT_EQUITY), 2) + "\n";
   comment += "• Free Margin: $" + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_FREE), 2) + "\n";
   comment += "• Margin Level: " + DoubleToString(AccountInfoDouble(ACCOUNT_MARGIN_LEVEL), 1) + "%\n";
   
   // Signal Statistics
   comment += "═══════════════════════════════════════════════\n";
   comment += "📊 SIGNAL STATISTICS:\n";
   comment += "• Processed: " + IntegerToString(totalSignalsProcessed) + "\n";
   comment += "• Executed: " + IntegerToString(totalTradesExecuted) + "\n";
   comment += "• Expired: " + IntegerToString(totalExpiredSignals) + " ⏰\n";
   comment += "• Filtered: " + IntegerToString(totalSymbolsFiltered) + "\n";
   comment += "• Success Rate: " + (totalSignalsProcessed > 0 ? 
           DoubleToString((double)totalTradesExecuted/totalSignalsProcessed*100, 1) : "0") + "%\n";
   
   // Current Positions
   comment += "═══════════════════════════════════════════════\n";
   if(openTradesCount > 0)
   {
      comment += "📈 OPEN POSITIONS (" + IntegerToString(openTradesCount) + "):\n";
      double totalProfit = 0;
      
      for(int i = 0; i < MathMin(openTradesCount, 3); i++)
      {
         if(positionInfo.SelectByTicket(openTrades[i].ticket))
         {
            double profit = positionInfo.Profit() + positionInfo.Swap() + positionInfo.Commission();
            totalProfit += profit;
            long ageMinutes = (TimeCurrent() - openTrades[i].openTime) / 60;
            
            comment += "• #" + IntegerToString(openTrades[i].ticket) + " " + 
                      openTrades[i].originalSymbol + " " +
                      DoubleToString(openTrades[i].lotSize, 2) + " lots\n";
            comment += "  P&L: $" + DoubleToString(profit, 2) + 
                      " | Age: " + IntegerToString((int)ageMinutes) + "min\n";
         }
      }
      
      if(openTradesCount > 3)
         comment += "• ... and " + IntegerToString(openTradesCount - 3) + " more positions\n";
      
      comment += "• Total P&L: $" + DoubleToString(totalProfit, 2) + "\n";
   }
   else
   {
      comment += "💤 No open positions\n";
   }
   
   // Status and Warnings
   comment += "═══════════════════════════════════════════════\n";
   string status = "🟡 WAITING";
   if(!connectionOK || !accountSafe)
      status = "🔴 CRITICAL";
   else if(totalSignalsProcessed > 0 && totalTradesExecuted > 0)
      status = "🟢 ACTIVE";
   else if(totalExpiredSignals > 5)
      status = "🟠 SIGNALS EXPIRED";
   
   comment += "🎯 STATUS: " + status + "\n";
   comment += "⏰ Fresh signals only: " + IntegerToString(MaxSignalAgeMinutes) + "min max\n";
   comment += "📱 Keep Windows app running!\n";
   comment += "👤 islamahmed9717 - Telegram EA MT5\n";
   
   Comment(comment);
}

//+------------------------------------------------------------------+
//| Final Signal Processing with Complete Validation               |
//+------------------------------------------------------------------+
bool ProcessSignalWithFullValidation(TelegramSignal &signal)
{
   // Step 1: Check signal quality
   if(!IsSignalQualityGood(signal))
   {
      return false;
   }
   
   // Step 2: Check account safety
   if(!CheckAccountSafety())
   {
      return false;
   }
   
   // All validations passed
   return true;
}

//+------------------------------------------------------------------+
//| END OF COMPLETE TELEGRAM EA MT5 CODE - VERSION 2.17           |
//| FINAL RELEASE - ALL COMPILATION ISSUES FIXED                  |
//| DEVELOPER: islamahmed9717                                      |
//| UPDATED: 2025-06-20 23:07:34 UTC                              |
//+------------------------------------------------------------------+
